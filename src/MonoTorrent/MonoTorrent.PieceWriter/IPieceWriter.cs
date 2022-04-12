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
        int MaximumOpenFiles { get; }

        ReusableTask CloseAsync (ITorrentManagerFile file);
        ReusableTask<bool> ExistsAsync (ITorrentManagerFile file);
        ReusableTask FlushAsync (ITorrentManagerFile file);
        ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite);
        ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer);
        ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer);
        ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles);
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
                done = await writer.ReadAsync (file, offset, buffer.Slice (0, todoFile));
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
                    return (done,0);
            }

            if (todoPadding > 0) {
                buffer.Slice (done, todoPadding).Span.Clear ();
                done += todoPadding;
            }

            return (done,todoPadding);
        }

        public static async ReusableTask PaddingAwareWriteAsync (this IPieceWriter writer, ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > (file.Length + file.Padding))
                throw new ArgumentOutOfRangeException (nameof (offset));

            (var todoFile, var todoPadding) = Partition (file, offset, buffer.Length);

            if (todoFile > 0) {
                await writer.WriteAsync (file, offset, buffer.Slice (0, todoFile));
            }
        }
    }
}
