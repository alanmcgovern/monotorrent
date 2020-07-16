//
// DiskManager.cs
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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client.RateLimiters;

using ReusableTasks;

namespace MonoTorrent.Client
{
    public class DiskManager : IDisposable
    {
        internal static ByteBufferPool BufferPool { get; } = new ByteBufferPool ();

        static readonly ICache<IncrementalHashData> IncrementalHashCache = new Cache<IncrementalHashData> (true);

        readonly Dictionary<ValueTuple<ITorrentData, int>, IncrementalHashData> IncrementalHashes = new Dictionary<ValueTuple<ITorrentData, int>, IncrementalHashData> ();

        class IncrementalHashData : ICacheable
        {
            public readonly SHA1 Hasher;
            public long NextOffsetToHash;

            public IncrementalHashData ()
            {
                Hasher = HashAlgoFactory.SHA1 ();
                Initialise ();
            }

            public void Initialise ()
            {
                Hasher.Initialize ();
                NextOffsetToHash = 0;
            }
        }

        struct BufferedIO
        {
            public readonly ITorrentData manager;
            public readonly long offset;
            public readonly byte[] buffer;
            public readonly int count;
            public readonly ReusableTaskCompletionSource<bool> tcs;

            public BufferedIO (ITorrentData manager, long offset, byte[] buffer, int count, ReusableTaskCompletionSource<bool> tcs)
            {
                this.manager = manager;
                this.offset = offset;
                this.buffer = buffer;
                this.count = count;
                this.tcs = tcs;
            }
        }

        static readonly MainLoop IOLoop = new MainLoop ("Disk IO");

        // These are fields so we can use threadsafe Interlocked operations to add/subtract.
        int pendingWrites;
        int pendingReads;

        /// <summary>
        /// True if the object has been disposed.
        /// </summary>
        bool Disposed { get; set; }

        /// <summary>
        /// The number of bytes which are currently cached in memory, pending writing.
        /// </summary>
        public int PendingReads => pendingReads;

        /// <summary>
        /// The number of bytes which are currently cached in memory, pending writing.
        /// </summary>
        public int PendingWrites => pendingWrites;

        /// <summary>
        /// Limits how fast data is read from the disk.
        /// </summary>
        internal RateLimiter ReadLimiter { get; }

        /// <summary>
        /// Tracks how fast the disk is being read from.
        /// </summary>
        SpeedMonitor ReadMonitor { get; }

        /// <summary>
        /// Read requests which have been queued because the <see cref="EngineSettings.MaximumDiskReadRate"/> limit has been exceeded.
        /// </summary>
        Queue<BufferedIO> ReadQueue { get; }

        /// <summary>
        /// The amount of data, in bytes, being read per second.
        /// </summary>
        public long ReadRate => ReadMonitor.Rate;

        /// <summary>
        /// The settings object passed to the ClientEngine, used to get the current read/write limits.
        /// </summary>
        EngineSettings Settings { get; }

        /// <summary>
        /// Limits how fast data is written to the disk.
        /// </summary>
        internal RateLimiter WriteLimiter { get; }

        /// <summary>
        /// Tracks how fast the disk is being written to.
        /// </summary>
        SpeedMonitor WriteMonitor { get; }

        /// <summary>
        /// Read requests which have been queued because the <see cref="EngineSettings.MaximumDiskWriteRate"/> limit has been exceeded.
        /// </summary>
        Queue<BufferedIO> WriteQueue { get; }

        /// <summary>
        /// The amount of data, in bytes, being written per second.
        /// </summary>
        public long WriteRate => WriteMonitor.Rate;

        /// <summary>
        /// The total number of bytes which have been read.
        /// </summary>
        public long TotalRead => ReadMonitor.Total;

        /// <summary>
        /// The total number of bytes which have been written.
        /// </summary>
        public long TotalWritten => WriteMonitor.Total;

        ValueStopwatch UpdateTimer;

        /// <summary>
        /// The piece writer used to read/write data
        /// </summary>
        internal IPieceWriter Writer { get; set; }

