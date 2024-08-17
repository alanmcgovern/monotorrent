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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent.PieceWriter
{
    public class DiskWriter : IPieceWriter
    {
        class Comparer : IEqualityComparer<ITorrentManagerFile>
        {
            public static Comparer Instance { get; } = new Comparer ();

            public bool Equals (ITorrentManagerFile? x, ITorrentManagerFile? y)
                => x == y;

            public int GetHashCode (ITorrentManagerFile obj)
                => obj.GetHashCode ();
        }

        class AllStreams
        {
            public ReusableSemaphore Locker = new ReusableSemaphore (1);
            public List<StreamData> Streams = new List<StreamData> ();
        }

        class StreamData
        {
            public ReusableSemaphore Locker = new ReusableSemaphore (1);
            public long LastUsedStamp = Stopwatch.GetTimestamp ();
            public IFileReaderWriter? Stream;
        }


        static readonly int DefaultMaxOpenFiles = 196;

        ReusableSemaphore Limiter { get; set; }

        public int OpenFiles { get; private set; }

        public int MaximumOpenFiles { get; private set; }

        Dictionary<ITorrentManagerFile, AllStreams> Streams { get; }

        public DiskWriter ()
            : this (DefaultMaxOpenFiles)
        {

        }

        public DiskWriter (int maxOpenFiles)
        {
            MaximumOpenFiles = maxOpenFiles;
            Limiter = new ReusableSemaphore (8); // 8 concurrent disk writer threads? Should be enough?
            Streams = new Dictionary<ITorrentManagerFile, AllStreams> ();
        }

        public async ReusableTask CloseAsync (ITorrentManagerFile file)
        {
            ThrowIfNoSyncContext ();

            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (Streams.TryGetValue (file, out AllStreams? allStreams)) {
                Streams.Remove (file);
                using (await allStreams.Locker.EnterAsync ())
                    await CloseAllAsync (allStreams);
            }
        }

        async ReusableTask CloseAllAsync (AllStreams allStreams)
        {
            ThrowIfNoSyncContext ();

            foreach (var data in allStreams.Streams) {
                using (await data.Locker.EnterAsync ()) {
                    data.Stream?.Dispose ();
                    OpenFiles--;
                }
            }
            allStreams.Streams.Clear ();
        }

        public async ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions options)
        {
            await new EnsureThreadPool ();

            if (File.Exists (file.FullPath))
                return false;

            var parent = Path.GetDirectoryName (file.FullPath);
            if (!string.IsNullOrEmpty (parent))
                Directory.CreateDirectory (parent);

            if (options == FileCreationOptions.PreferPreallocation) {
#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NETCOREAPP3_0 || NET472
                    if (!File.Exists (file.FullPath))
                        using (var fs = new FileStream (file.FullPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)) {
                            fs.SetLength (file.Length);
                            fs.Seek (file.Length - 1, SeekOrigin.Begin);
                            fs.Write (new byte[1]);
                        }
#else
                File.OpenHandle (file.FullPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, FileOptions.None, file.Length).Dispose ();
#endif
            } else {
                try {
                    NtfsSparseFile.CreateSparse (file.FullPath, file.Length);
                } catch {
                    // who cares if we can't pre-allocate a sparse file. Try a regular file!
                    new FileStream (file.FullPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete).Dispose ();
                }
            }
            return true;
        }

        public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            return ReusableTask.FromResult (File.Exists (file.FullPath));
        }

        public async ReusableTask FlushAsync (ITorrentManagerFile file)
        {
            ThrowIfNoSyncContext ();

            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (Streams.TryGetValue (file, out AllStreams? allStreams)) {
                using var releaser = await allStreams.Locker.EnterAsync ();
                foreach (var data in allStreams.Streams) {
                    using (await data.Locker.EnterAsync ()) {
                        await data.Stream!.FlushAsync ();
                    }
                }
            }
        }

        public async ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file)
        {
            await new EnsureThreadPool ();
            var info = new FileInfo (file.FullPath);
            return info.Exists ? info.Length : (long?) null;
        }

        public async ReusableTask MoveAsync (ITorrentManagerFile file, string newPath, bool overwrite)
        {
            ThrowIfNoSyncContext ();

            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (!Streams.TryGetValue (file, out AllStreams? data))
                Streams[file] = data = new AllStreams ();

            using var releaser = await data.Locker.EnterAsync ();
            await CloseAllAsync (data).ConfigureAwait (false);

            await new EnsureThreadPool ();
            if (File.Exists (file.FullPath)) {
                if (overwrite)
                    File.Delete (newPath);

                Directory.CreateDirectory (Path.GetDirectoryName (newPath)!);
                File.Move (file.FullPath, newPath);
            }
        }

        public async ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
        {
            ThrowIfNoSyncContext ();

            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                (var writer, var releaser) = await GetOrCreateStreamAsync (file, FileAccess.Read).ConfigureAwait (false);
                using (releaser)
                    if (writer != null)
                        return await writer.ReadAsync (buffer, offset).ConfigureAwait (false);
                return 0;
            }
        }

        public async ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length)
        {
            await new EnsureThreadPool ();
            var info = new FileInfo (file.FullPath);
            if (!info.Exists)
                return false;

            using (var fileStream = new FileStream (file.FullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1, FileOptions.None))
                fileStream.SetLength (file.Length);
            return true;
        }

        public async ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            ThrowIfNoSyncContext ();

            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                (var writer, var releaser) = await GetOrCreateStreamAsync (file, FileAccess.ReadWrite).ConfigureAwait (false);
                using (releaser)
                    await writer.WriteAsync (buffer, offset).ConfigureAwait (false);
            }
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            MaximumOpenFiles = maximumOpenFiles;
            return ReusableTask.CompletedTask;
        }

        internal async ReusableTask<(IFileReaderWriter, ReusableSemaphore.Releaser)> GetOrCreateStreamAsync (ITorrentManagerFile file, FileAccess access)
        {
            if (!Streams.TryGetValue (file, out AllStreams? allStreams))
                allStreams = Streams[file] = new AllStreams ();

            // If this completes synchronously we will want to swap threads before doing file manipulation later
            // in the method. If we already have a cached FileStream we won't need to swap threads before returning it.
            StreamData freshStreamData;
            ReusableSemaphore.Releaser freshStreamDataReleaser;
            using (await allStreams.Locker.EnterAsync ()) {
                // We should check if the on-disk file needs truncation if this is the very first time we're opening it.
                foreach (var existing in allStreams.Streams) {
                    if (existing.Locker.TryEnter (out ReusableSemaphore.Releaser r)) {
                        if (((access & FileAccess.Write) != FileAccess.Write) || existing.Stream!.CanWrite) {
                            existing.LastUsedStamp = Stopwatch.GetTimestamp ();
                            return (existing.Stream!, r);
                        } else {
                            r.Dispose ();
                        }
                    }
                }

                // Create the stream data and acquire the lock immediately, so any async invocation of MaybeRemoveOldestStream can't kill the stream. 
                freshStreamData = new StreamData ();
                freshStreamDataReleaser = await freshStreamData.Locker.EnterAsync ();
                allStreams.Streams.Add (freshStreamData);
                OpenFiles++;
                MaybeRemoveOldestStreams ();
            }

            // We're about to do file manipulation, so swap to a threadpool thread to avoid hanging the DiskIO loop.
            await new EnsureThreadPool ();

            if (!File.Exists (file.FullPath)) {
                if (Path.GetDirectoryName (file.FullPath) is string parentDirectory)
                    Directory.CreateDirectory (parentDirectory);
            }
            freshStreamData.Stream = new RandomFileReaderWriter (file.FullPath, file.Length, FileMode.OpenOrCreate, access, FileShare.ReadWrite | FileShare.Delete);
            return (freshStreamData.Stream, freshStreamDataReleaser);
        }

        public void Dispose ()
        {
            foreach (var stream in Streams.Values) {
                foreach (var v in stream.Streams)
                    v.Stream?.Dispose ();
            }

            Streams.Clear ();
            OpenFiles = 0;
        }

        void MaybeRemoveOldestStreams ()
        {
            while (MaximumOpenFiles != 0 && OpenFiles > MaximumOpenFiles) {
                AllStreams? oldestAllStreams = null;
                StreamData? oldestStream = null;
                ITorrentManagerFile? oldestKey = null;

                foreach (var keypair in Streams) {
                    foreach (var stream in keypair.Value.Streams) {
                        if (oldestStream == null || oldestStream.LastUsedStamp > stream.LastUsedStamp) {
                            oldestStream = stream;
                            oldestKey = keypair.Key;
                            oldestAllStreams = keypair.Value;
                        }
                    }
                }

                if (oldestAllStreams != null && oldestStream != null && oldestKey != null) {
                    OpenFiles--;
                    oldestAllStreams.Streams.Remove (oldestStream);
                    if (oldestAllStreams.Streams.Count == 0)
                        Streams.Remove (oldestKey);
                    AsyncDispose (oldestStream);
                }
            }
        }

        async void AsyncDispose(StreamData streamData)
        {
            using (await streamData.Locker.EnterAsync ().ConfigureAwait (false))
                streamData.Stream?.Dispose ();
        }

        [Conditional ("DEBUG")]
        void ThrowIfNoSyncContext ()
        {
            if (SynchronizationContext.Current is null)
                throw new InvalidOperationException ();
        }
    }
}
