//
// IPieceWriter.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;

using ReusableTasks;

namespace MonoTorrent.PieceWriter
{
    public interface IPieceWriter : IDisposable
    {
        int OpenFiles { get; }
        int MaximumOpenFiles { get; }

        /// <summary>
        /// Releases all resources associated with the specified file.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        ReusableTask CloseAsync (ITorrentManagerFile file);

        /// <summary>
        /// Returns false if the file already exists, otherwise creates the file and returns true.  
        /// </summary>
        /// <param name="file">The file to create</param>
        /// <param name="options">Determines how new files will be created.</param>
        /// <returns></returns>
        ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions options);

        /// <summary>
        /// Returns true if the file exists.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        ReusableTask<bool> ExistsAsync (ITorrentManagerFile file);

        /// <summary>
        /// Flush any cached data to the file.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        ReusableTask FlushAsync (ITorrentManagerFile file);

        /// <summary>
        /// Returns null if the specified file does not exist, otherwise returns the length of the file in bytes.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file);

        /// <summary>
        /// Moves the specified file to the new location. Optionally overwrite any pre-existing files.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="fullPath"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite);

        /// <summary>
        /// Reads the specified amount of data from the specified file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="offset"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer);

        /// <summary>
        /// Returns false and no action is taken if the file does not already exist. If the file does exist
        /// it's length is set to the provided value.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length);

        /// <summary>
        /// Optional limit to the maximum number of files this writer can have open concurrently.
        /// </summary>
        /// <param name="maximumOpenFiles"></param>
        /// <returns></returns>
        ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles);

        /// <summary>
        /// Writes all data in the provided buffer to the specified file. Some implementatations may
        /// have an internal cache, which means <see cref="FlushAsync(ITorrentManagerFile)"/> should
        /// be invoked to guarantee the data is written to it's final destination.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="offset"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer);
    }

    public static class PaddingAwareIPieceWriterExtensions
    {
        private static (int todoFile, int todoPad) Partition (ITorrentManagerFile file, long offset, int todo)
        {
            int todoFile = (offset + todo) > file.Length ? (int) Math.Max (0, file.Length - offset) : todo;
            int todoPad = (int) Math.Min (todo - todoFile, file.Padding);
            return (todoFile, todoPad);
        }

        public static async ReusableTask<int> PaddingAwareReadAsync (this IPieceWriter writer, ITorrentManagerFile file, long offset, Memory<byte> buffer)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > (file.Length + file.Padding))
                throw new ArgumentOutOfRangeException (nameof (offset));

            (var todoFile, var todoPadding) = Partition (file, offset, buffer.Length);

            int done = 0;

            if (todoFile > 0) {
                done = await writer.ReadAsync (file, offset, buffer.Slice (0, todoFile)).ConfigureAwait (false);
                if (done < todoFile)
                    return done;
            }

            if (todoPadding > 0) {
                buffer.Slice (done, todoPadding).Span.Clear ();
                done += todoPadding;
            }

            return done;
        }

        public static async ReusableTask<(int total, int padding)> PaddingAwareReadAsyncForCreator (this IPieceWriter writer, ITorrentManagerFile file, long offset, Memory<byte> buffer)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > (file.Length + file.Padding))
                throw new ArgumentOutOfRangeException (nameof (offset));

            (var todoFile, var todoPadding) = Partition (file, offset, buffer.Length);

            int done = 0;

            if (todoFile > 0) {
                done = await writer.ReadAsync (file, offset, buffer.Slice (0, todoFile));
                if (done < todoFile)
                    return (done, 0);
            }

            if (todoPadding > 0) {
                buffer.Slice (done, todoPadding).Span.Clear ();
                done += todoPadding;
            }

            return (done, todoPadding);
        }

        public static
#if DEBUG
            async
#endif
            ReusableTask PaddingAwareWriteAsync (this IPieceWriter writer, ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > (file.Length + file.Padding))
                throw new ArgumentOutOfRangeException (nameof (offset));

            (var todoFile, var todoPadding) = Partition (file, offset, buffer.Length);

            // This won't show up in stacktraces in release builds due to the lack of 'await'.
            // Hopefully that won't be confusing :p
            if (todoFile > 0) {
#if DEBUG
                await
#else
                return
#endif
                writer.WriteAsync (file, offset, buffer.Slice (0, todoFile));
            }
#if !DEBUG
            return default;
#endif
        }
    }
}