        internal DiskManager (EngineSettings settings, IPieceWriter writer)
        {
            ReadLimiter = new RateLimiter ();
            ReadMonitor = new SpeedMonitor ();
            ReadQueue = new Queue<BufferedIO> ();

            WriteLimiter = new RateLimiter ();
            WriteMonitor = new SpeedMonitor ();
            WriteQueue = new Queue<BufferedIO> ();

            UpdateTimer = ValueStopwatch.StartNew ();

            Settings = settings ?? throw new ArgumentNullException (nameof (settings));
            Writer = writer ?? throw new ArgumentNullException (nameof (writer));
        }

        void IDisposable.Dispose ()
        {
            Dispose ();
        }

        internal void Dispose ()
        {
            if (Disposed)
                return;

            Debug.Assert (ReadQueue.Count == 0, "The read queue should be cancelled");
            Debug.Assert (WriteQueue.Count == 0, "The write queue should be flushed before disposing");
            Disposed = true;
            Writer.Dispose ();
        }

        internal async Task<bool> CheckFileExistsAsync (ITorrentFileInfo file)
        {
            await IOLoop;

            return await Writer.ExistsAsync (file).ConfigureAwait (false);
        }

        internal async Task<bool> CheckAnyFilesExistAsync (ITorrentData manager)
        {
            await IOLoop;

            for (int i = 0; i < manager.Files.Count; i++)
                if (await Writer.ExistsAsync (manager.Files[i]).ConfigureAwait (false))
                    return true;
            return false;
        }

        internal Func<ITorrentData, int, byte[]> GetHashAsyncOverride;

        internal async ReusableTask<byte[]> GetHashAsync (ITorrentData manager, int pieceIndex)
        {
            if (GetHashAsyncOverride != null)
                return GetHashAsyncOverride (manager, pieceIndex);

            await IOLoop;

            if (IncrementalHashes.TryGetValue (ValueTuple.Create (manager, pieceIndex), out IncrementalHashData incrementalHash)) {
                // We request the blocks for most pieces sequentially, and most (all?) torrent clients
                // will process requests in the order they have been received. This means we can optimise
                // hashing a received piece by hashing each block as it arrives. If blocks arrive out of order then
                // we'll compute the final hash by reading the data from disk.
                if (incrementalHash.NextOffsetToHash == (long) manager.PieceLength * (pieceIndex + 1)
                 || incrementalHash.NextOffsetToHash == manager.Size) {
                    byte[] result = incrementalHash.Hasher.Hash;
                    IncrementalHashCache.Enqueue (incrementalHash);
                    IncrementalHashes.Remove (ValueTuple.Create (manager, pieceIndex));
                    return result;
                }
            } else {
                // If we have no partial hash data for this piece we could be doing a full
                // hash check, so let's create a IncrementalHashData for our piece!
                incrementalHash = IncrementalHashCache.Dequeue ();
                incrementalHash.NextOffsetToHash = (long) manager.PieceLength * pieceIndex;
            }

            // We can store up to 4MB of pieces in an in-memory queue so that, when we're rate limited
            // we can process the queue in-order. When we try to hash a piece we need to make sure
            // that in-memory cache is written to the PieceWriter before we try to Read the data back
            // to hash it.
            if (WriteQueue.Count > 0)
                await WaitForPendingWrites ();

            // Note that 'startOffset' may not be the very start of the piece if we have a partial hash.
            long startOffset = incrementalHash.NextOffsetToHash;
            long endOffset = Math.Min ((long) manager.PieceLength * (pieceIndex + 1), manager.Size);

            using (DiskManager.BufferPool.Rent (Piece.BlockSize, out byte[] hashBuffer)) {
                try {
                    SHA1 hasher = incrementalHash.Hasher;

                    while (startOffset != endOffset) {
                        int count = (int) Math.Min (Piece.BlockSize, endOffset - startOffset);
                        if (!await ReadAsync (manager, startOffset, hashBuffer, count).ConfigureAwait (false))
                            return null;
                        startOffset += count;
                        hasher.TransformBlock (hashBuffer, 0, count, hashBuffer, 0);
                    }

                    hasher.TransformFinalBlock (hashBuffer, 0, 0);
                    return hasher.Hash;
                } finally {
                    await IOLoop;
                    IncrementalHashCache.Enqueue (incrementalHash);
                    IncrementalHashes.Remove (ValueTuple.Create (manager, pieceIndex));
                }
            }
        }

