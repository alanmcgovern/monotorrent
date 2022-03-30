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
using System.Runtime.InteropServices;
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.PieceWriter
{
    public class DiskWriter : IPieceWriter
    {
        static readonly int DefaultMaxOpenFiles = 196;

        // Don't pass FileOptions.RandomAccess
        // https://docs.microsoft.com/en-GB/troubleshoot/windows-server/application-management/operating-system-performance-degrades
        //
        static readonly Func<ITorrentManagerFile, FileAccess, Stream> DefaultStreamCreator =
            (file, access) => new FileStream (file.FullPath, FileMode.OpenOrCreate, access, FileShare.ReadWrite, 1, FileOptions.None);

        SemaphoreSlim Limiter { get; set; }

        public int MaximumOpenFiles => StreamCache.Count;

        readonly FileStreamBuffer StreamCache;

        public DiskWriter ()
            : this (DefaultStreamCreator, DefaultMaxOpenFiles)
        {

        }

        internal DiskWriter (Func<ITorrentManagerFile, FileAccess, Stream> streamCreator)
            : this (streamCreator, DefaultMaxOpenFiles)
        {

        }

        public DiskWriter (int maxOpenFiles)
            : this (DefaultStreamCreator, maxOpenFiles)
        {

        }

        internal DiskWriter (Func<ITorrentManagerFile, FileAccess, Stream> streamCreator, int maxOpenFiles)
        {
            StreamCache = new FileStreamBuffer (streamCreator, maxOpenFiles);
            Limiter = new SemaphoreSlim (maxOpenFiles);
        }

        public void Dispose ()
        {
            StreamCache.Dispose ();
        }

        public async ReusableTask CloseAsync (ITorrentManagerFile file)
        {
            await StreamCache.CloseStreamAsync (file);
        }

        public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
        {
            return ReusableTask.FromResult (File.Exists (file.FullPath));
        }

        public async ReusableTask FlushAsync (ITorrentManagerFile file)
            => await StreamCache.FlushAsync (file);

        public async ReusableTask MoveAsync (ITorrentManagerFile file, string newPath, bool overwrite)
        {
            await StreamCache.CloseStreamAsync (file);

            if (overwrite)
                File.Delete (newPath);
            if (File.Exists (file.FullPath)) {
                Directory.CreateDirectory (Path.GetDirectoryName (newPath)!);
                File.Move (file.FullPath, newPath);
            }
        }

        public async ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                using var rented = await StreamCache.GetOrCreateStreamAsync (file, FileAccess.Read).ConfigureAwait (false);

                await SwitchToThreadpool ();
                if (rented.Stream!.Length < offset + buffer.Length)
                    return 0;

                if (rented.Stream.Position != offset)
                    rented.Stream.Seek (offset, SeekOrigin.Begin);
                return rented.Stream.Read (buffer);
            }
        }

        public async ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                using var rented = await StreamCache.GetOrCreateStreamAsync (file, FileAccess.ReadWrite).ConfigureAwait (false);

                // FileStream.WriteAsync does some work synchronously, according to the profiler.
                // It looks like if the file is too small it is expanded (SetLength is called)
                // synchronously before the asynchronous Write is performed.
                //
                // We also want the Seek operation to execute on the threadpool.
                await SwitchToThreadpool ();
                rented.Stream!.Seek (offset, SeekOrigin.Begin);
                rented.Stream.Write (buffer);
            }
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            Limiter = new SemaphoreSlim (maximumOpenFiles);
            return ReusableTask.CompletedTask;
        }

        static ThreadSwitcher SwitchToThreadpool ()
            => new ThreadSwitcher ();
    }
}
