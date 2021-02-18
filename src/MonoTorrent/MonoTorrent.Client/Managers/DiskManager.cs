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
            public int NextOffsetToHash;
            public ReusableExclusiveSemaphore Locker;

            public IncrementalHashData ()
            {
                Hasher = HashAlgoFactory.SHA1 ();
                Locker = new ReusableExclusiveSemaphore ();
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
            public readonly BlockInfo request;
            public readonly byte[] buffer;
            public readonly ReusableTaskCompletionSource<bool> tcs;
            public readonly bool preferSkipCache;

            public BufferedIO (ITorrentData manager, BlockInfo request, byte[] buffer, bool preferSkipCache, ReusableTaskCompletionSource<bool> tcs)
            {
                this.manager = manager;
                this.request = request;
                this.buffer = buffer;
                this.preferSkipCache = preferSkipCache;
                this.tcs = tcs;
            }
        }

        static readonly MainLoop IOLoop = new MainLoop ("Disk IO");

        // These are fields so we can use threadsafe Interlocked operations to add/subtract.
        int pendingWriteBytes;
        int pendingReadBytes;

        /// <summary>
        /// Size of the memory cache in bytes.
        /// </summary>
        public long CacheBytesUsed => Cache.CacheUsed;

        /// <summary>
        /// True if the object has been disposed.
        /// </summary>
        bool Disposed { get; set; }

        /// <summary>
        /// The number of bytes pending being read as the <see cref="EngineSettings.MaximumDiskReadRate"/> rate limit is being exceeded.
        /// </summary>
        public int PendingReadBytes => pendingReadBytes;

        /// <summary>
        /// The number of bytes pending being written as the <see cref="EngineSettings.MaximumDiskWriteRate"/> rate limit is being exceeded.
        /// </summary>
        public int PendingWriteBytes => pendingWriteBytes;

        /// <summary>
        /// Limits how fast data is read from the disk.
        /// </summary>
        RateLimiter ReadLimiter { get; }

        /// <summary>
        /// Read requests which have been queued because the <see cref="EngineSettings.MaximumDiskReadRate"/> limit has been exceeded.
        /// </summary>
        Queue<BufferedIO> ReadQueue { get; }

        /// <summary>
        /// The amount of data, in bytes, being read per second.
        /// </summary>
        public long ReadRate => Cache.ReadMonitor.Rate;

        /// <summary>
        /// The settings object passed to the ClientEngine, used to get the current read/write limits.
        /// </summary>
        EngineSettings Settings { get; set; }

        /// <summary>
        /// Limits how fast data is written to the disk.
        /// </summary>
        RateLimiter WriteLimiter { get; }

        /// <summary>
        /// Read requests which have been queued because the <see cref="EngineSettings.MaximumDiskWriteRate"/> limit has been exceeded.
        /// </summary>
        Queue<BufferedIO> WriteQueue { get; }

        /// <summary>
        /// The amount of data, in bytes, being written per second.
        /// </summary>
        public long WriteRate => Cache.WriteMonitor.Rate;

        /// <summary>
        /// Total bytes read from the cache.
        /// </summary>
        public long TotalCacheBytesRead => Cache.CacheHits;

        /// <summary>
        /// The total bytes which have been read. Excludes bytes read from the cache.
        /// </summary>
        public long TotalBytesRead => Cache.ReadMonitor.Total;

        /// <summary>
        /// The total number of bytes which have been written. Excludes bytes written to the cache.
        /// </summary>
        public long TotalBytesWritten => Cache.WriteMonitor.Total;

        ValueStopwatch UpdateTimer;

        /// <summary>
        /// The piece writer used to read/write data
        /// </summary>
        MemoryCache Cache { get; }

        internal DiskManager (EngineSettings settings, IPieceWriter writer = null)
        {
            ReadLimiter = new RateLimiter ();
            ReadQueue = new Queue<BufferedIO> ();

            WriteLimiter = new RateLimiter ();
            WriteQueue = new Queue<BufferedIO> ();

            UpdateTimer = ValueStopwatch.StartNew ();

            Settings = settings ?? throw new ArgumentNullException (nameof (settings));

            writer ??= new DiskWriter (settings.MaximumOpenFiles);
            Cache = new MemoryCache (settings.DiskCacheBytes, writer);
        }

        internal void ChangePieceWriter (IPieceWriter writer)
        {
            Cache.Writer = writer;
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
            Cache.Dispose ();
        }

        internal async Task<bool> CheckFileExistsAsync (ITorrentFileInfo file)
        {
            await IOLoop;

            return await Cache.Writer.ExistsAsync (file).ConfigureAwait (false);
        }

        internal async Task<bool> CheckAnyFilesExistAsync (ITorrentData manager)
        {
            await IOLoop;

            for (int i = 0; i < manager.Files.Count; i++)
                if (await Cache.Writer.ExistsAsync (manager.Files[i]).ConfigureAwait (false))
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
                // Immediately remove it from the dictionary so another thread writing data to using `WriteAsync` can't try to use it
                IncrementalHashes.Remove (ValueTuple.Create (manager, pieceIndex));

                // We request the blocks for most pieces sequentially, and most (all?) torrent clients
                // will process requests in the order they have been received. This means we can optimise
                // hashing a received piece by hashing each block as it arrives. If blocks arrive out of order then
                // we'll compute the final hash by reading the data from disk.
                if (incrementalHash.NextOffsetToHash == manager.PieceIndexToByteOffset (pieceIndex + 1)
                 || incrementalHash.NextOffsetToHash == manager.Size) {
                    byte[] result = incrementalHash.Hasher.Hash;
                    IncrementalHashCache.Enqueue (incrementalHash);
                    return result;
                }
            } else {
                // If we have no partial hash data for this piece we could be doing a full
                // hash check, so let's create a IncrementalHashData for our piece!
                incrementalHash = IncrementalHashCache.Dequeue ();
            }

            // We can store up to 4MB of pieces in an in-memory queue so that, when we're rate limited
            // we can process the queue in-order. When we try to hash a piece we need to make sure
            // that in-memory cache is written to the PieceWriter before we try to Read the data back
            // to hash it.
            if (WriteQueue.Count > 0)
                await WaitForPendingWrites ();

            using var releaser = await incrementalHash.Locker.EnterAsync ();
            // Note that 'startOffset' may not be the very start of the piece if we have a partial hash.
            int startOffset = incrementalHash.NextOffsetToHash;
            int endOffset = (int) Math.Min (manager.Size - manager.PieceIndexToByteOffset (pieceIndex), manager.PieceLength);
            using (BufferPool.Rent (Piece.BlockSize, out byte[] hashBuffer)) {
                try {
                    SHA1 hasher = incrementalHash.Hasher;

                    while (startOffset != endOffset) {
                        int count = (int) Math.Min (Piece.BlockSize, endOffset - startOffset);
                        if (!await ReadAsync (manager, new BlockInfo (pieceIndex, startOffset, count), hashBuffer).ConfigureAwait (false))
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

        async ReusableTask<bool> WaitForPendingWrites ()
        {
            var tcs = new ReusableTaskCompletionSource<bool> ();
            WriteQueue.Enqueue (new BufferedIO (null, default, null, false, tcs));
            await ProcessBufferedIOAsync ();
            return await tcs.Task;
        }

        internal async Task CloseFilesAsync (ITorrentData manager)
        {
            await IOLoop;

            // Process all pending reads/writes then close any open streams
            await ProcessBufferedIOAsync (true);
            foreach (var file in manager.Files)
                await Cache.Writer.CloseAsync (file);
        }

        /// <summary>
        /// Iterates over every file in this torrent and flushes any pending data to disk. Typically a
        /// <see cref="TorrentManager"/> will be passed to this method.
        /// </summary>
        /// <param name="manager">The torrent containing the files to flush</param>
        /// <returns></returns>
        public Task FlushAsync (ITorrentData manager)
        {
            return FlushAsync (manager, 0, manager.PieceCount () - 1);
        }

        /// <summary>
        /// Iterates over every file in this torrent which is contains data from the specified piece and
        /// flushes that file to disk. Typically a <see cref="TorrentManager"/> will be passed to this method.
        /// </summary>
        /// <param name="manager">The torrent containing the files to flush</param>
        /// <param name="startIndex">The first index of the piece to flush.</param>
        /// <param name="endIndex">The final index of the piece to flush.</param>
        /// <returns></returns>
        public async Task FlushAsync (ITorrentData manager, int startIndex, int endIndex)
        {
            if (manager is null)
                throw new ArgumentNullException (nameof (manager));
            await IOLoop;

            if (WriteQueue.Count > 0)
                await WaitForPendingWrites ();

            var firstFile = IPieceWriterExtensions.FindFileByPieceIndex (manager.Files, startIndex);
            for (int i = firstFile; i < manager.Files.Count; i ++) {
                if (manager.Files[i].StartPieceIndex <= endIndex)
                    await Cache.Writer.FlushAsync (manager.Files[i]);
                else
                    break;
            }
        }

        internal async Task MoveFileAsync (TorrentFileInfo file, string newPath)
        {
            await IOLoop;

            newPath = Path.GetFullPath (newPath);
            await Cache.Writer.MoveAsync (file, newPath, false);
            file.FullPath = newPath;
        }

        internal async Task MoveFilesAsync (ITorrentData manager, string newRoot, bool overwrite)
        {
            await IOLoop;

            foreach (TorrentFileInfo file in manager.Files) {
                string newPath = Path.Combine (newRoot, file.Path);
                if (await Cache.Writer.ExistsAsync (file)) {
                    await Cache.Writer.MoveAsync (file, newPath, overwrite);
                }
                file.FullPath = newPath;
            }
        }

        internal async ReusableTask<bool> ReadAsync (ITorrentData manager, BlockInfo request, byte[] buffer)
        {
            Interlocked.Add (ref pendingReadBytes, request.RequestLength);

            await IOLoop;

            try {
                if (ReadLimiter.TryProcess (request.RequestLength)) {
                    return await Cache.ReadAsync (manager, request, buffer).ConfigureAwait (false) == request.RequestLength;
                } else {
                    var tcs = new ReusableTaskCompletionSource<bool> ();
                    ReadQueue.Enqueue (new BufferedIO (manager, request, buffer, false, tcs));
                    return await tcs.Task.ConfigureAwait (false);
                }
            } finally {
                Interlocked.Add (ref pendingReadBytes, -request.RequestLength);
            }
        }

        internal async Task<bool> ReadAsync (ITorrentFileInfo file, long position, byte[] buffer, int offset, int count)
        {
            await IOLoop;
            return await Cache.Writer.ReadAsync (file, position, buffer, offset, count) == count;
        }

        internal async ReusableTask WriteAsync (ITorrentData manager, BlockInfo request, byte[] buffer)
        {
            if (request.RequestLength < 1)
                throw new ArgumentOutOfRangeException (nameof (request.RequestLength), $"Count must be greater than zero, but was {request.RequestLength}.");

            Interlocked.Add (ref pendingWriteBytes, request.RequestLength);

            await IOLoop;

            try {
                int pieceIndex = request.PieceIndex;
                long pieceStart = manager.PieceIndexToByteOffset (pieceIndex);
                long pieceEnd = pieceStart + manager.PieceLength;

                if (!IncrementalHashes.TryGetValue (ValueTuple.Create (manager, pieceIndex), out IncrementalHashData incrementalHash) && request.StartOffset == 0) {
                    incrementalHash = IncrementalHashes[ValueTuple.Create (manager, pieceIndex)] = IncrementalHashCache.Dequeue ();
                }

                ReusableTaskCompletionSource<bool> tcs = null;
                ReusableTask writeTask = default;
                // Don't retain this in a cache if we are about to successfully incrementally hash the piece.
                // We know we won't have to read this block back later.
                bool preferSkipCache = incrementalHash != null && request.StartOffset == incrementalHash.NextOffsetToHash;
                if (WriteLimiter.TryProcess (request.RequestLength)) {
                    writeTask = Cache.WriteAsync (manager, request, buffer, preferSkipCache);
                } else {
                    tcs = new ReusableTaskCompletionSource<bool> ();
                    WriteQueue.Enqueue (new BufferedIO (manager, request, buffer, preferSkipCache, tcs));
                }

                if (incrementalHash != null) {
                    using var releaser = await incrementalHash.Locker.EnterAsync ();
                    // Incremental hashing does not perform proper bounds checking to ensure
                    // that pieces are correctly incrementally hashed even if 'count' is greater
                    // than the PieceLength. This should never happen under normal operation, but
                    // unit tests do it for convenience sometimes. Keep things safe by cancelling
                    // incremental hashing if that occurs.
                    if ((incrementalHash.NextOffsetToHash + request.RequestLength) > pieceEnd) {
                        IncrementalHashes.Remove (ValueTuple.Create (manager, pieceIndex));
                    } else if (incrementalHash.NextOffsetToHash == request.StartOffset) {
                        await MainLoop.SwitchThread ();
                        incrementalHash.Hasher.TransformBlock (buffer, 0, request.RequestLength, buffer, 0);
                        if (incrementalHash.NextOffsetToHash + request.RequestLength == manager.PieceIndexToByteOffset (pieceIndex + 1)
                            || incrementalHash.NextOffsetToHash + request.RequestLength == manager.Size) {
                            incrementalHash.Hasher.TransformFinalBlock (Array.Empty<byte> (), 0, 0);
                        }
                        incrementalHash.NextOffsetToHash += request.RequestLength;
                    }
                }

                if (tcs != null)
                    await tcs.Task.ConfigureAwait (false);
                else {
                    await writeTask.ConfigureAwait (false);
                }
            } finally {
                Interlocked.Add (ref pendingWriteBytes, -request.RequestLength);
            }
        }

        async ReusableTask ProcessBufferedIOAsync (bool force = false)
        {
            await IOLoop;

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

                if (!force && !WriteLimiter.TryProcess (io.request.RequestLength))
                    break;

                io = WriteQueue.Dequeue ();

                try {
                    await Cache.WriteAsync (io.manager, io.request, io.buffer, io.preferSkipCache);
                    io.tcs.SetResult (true);
                } catch (Exception ex) {
                    io.tcs.SetException (ex);
                }
            }

            while (ReadQueue.Count > 0) {
                if (!force && !ReadLimiter.TryProcess (ReadQueue.Peek ().request.RequestLength))
                    break;

                io = ReadQueue.Dequeue ();

                try {
                    bool result = await Cache.ReadAsync (io.manager, io.request, io.buffer) == io.request.RequestLength;
                    io.tcs.SetResult (result);
                } catch (Exception ex) {
                    io.tcs.SetException (ex);
                }
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
            // The tests sometimes set the rate limit to 1kB/sec, then they tick
            // time forwards, and they ensure no data is actually written. If the
            // test fixture has called ReadAsync/WriteAsync, we want to ensure the
            // tick executes *after* they are processed to avoid having a race condition
            // where the tests enqueues things assuming the rate limit is 1kB/sec and so
            // the pieces won't be written, but then they *are* actually written because
            // UpdateChunks is invoked before the IO thread loops.
            // By forcing this to occur on the IO loop for the tests, that race condition
            // is eliminated. In the real world this is a threadsafe update so it's fine!
            await IOLoop;
            await Tick (delta, true);
        }

        ReusableTask Tick (int delta, bool waitForBufferedIO)
        {
            UpdateTimer.Restart ();

            Cache.ReadMonitor.Tick (delta);
            Cache.WriteMonitor.Tick (delta);

            WriteLimiter.UpdateChunks (Settings.MaximumDiskWriteRate, WriteRate);
            ReadLimiter.UpdateChunks (Settings.MaximumDiskReadRate, ReadRate);

            ReusableTask processTask = ProcessBufferedIOAsync ();
            return waitForBufferedIO ? processTask : ReusableTask.CompletedTask;
        }

        internal void UpdateSettings (EngineSettings settings)
        {
            var oldSettings = Settings;
            Settings = settings;

            if (oldSettings.MaximumOpenFiles != settings.MaximumOpenFiles && Cache.Writer is DiskWriter dr) {
                dr.UpdateMaximumOpenFiles (settings.MaximumOpenFiles);
            }

            if (oldSettings.DiskCacheBytes != settings.DiskCacheBytes) {
                Cache.Capacity = settings.DiskCacheBytes;
            }
        }
    }
}