        async ReusableTask WaitForPendingWrites ()
        {
            var tcs = new ReusableTaskCompletionSource<bool> ();
            WriteQueue.Enqueue (new BufferedIO (null, -1, null, -1, tcs));
            await tcs.Task;
        }

        internal async Task CloseFilesAsync (ITorrentData manager)
        {
            await IOLoop;

            // Process all pending reads/writes then close any open streams
            await ProcessBufferedIOAsync (true);
            foreach (var file in manager.Files)
                using (await file.Locker.EnterAsync ())
                    await Writer.CloseAsync (file);
        }

        /// <summary>
        /// Iterates over every file in this torrent and flushes any pending data to disk. Typically a
        /// <see cref="TorrentManager"/> will be passed to this method.
        /// </summary>
        /// <param name="manager">The torrent containing the files to flush</param>
        /// <returns></returns>
        public Task FlushAsync (ITorrentData manager)
        {
            return FlushAsync (manager, -1);
        }

        /// <summary>
        /// Iterates over every file in this torrent which is contains data from the specified piece and
        /// flushes that file to disk. Typically a <see cref="TorrentManager"/> will be passed to this method.
        /// </summary>
        /// <param name="manager">The torrent containing the files to flush</param>
        /// <param name="pieceIndex">The index of the piece to flush.</param>
        /// <returns></returns>
        public async Task FlushAsync (ITorrentData manager, int pieceIndex)
        {
            if (manager is null)
                throw new ArgumentNullException (nameof (manager));
            await IOLoop;

            await WaitForPendingWrites ();
            foreach (var file in manager.Files) {
                if (pieceIndex == -1 || (pieceIndex >= file.StartPieceIndex && pieceIndex <= file.EndPieceIndex))
                    await Writer.FlushAsync (file);
            }
        }

        internal async Task MoveFileAsync (TorrentFileInfo file, string newPath)
        {
            await IOLoop;

            newPath = Path.GetFullPath (newPath);
            await Writer.MoveAsync (file, newPath, false);
            file.FullPath = newPath;
        }

        internal async Task MoveFilesAsync (ITorrentData manager, string newRoot, bool overwrite)
        {
            await IOLoop;

            foreach (TorrentFileInfo file in manager.Files) {
                string newPath = Path.Combine (newRoot, file.Path);
                await Writer.MoveAsync (file, newPath, overwrite);
                file.FullPath = newPath;
            }
        }

        internal async ReusableTask<bool> ReadAsync (ITorrentData manager, long offset, byte[] buffer, int count)
        {
            Interlocked.Add (ref pendingReads, count);

            await IOLoop;

            if (ReadLimiter.TryProcess (count)) {
                try {
                    return await DoReadAsync (manager, offset, buffer, count);
                } finally {
                    Interlocked.Add (ref pendingReads, -count);
                }
            } else {
                var tcs = new ReusableTaskCompletionSource<bool> ();
                ReadQueue.Enqueue (new BufferedIO (manager, offset, buffer, count, tcs));
                return await tcs.Task;
            }
        }

