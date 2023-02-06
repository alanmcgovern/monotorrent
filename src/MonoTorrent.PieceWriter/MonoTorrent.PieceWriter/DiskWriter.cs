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

using Microsoft.Win32.SafeHandles;

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

        class AllStreams : IDisposable
        {
            public ReusableSemaphore Locker = new ReusableSemaphore (1);
            public List<StreamData> Streams = new List<StreamData> ();

#if NET6_0_OR_GREATER
            public SafeFileHandle? ReadHandle { get; internal set; }
            public SafeFileHandle? ReadWriteHandle { get; internal set; }
#endif
            public void Dispose ()
            {
#if NET6_0_OR_GREATER
                ReadHandle?.Dispose ();
                ReadWriteHandle?.Dispose ();
#endif
            }
        }

        class StreamData
        {
            public ReusableSemaphore Locker = new ReusableSemaphore (1);
            public long LastUsedStamp = Stopwatch.GetTimestamp ();
            public IFileReaderWriter? Stream;

            public StreamData ()
            {
            }

            public StreamData (IFileReaderWriter stream)
                => Stream = stream;
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
            Limiter = new ReusableSemaphore (maxOpenFiles);
            Streams = new Dictionary<ITorrentManagerFile, AllStreams> ();
        }

        public async ReusableTask CloseAsync (ITorrentManagerFile file)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (Streams.TryGetValue (file, out AllStreams? allStreams)) {
                    Streams.Remove (file);

                using (await allStreams.Locker.EnterAsync ()) {
                    await CloseAllAsync (allStreams);
                    allStreams.Dispose ();
                }
            }
        }

        async ReusableTask CloseAllAsync (AllStreams allStreams)
        {
            foreach (var data in allStreams.Streams) {
                using (await data.Locker.EnterAsync ()) {
                    data.Stream?.Dispose ();
                    OpenFiles--;
                }
            }
            allStreams.Streams.Clear ();
        }

        public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            return ReusableTask.FromResult (File.Exists (file.FullPath));
        }

        public async ReusableTask FlushAsync (ITorrentManagerFile file)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (Streams.TryGetValue (file, out AllStreams? allStreams)) {
                using var releaser = await allStreams.Locker.EnterAsync ();
                foreach (var data in allStreams.Streams) {
                    using (await data.Locker.EnterAsync ()) {
                        if (!(data.Stream is null))
                            await data.Stream.FlushAsync ();
                    }
                }
            }
        }

        public async ReusableTask MoveAsync (ITorrentManagerFile file, string newPath, bool overwrite)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (!Streams.TryGetValue (file, out AllStreams? data))
                Streams[file] = data = new AllStreams ();

            using var releaser = await data.Locker.EnterAsync ();
            await CloseAllAsync (data).ConfigureAwait (false);

            await new ThreadSwitcher ();
            if (File.Exists (file.FullPath)) {
                if (overwrite)
                    File.Delete (newPath);

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
                (var writer, var releaser) = await GetOrCreateStreamAsync (file, FileAccess.Read).ConfigureAwait (false);
                using (releaser)
                    if (writer != null)
                        return await writer.ReadAsync (buffer, offset).ConfigureAwait (false);
                return 0;
            }
        }

        public async ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            if (file is null)
                throw new ArgumentNullException (nameof (file));

            if (offset < 0 || offset + buffer.Length > file.Length)
                throw new ArgumentOutOfRangeException (nameof (offset));

            using (await Limiter.EnterAsync ()) {
                (var writer, var releaser) = await GetOrCreateStreamAsync (file, FileAccess.ReadWrite).ConfigureAwait (false);
                using (releaser)
                    if (writer != null)
                        await writer.WriteAsync (buffer, offset).ConfigureAwait (false);
            }
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            Limiter.ChangeCount (maximumOpenFiles);
            return ReusableTask.CompletedTask;
        }

        internal async ReusableTask<(IFileReaderWriter, ReusableSemaphore.Releaser)> GetOrCreateStreamAsync (ITorrentManagerFile file, FileAccess access)
        {
            if (!Streams.TryGetValue (file, out AllStreams? allStreams))
                allStreams = Streams[file] = new AllStreams ();

            StreamData data;
            ReusableSemaphore.Releaser dataReleaser;
            using (var releaser = await allStreams.Locker.EnterAsync ()) {
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
                // The actual `Stream` object will be created later, after the lock is dropped.
                data = new StreamData ();
                dataReleaser = await data.Locker.EnterAsync ();

                allStreams.Streams.Add (data);
                OpenFiles++;
                MaybeRemoveOldestStreams ();
            }

            // The next few lines are disk IO heavy - so explictly transfer to a background thread for this!
            await new ThreadSwitcher ();

            // If multiple concurrent write requests are made to a file which does not yet exist on-disk,
            // serialize access so the sparse file can be safely created before anything actually opens
            // the file.
            try {
                lock (allStreams) {
                    if (!File.Exists (file.FullPath)) {
                        if (Path.GetDirectoryName (file.FullPath) is string parentDirectory)
                            Directory.CreateDirectory (parentDirectory);
                        NtfsSparseFile.CreateSparse (file.FullPath, file.Length);
                    } else if (access.HasFlag (FileAccess.Write)) {
                        FileReaderWriterHelper.MaybeTruncate (file.FullPath, file.Length);
                    }
                }
#if NET6_0_OR_GREATER
                if (access == FileAccess.Read) {
                    if (allStreams.ReadHandle is null)
                        allStreams.ReadHandle = File.OpenHandle (file.FullPath, FileMode.OpenOrCreate, access, FileShare.ReadWrite, FileOptions.None);
                    data.Stream = new RandomFileReaderWriter (allStreams.ReadHandle, file.Length, access);
                }
                else if (access == FileAccess.ReadWrite) {
                    if (allStreams.ReadWriteHandle is null)
                        allStreams.ReadWriteHandle = File.OpenHandle (file.FullPath, FileMode.OpenOrCreate, access, FileShare.ReadWrite, FileOptions.None);
                    data.Stream = new RandomFileReaderWriter (allStreams.ReadWriteHandle, file.Length, access);
                } else {
                    throw new NotSupportedException ();
                }
#else
                data.Stream = new RandomFileReaderWriter (file.FullPath, file.Length, FileMode.OpenOrCreate, access, FileShare.ReadWrite);
#endif
                return (data.Stream, dataReleaser);
            } catch {
                using var safetyRelease = await allStreams.Locker.EnterAsync ();
                allStreams.Streams.Remove (data);
                dataReleaser.Dispose ();
                throw;
            }
        }

        public void Dispose ()
        {
            foreach (var stream in Streams.Values) {
                foreach (var v in stream.Streams)
                    v.Stream?.Dispose ();
                stream.Dispose ();
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
                    if (oldestAllStreams.Streams.Count == 0) {
                        Streams.Remove (oldestKey);
                        AsyncDispose (oldestStream, oldestAllStreams);
                    } else {
                        AsyncDispose (oldestStream, null);
                    }
                }
            }
        }

        async void AsyncDispose(StreamData streamData, AllStreams? allStreams)
        {
            using (await streamData.Locker.EnterAsync ().ConfigureAwait (false))
                streamData.Stream?.Dispose ();

            allStreams?.Dispose ();
        }
    }
}
