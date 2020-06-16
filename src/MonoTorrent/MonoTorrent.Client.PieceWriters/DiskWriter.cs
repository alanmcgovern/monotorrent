//
// DiskWriter.cs
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    public class DiskWriter : IPieceWriter
    {
        static readonly int DefaultMaxOpenFiles = 196;

#if UWP
        static readonly Func<TorrentFile, FileAccess, ReusableTask<ITorrentFileStream>> DefaultStreamCreator = async (file, access) => {
            var openedFile = await Windows.Storage.StorageFile.GetFileFromPathAsync (file.FullPath);
            var streamTask = access.HasFlag (FileAccess.Write) ? openedFile.OpenStreamForWriteAsync () : openedFile.OpenStreamForReadAsync ();
            var stream = await streamTask;
            // FIXME: return new TorrentFileStream (stream);
            throw new NotImplementedException ();
        };
#else
        static readonly Func<TorrentFile, FileAccess, ReusableTask<ITorrentFileStream>> DefaultStreamCreator =
            (file, access) => ReusableTask.FromResult<ITorrentFileStream> (new TorrentFileStream (file.FullPath, access));
#endif

        readonly SemaphoreSlim Limiter;

        public int OpenFiles => StreamCache.Count;

        readonly FileStreamBuffer StreamCache;

        public DiskWriter ()
            : this (DefaultStreamCreator, DefaultMaxOpenFiles)
        {

        }

        internal DiskWriter (Func<TorrentFile, FileAccess, ReusableTask<ITorrentFileStream>> streamCreator)
            : this (streamCreator, DefaultMaxOpenFiles)
        {

        }

        public DiskWriter (int maxOpenFiles)
            : this (DefaultStreamCreator, maxOpenFiles)
        {

        }

        internal DiskWriter (Func<TorrentFile, FileAccess, ReusableTask<ITorrentFileStream>> streamCreator, int maxOpenFiles)
        {
            StreamCache = new FileStreamBuffer (streamCreator, maxOpenFiles);
            Limiter = new SemaphoreSlim (maxOpenFiles);
        }

        public void Dispose ()
        {
            StreamCache.Dispose ();
        }

        public async ReusableTask CloseAsync (TorrentFile file)
        {
            await StreamCache.CloseStreamAsync (file);
        }

        public ReusableTask<bool> ExistsAsync (TorrentFile file)
        {
            return ReusableTask.FromResult (File.Exists (file.FullPath));
        }

        public async ReusableTask FlushAsync (TorrentFile file)
            => await StreamCache.FlushAsync (file);

        public async ReusableTask MoveAsync (TorrentFile file, string newPath, bool overwrite)
        {
            await StreamCache.CloseStreamAsync (file);

            if (overwrite)
                File.Delete (newPath);
            File.Move (file.FullPath, newPath);
        }

        public async ReusableTask<int> ReadAsync (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File (file);
            Check.Buffer (buffer);

            if (offset < 0 || offset + count > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                using var rented = await StreamCache.GetStreamAsync (file, FileAccess.Read);
                if (rented.Stream.Length < offset + count)
                    return 0;

                if (rented.Stream.Position != offset)
                    await rented.Stream.SeekAsync (offset);
                return await rented.Stream.ReadAsync (buffer, bufferOffset, count);
            }
        }

        public async ReusableTask WriteAsync (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File (file);
            Check.Buffer (buffer);

            if (offset < 0 || offset + count > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                using var rented = await StreamCache.GetStreamAsync (file, FileAccess.ReadWrite);

                // FileStream.WriteAsync does some work synchronously, according to the profiler.
                // It looks like if the file is too small it is expanded (SetLength is called)
                // synchronously before the asynchronous Write is performed.
                //
                // We also want the Seek operation to execute on the threadpool.
                await MainLoop.SwitchToThreadpool ();
                await rented.Stream.SeekAsync (offset);
                await rented.Stream.WriteAsync (buffer, bufferOffset, count).ConfigureAwait (false);
            }
        }
    }
}