        internal async ReusableTask WriteAsync (ITorrentData manager, long offset, byte[] buffer, int count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException (nameof (count), $"Count must be greater than zero, but was {count}.");

            Interlocked.Add (ref pendingWrites, count);
            await IOLoop;

            int pieceIndex = (int) (offset / manager.PieceLength);
            long pieceStart = (long) pieceIndex * manager.PieceLength;
            long pieceEnd = pieceStart + manager.PieceLength;

            if (!IncrementalHashes.TryGetValue (ValueTuple.Create (manager, pieceIndex), out IncrementalHashData incrementalHash) && offset == pieceStart) {
                incrementalHash = IncrementalHashes[ValueTuple.Create (manager, pieceIndex)] = IncrementalHashCache.Dequeue ();
                incrementalHash.NextOffsetToHash = (long) manager.PieceLength * pieceIndex;
            }

            ReusableTaskCompletionSource<bool> tcs = null;
            ReusableTask writeTask = default;
            if (WriteLimiter.TryProcess (count)) {
                try {
                    writeTask = DoWriteAsync (manager, offset, buffer, count);
                } catch {
                    Interlocked.Add (ref pendingWrites, -count);
                    throw;
                }
            } else {
                tcs = new ReusableTaskCompletionSource<bool> ();
                WriteQueue.Enqueue (new BufferedIO (manager, offset, buffer, count, tcs));
            }

            if (incrementalHash != null) {
                // Incremental hashing does not perform proper bounds checking to ensure
                // that pieces are correctly incrementally hashed even if 'count' is greater
                // than the PieceLength. This should never happen under normal operation, but
                // unit tests do it for convenience sometimes. Keep things safe by cancelling
                // incremental hashing if that occurs.
                if ((incrementalHash.NextOffsetToHash + count) > pieceEnd) {
                    IncrementalHashes.Remove (ValueTuple.Create (manager, pieceIndex));
                } else if (incrementalHash.NextOffsetToHash == offset) {
                    await MainLoop.SwitchThread ();
                    incrementalHash.Hasher.TransformBlock (buffer, 0, count, buffer, 0);
                    if (incrementalHash.NextOffsetToHash + count == (long) manager.PieceLength * (pieceIndex + 1)
                        || incrementalHash.NextOffsetToHash + count == manager.Size) {
                        incrementalHash.Hasher.TransformFinalBlock (Array.Empty<byte> (), 0, 0);
                    }
                    incrementalHash.NextOffsetToHash += count;
                }
            }

            if (tcs != null)
                await tcs.Task.ConfigureAwait (false);
            else {
                try {
                    await writeTask.ConfigureAwait (false);
                } finally {
                    Interlocked.Add (ref pendingWrites, -count);
                }
            }
        }

        async ReusableTask ProcessBufferedIOAsync (bool force = false)
        {
            BufferedIO io;

            while (WriteQueue.Count > 0) {
                io = WriteQueue.Peek ();
                // This means we wanted to wait until all the writes had been flushed
                // before we attempt to generate the hash of a given piece.
                if (io.manager == null && io.buffer == null) {
                    io = WriteQueue.Dequeue ();
                    io.tcs.SetResult (true);
                    continue;
                }

                if (!force && !WriteLimiter.TryProcess (io.count))
                    break;

                io = WriteQueue.Dequeue ();

                try {
                    try {
                        await DoWriteAsync (io.manager, io.offset, io.buffer, io.count);
                    } finally {
                        Interlocked.Add (ref pendingWrites, -io.count);
                    }
                    io.tcs.SetResult (true);
                } catch (Exception ex) {
                    io.tcs.SetException (ex);
                }
            }

            while (ReadQueue.Count > 0) {
                if (!force && !ReadLimiter.TryProcess (ReadQueue.Peek ().count))
                    break;

                io = ReadQueue.Dequeue ();

                try {
                    Interlocked.Add (ref pendingReads, -io.count);
                    bool result = await DoReadAsync (io.manager, io.offset, io.buffer, io.count);
                    io.tcs.SetResult (result);
                } catch (Exception ex) {
                    io.tcs.SetException (ex);
                }
            }
        }

        static readonly Func<ITorrentFileInfo, (long offset, int pieceLength), int> Comparator = (file, offsetAndPieceLength) => {
            // Force these two be longs right at the start so we don't overflow
            // int32s when dealing with large torrents.
            (long offset, long pieceLength) = offsetAndPieceLength;
            var fileStart = (long) file.StartPieceIndex * pieceLength + file.StartPieceOffset;
            var fileEnd = fileStart + file.Length;
            if (offset >= fileStart && offset < fileEnd)
                return 0;
            if (offset >= fileEnd)
                return -1;
            else
                return 1;
        };

        public static int FindFileIndex (IList<ITorrentFileInfo> files, long offset, int pieceLength)
        {
            return files.BinarySearch (Comparator, (offset, pieceLength));
        }

        async ReusableTask<bool> DoReadAsync (ITorrentData manager, long offset, byte[] buffer, int count)
        {
            ReadMonitor.AddDelta (count);

            if (count < 1)
                throw new ArgumentOutOfRangeException (nameof (count), $"Count must be greater than zero, but was {count}.");

            if (offset < 0 || offset + count > manager.Size)
                throw new ArgumentOutOfRangeException (nameof (offset));

            int totalRead = 0;
            var files = manager.Files;
            int i = FindFileIndex (files, offset, manager.PieceLength);
            offset -= (long) files[i].StartPieceIndex * manager.PieceLength + files[i].StartPieceOffset;

            while (totalRead < count) {
                int fileToRead = (int) Math.Min (files[i].Length - offset, count - totalRead);
                fileToRead = Math.Min (fileToRead, Piece.BlockSize);

                if (fileToRead != await ReadFromFileAsync (files[i], offset, buffer, totalRead, fileToRead))
                    return false;

                offset += fileToRead;
                totalRead += fileToRead;
                if (offset >= files[i].Length) {
                    offset = 0;
                    i++;
                }
            }

            return true;
        }

        async ReusableTask<int> ReadFromFileAsync (ITorrentFileInfo torrentFile, long offset, byte[] buffer, int bufferOffset, int count)
        {
            await torrentFile.Locker.WaitAsync ();
            try {
                return await Writer.ReadAsync (torrentFile, offset, buffer, bufferOffset, count);
            } finally {
                torrentFile.Locker.Release ();
            }
        }

        /// <summary>
        /// Attempts to update the rate limits and process pending reads/writes. This method
        /// self-throttles so it only executes if it has been more than 800ms since the previous
        /// run. This ensures estimated read/write rates are a little more accurate overall.
        /// If there are pending reads/writes this method will not block until they are processed.
        /// </summary>
        internal void Tick ()
        {
            int delta = (int) UpdateTimer.ElapsedMilliseconds;
            if (delta > 800)
                Tick (delta, false);
        }

        /// <summary>
        /// Used for unit testing to allow the rate limits to be updated as if the specified
        /// amount of time had actually passed. When executed this method will block until
        /// as many pending reads/writes have been processed as is allowed by the rate limit.
        /// </summary>
        /// <param name="delta">The amount of time, in milliseconds, which has passed</param>
        /// <returns></returns>
        internal async ReusableTask Tick (int delta)
        {
            await IOLoop;
            await Tick (delta, true);
        }

        ReusableTask Tick (int delta, bool waitForBufferedIO)
        {
            UpdateTimer.Restart ();

            ReadMonitor.Tick (delta);
            WriteMonitor.Tick (delta);

            WriteLimiter.UpdateChunks (Settings.MaximumDiskWriteRate, WriteRate);
            ReadLimiter.UpdateChunks (Settings.MaximumDiskReadRate, ReadRate);

            ReusableTask processTask = ProcessBufferedIOAsync ();
            return waitForBufferedIO ? processTask : ReusableTask.CompletedTask;
        }

        async ReusableTask DoWriteAsync (ITorrentData manager, long offset, byte[] buffer, int count)
        {
            WriteMonitor.AddDelta (count);

            if (offset < 0 || offset + count > manager.Size)
                throw new ArgumentOutOfRangeException (nameof (offset));

            int totalWritten = 0;
            var files = manager.Files;
            int i = FindFileIndex (files, offset, manager.PieceLength);
            offset -= (long) files[i].StartPieceIndex * manager.PieceLength + files[i].StartPieceOffset;

            while (totalWritten < count) {
                int fileToWrite = (int) Math.Min (files[i].Length - offset, count - totalWritten);
                fileToWrite = Math.Min (fileToWrite, Piece.BlockSize);

                await files[i].Locker.WaitAsync ();
                try {
                    await Writer.WriteAsync (files[i], offset, buffer, totalWritten, fileToWrite);
                } finally {
                    files[i].Locker.Release ();
                }
                offset += fileToWrite;
                totalWritten += fileToWrite;
                if (offset >= files[i].Length) {
                    offset = 0;
                    i++;
                }
            }
        }
    }
}
