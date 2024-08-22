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
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.RateLimiters;
using MonoTorrent.PieceWriter;

using ReusableTasks;

namespace MonoTorrent.Client
{
    public class DiskManager : IDisposable
    {
        internal static MemoryPool BufferPool { get; } = MemoryPool.Default;

        readonly SpinLocked<Queue<SemaphoreLocked<IncrementalHashData>>> IncrementalHashCache;

        readonly SpinLocked<Dictionary<ValueTuple<ITorrentManagerInfo, int>, SemaphoreLocked<IncrementalHashData>>> IncrementalHashes = SpinLocked.Create (new Dictionary<ValueTuple<ITorrentManagerInfo, int>, SemaphoreLocked<IncrementalHashData>> ());

        class IncrementalHashData : ICacheable
        {
            static readonly byte[] Flusher = new byte[32];

            readonly IncrementalHash SHA1Hasher;

            public int NextOffsetToHash { get; private set; }

            bool HashersNeedFlushing { get; set; }
            public bool PreparedForFirstUse { get; private set; }

            int v2BytesRemaining = 0;
            readonly IncrementalHash SHA256Hasher;
            MemoryPool.Releaser BlockHashesReleaser { get; set; }
            Memory<byte> BlockHashes { get; set; }

            ITorrentManagerInfo? Manager { get; set; }
            int PieceIndex { get; set; }
            bool UseV1 => Manager?.InfoHashes.V1 != null;
            bool UseV2 => Manager?.InfoHashes.V2 != null;

            public IncrementalHashData ()
            {
                SHA1Hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA1);
                SHA256Hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);
                PieceIndex = -1;
            }

            public void Initialise ()
            {
                // If we've used them, reset them.
                if (HashersNeedFlushing) {
                    SHA1Hasher.TryGetHashAndReset (Flusher, out _);
                    SHA256Hasher.TryGetHashAndReset (Flusher, out _);
                    HashersNeedFlushing = false;
                }
                NextOffsetToHash = 0;

                BlockHashesReleaser.Dispose ();
                BlockHashesReleaser = default;
                BlockHashes = default;
                Manager = null;
                PieceIndex = -1;
                PreparedForFirstUse = false;
            }

            public void PrepareForFirstUse (ITorrentManagerInfo manager, int pieceIndex)
            {
                Manager = manager;
                PieceIndex = pieceIndex;
                PreparedForFirstUse = true;

                if (UseV2) {
                    var file = manager.Files[manager.Files.FindFileByPieceIndex (pieceIndex)];
                    Memory<byte> hashes;
                    v2BytesRemaining = manager.TorrentInfo!.BytesPerPieceV2 (pieceIndex);
                    int actualBlocks = (v2BytesRemaining + Constants.BlockSize - 1) / Constants.BlockSize;
                    if (file.StartPieceIndex == file.EndPieceIndex) {
                        var blocks = actualBlocks;
                        if (blocks > 1)
                            blocks = checked((int) BitOps.RoundUpToPowerOf2 (blocks));
                        BlockHashesReleaser = MemoryPool.Default.Rent (blocks * 32, out hashes);
                    } else { 
                        BlockHashesReleaser = MemoryPool.Default.Rent (manager.TorrentInfo!.PieceLength / Constants.BlockSize  * 32, out hashes);
                    }
                    BlockHashes = hashes;
                    BlockHashes.Span.Slice (actualBlocks).Clear ();
                }
            }

            public void AppendData(ReadOnlySpan<byte> buffer)
            {
                HashersNeedFlushing = true;
                // SHA1 hashes need to be computed by hashing the blocks in the piece sequentially
                if (UseV1)
                    SHA1Hasher.AppendData (buffer);

                // SHA256 hashes are hashed blockwise first, and then those block hashes are combined like a merkle tree until
                // we have a piece hash.
                if (UseV2 && v2BytesRemaining > 0) {
                    var tmp = buffer;
                    if (v2BytesRemaining < tmp.Length)
                        tmp = tmp.Slice (0, v2BytesRemaining);
                    SHA256Hasher.AppendData (tmp);
                    v2BytesRemaining -= tmp.Length;
                    if (!SHA256Hasher.TryGetHashAndReset (BlockHashes.Span.Slice ((NextOffsetToHash / Constants.BlockSize) * 32, 32), out _))
                        throw new InvalidOperationException ("Failed to generate SHA256 hash for the provided piece");
                }

                NextOffsetToHash += buffer.Length;
            }

            public bool TryGetHashAndReset (PieceHash dest)
            {
                if (Manager is null)
                    return false;

                if (UseV1 && (!SHA1Hasher.TryGetHashAndReset (dest.V1Hash.Span, out int written) || written != dest.V1Hash.Length))
                    return false;

                if (Manager != null && UseV2) {
                    if (BlockHashes.Length == 32)
                        BlockHashes.Span.CopyTo (dest.V2Hash.Span);
                    // Layer 0 corresponds to 16kB hashes.
                    else if (!MerkleTreeHasher.TryHash (SHA256Hasher, BlockHashes.Span, 0, ReadOnlySpan<byte>.Empty, 0, BlockHashes.Span.Length / 32, dest.V2Hash.Span, out written) || written != dest.V2Hash.Length)
                        return false;
                }
                HashersNeedFlushing = false;
                return true;
            }
        }

        struct BufferedIO
        {
            public readonly ITorrentManagerInfo? manager;
            public readonly BlockInfo request;
            public readonly Memory<byte> buffer;
            public readonly ReusableTaskCompletionSource<bool> tcs;
            public readonly bool preferSkipCache;

            public BufferedIO (ITorrentManagerInfo? manager, BlockInfo request, Memory<byte> buffer, bool preferSkipCache, ReusableTaskCompletionSource<bool> tcs)
            {
                this.manager = manager;
                this.request = request;
                this.buffer = buffer;
                this.preferSkipCache = preferSkipCache;
                this.tcs = tcs;
            }
        }

        static internal readonly MainLoop IOLoop = new MainLoop ("Disk IO");

        // These are fields so we can use threadsafe Interlocked operations to add/subtract.
        int pendingWriteBytes;
        int pendingReadBytes;

        /// <summary>
        /// Size of the memory cache in bytes.
        /// </summary>
        public long CacheBytesUsed => Cache.CacheUsed;

        SpeedMonitor WriterReadMonitor { get; } = new SpeedMonitor ();
        SpeedMonitor WriterWriteMonitor { get; } = new SpeedMonitor ();

        /// <summary>
        /// True if the object has been disposed.
        /// </summary>
        bool Disposed { get; set; }

        Factories Factories { get; }

        public int OpenFiles => Cache.Writer.OpenFiles;
        public int MaximumOpenFiles => Cache.Writer.MaximumOpenFiles;

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
        public long ReadRate => WriterReadMonitor.Rate;

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
        public long WriteRate => WriterWriteMonitor.Rate;

        /// <summary>
        /// Total bytes read from the cache.
        /// </summary>
        public long TotalCacheBytesRead => Cache.CacheHits;

        /// <summary>
        /// The total bytes which have been read. Excludes bytes read from the cache.
        /// </summary>
        public long TotalBytesRead => WriterReadMonitor.Total;

        /// <summary>
        /// The total number of bytes which have been written. Excludes bytes written to the cache.
        /// </summary>
        public long TotalBytesWritten => WriterWriteMonitor.Total;

        ValueStopwatch UpdateTimer;

        /// <summary>
        /// The piece writer used to read/write data
        /// </summary>
        IBlockCache Cache { get; }

        internal DiskManager (EngineSettings settings, Factories factories, IPieceWriter? writer = null)
        {
            ReadLimiter = new RateLimiter ();
            ReadQueue = new Queue<BufferedIO> ();

            WriteLimiter = new RateLimiter ();
            WriteQueue = new Queue<BufferedIO> ();

            UpdateTimer = ValueStopwatch.StartNew ();

            Factories = factories ?? throw new ArgumentNullException (nameof (factories));
            Settings = settings ?? throw new ArgumentNullException (nameof (settings));

            writer ??= factories.CreatePieceWriter (settings.MaximumOpenFiles);
            Cache = factories.CreateBlockCache (writer, settings.DiskCacheBytes, settings.DiskCachePolicy, BufferPool);
            Cache.ReadThroughCache += (o, e) => WriterReadMonitor.AddDelta (e.RequestLength);
            Cache.WrittenThroughCache += (o, e) => WriterWriteMonitor.AddDelta (e.RequestLength);
            IncrementalHashCache = SpinLocked.Create (new Queue<SemaphoreLocked<IncrementalHashData>> (64));
        }

        internal async ReusableTask SetWriterAsync (IPieceWriter writer)
            => await Cache.SetWriterAsync (writer);

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

        internal async Task<bool> CheckFileExistsAsync (ITorrentManagerFile file)
        {
            await IOLoop;

            return await Cache.Writer.ExistsAsync (file).ConfigureAwait (false);
        }

        internal async Task<bool> CheckAnyFilesExistAsync (ITorrentManagerInfo manager)
        {
            await IOLoop;

            for (int i = 0; i < manager.Files.Count; i++)
                if (await Cache.Writer.ExistsAsync (manager.Files[i]).ConfigureAwait (false))
                    return true;
            return false;
        }

        internal Func<ITorrentManagerInfo, int, PieceHash, Task<bool>>? GetHashAsyncOverride;

        internal async ReusableTask<bool> GetHashAsync (ITorrentManagerInfo manager, int pieceIndex, PieceHash dest)
        {
            if (GetHashAsyncOverride != null) {
                return await GetHashAsyncOverride (manager, pieceIndex, dest);
            }

            await IOLoop;

            // We can store up to 4MB of pieces in an in-memory queue so that, when we're rate limited
            // we can process the queue in-order. When we try to hash a piece we need to make sure
            // that in-memory cache is written to the PieceWriter before we try to Read the data back
            // to hash it.
            if (WriteQueue.Count > 0)
                await WaitForPendingWrites ();

            SemaphoreLocked<IncrementalHashData>? incrementalHashBox;
            using (IncrementalHashes.Enter (out var hashes)) {
                if (hashes.TryGetValue (ValueTuple.Create (manager, pieceIndex), out incrementalHashBox)) {
                    // Immediately remove it from the dictionary so another thread writing data to using `WriteAsync` can't try to use it
                    hashes.Remove (ValueTuple.Create (manager, pieceIndex));
                }
            }

            if (incrementalHashBox == null) {
                using (IncrementalHashCache.Enter (out var cache))
                    incrementalHashBox = cache.Count > 0 ? cache.Dequeue () : null;
                incrementalHashBox ??= SemaphoreLocked.Create (new IncrementalHashData ());
            }

            using var releaser = await incrementalHashBox.EnterAsync ();
            var incrementalHash = releaser.Value;

            // We request the blocks for most pieces sequentially, and most (all?) torrent clients
            // will process requests in the order they have been received. This means we can optimise
            // hashing a received piece by hashing each block as it arrives. If blocks arrive out of order then
            // we'll compute the final hash by reading the data from disk.
            if (incrementalHash.NextOffsetToHash == manager.TorrentInfo!.BytesPerPiece (pieceIndex)) {
                if (!incrementalHash.TryGetHashAndReset (dest))
                    throw new NotSupportedException ("Could not generate SHA1 hash for this piece");

                incrementalHash.Initialise ();
                using (IncrementalHashCache.Enter (out var cache))
                    cache.Enqueue (incrementalHashBox);
                return true;
            }

            // If we had no partial hash data, prep this to hash the entire piece
            if (!incrementalHash.PreparedForFirstUse)
                incrementalHash.PrepareForFirstUse (manager, pieceIndex);

            // Note that 'startOffset' may not be the very start of the piece if we have a partial hash.
            int startOffset = incrementalHash.NextOffsetToHash;
            int endOffset = manager.TorrentInfo!.BytesPerPiece (pieceIndex);
            using (BufferPool.Rent (Constants.BlockSize, out Memory<byte> readingBuffer))
            using (BufferPool.Rent (Constants.BlockSize, out Memory<byte> hashingBuffer)) {
                try {
                    int hashingCount = 0;
                    int readingCount = Math.Min (Constants.BlockSize, endOffset - startOffset);
                    var readingTask = ReadAsync (manager, new BlockInfo (pieceIndex, startOffset, readingCount), readingBuffer.Slice (0, readingCount));

                    // Swap to a threadpool thread here so all the incremental hashing happens
                    // off the main IO loop.
                    await new EnsureThreadPool ();
                    while (startOffset != endOffset) {
                        // Wait for the reading task to complete.
                        if (!await readingTask.ConfigureAwait (false)) {
                            return false;
                        } else {
                            // Great success. Increment 'startOffset' by the amount of data we just read
                            startOffset += readingCount;

                            // Now flip the buffers so we can asynchronously read the next block while we hash the current block.
                            (readingBuffer, hashingBuffer) = (hashingBuffer, readingBuffer);
                            (readingCount, hashingCount) = (hashingCount, readingCount);

                            // begin the next read with into the readbuffer
                            readingCount = Math.Min (Constants.BlockSize, endOffset - startOffset);
                            if (readingCount > 0)
                                readingTask = ReadAsync (manager, new BlockInfo (pieceIndex, startOffset, readingCount), readingBuffer.Slice (0, readingCount));
                        }

                        // While the prior async read is completing, hash the data
                        incrementalHash.AppendData (hashingBuffer.Span.Slice (0, hashingCount));
                    }
                    return incrementalHash.TryGetHashAndReset (dest);
                } finally {
                    incrementalHash.Initialise ();
                    using (IncrementalHashCache.Enter (out var cache))
                        cache.Enqueue (incrementalHashBox);
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

        internal async Task CloseFilesAsync (ITorrentManagerInfo manager)
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
        public Task FlushAsync (ITorrentManagerInfo manager)
        {
            return FlushAsync (manager, 0, manager.TorrentInfo!.PieceCount () - 1);
        }

        /// <summary>
        /// Iterates over every file in this torrent which is contains data from the specified piece and
        /// flushes that file to disk. Typically a <see cref="TorrentManager"/> will be passed to this method.
        /// </summary>
        /// <param name="manager">The torrent containing the files to flush</param>
        /// <param name="startIndex">The first index of the piece to flush.</param>
        /// <param name="endIndex">The final index of the piece to flush.</param>
        /// <returns></returns>
        public async Task FlushAsync (ITorrentManagerInfo manager, int startIndex, int endIndex)
        {
            if (manager is null)
                throw new ArgumentNullException (nameof (manager));
            await IOLoop;

            if (WriteQueue.Count > 0)
                await WaitForPendingWrites ();

            var firstFile = manager.Files.FindFileByPieceIndex (startIndex);
            for (int i = firstFile; i < manager.Files.Count; i++) {
                if (manager.Files[i].StartPieceIndex <= endIndex)
                    await Cache.Writer.FlushAsync (manager.Files[i]);
                else
                    break;
            }
        }

        internal Task MoveFileAsync (ITorrentManagerFile file, (string newPath, string downloadComplete, string downloadIncomplete) paths)
            => MoveFileAsync ((TorrentFileInfo) file, paths);

        internal Task MoveFileAsync (TorrentFileInfo file, (string newPath, string downloadComplete, string downloadIncomplete) paths)
            => MoveFileAsync (file, paths, false);

        internal async Task MoveFilesAsync (IList<ITorrentManagerFile> files, string newRoot, bool overwrite)
        {
            foreach (TorrentFileInfo file in files) {
                var paths = TorrentFileInfo.GetNewPaths (Path.GetFullPath (Path.Combine (newRoot, file.Path)), Settings.UsePartialFiles, file.Path == file.DownloadCompleteFullPath);
                await MoveFileAsync (file, paths, overwrite);
            }
        }

        async Task MoveFileAsync (TorrentFileInfo file, (string newPath, string downloadCompletePath, string downloadIncompletePath) paths, bool overwrite)
        {
            await IOLoop;

            if (paths.newPath != file.FullPath && await Cache.Writer.ExistsAsync (file)) {
                await Cache.Writer.MoveAsync (file, paths.newPath, overwrite);
            }
            file.UpdatePaths (paths);
        }

        internal async ReusableTask<bool> ReadAsync (ITorrentManagerInfo manager, BlockInfo request, Memory<byte> buffer)
        {
            Interlocked.Add (ref pendingReadBytes, request.RequestLength);

            await IOLoop;

            try {
                if (ReadLimiter.TryProcess (request.RequestLength)) {
                    return await Cache.ReadAsync (manager, request, buffer).ConfigureAwait (false);
                } else {
                    var tcs = new ReusableTaskCompletionSource<bool> ();
                    ReadQueue.Enqueue (new BufferedIO (manager, request, buffer, false, tcs));
                    return await tcs.Task.ConfigureAwait (false);
                }
            } finally {
                Interlocked.Add (ref pendingReadBytes, -request.RequestLength);
            }
        }

        internal async Task<bool> ReadAsync (ITorrentManagerFile file, long position, Memory<byte> buffer)
        {
            await IOLoop;
            return await Cache.Writer.ReadAsync (file, position, buffer) == buffer.Length;
        }

        internal async ReusableTask WriteAsync (ITorrentManagerInfo manager, BlockInfo request, Memory<byte> buffer)
        {
            if (request.RequestLength < 1)
                throw new ArgumentOutOfRangeException (nameof (request.RequestLength), $"Count must be greater than zero, but was {request.RequestLength}.");

            Interlocked.Add (ref pendingWriteBytes, request.RequestLength);

            await IOLoop;

            try {
                int pieceIndex = request.PieceIndex;
                SemaphoreLocked<IncrementalHashData>? incrementalHashBox = null;
                using (IncrementalHashes.Enter (out var hashes)) {
                    if (!hashes.TryGetValue (ValueTuple.Create (manager, pieceIndex), out incrementalHashBox) && request.StartOffset == 0) {
                        using (IncrementalHashCache.Enter (out var cache))
                            incrementalHashBox = cache.Count > 0 ? cache.Dequeue () : null;

                        incrementalHashBox ??= SemaphoreLocked.Create (new IncrementalHashData ());
                        hashes[ValueTuple.Create (manager, pieceIndex)] = incrementalHashBox;
                    }
                }

                ReusableTaskCompletionSource<bool>? tcs = null;
                ReusableTask writeTask = default;

                using (var accessor = incrementalHashBox == null ? default : await incrementalHashBox.EnterAsync ()) {
                    bool canIncrementallyHash = false;
                    var incrementalHash = accessor.Value;
                    if (incrementalHash != null) {
                        if (incrementalHash.NextOffsetToHash == 0 && request.StartOffset == 0)
                            incrementalHash.PrepareForFirstUse (manager, pieceIndex);

                        if (incrementalHash.NextOffsetToHash < request.StartOffset)
                            await TryIncrementallyHashFromMemory (manager, pieceIndex, incrementalHash);

                        canIncrementallyHash = request.StartOffset == incrementalHash.NextOffsetToHash;
                    }

                    // If we can incrementally hash the data, instruct the cache to write the block straight through
                    // to the IPieceWriter, so it is not stored in the in-memory cache.
                    if (WriteLimiter.TryProcess (request.RequestLength)) {
                        writeTask = Cache.WriteAsync (manager, request, buffer, canIncrementallyHash);
                    } else {
                        tcs = new ReusableTaskCompletionSource<bool> ();
                        WriteQueue.Enqueue (new BufferedIO (manager, request, buffer, canIncrementallyHash, tcs));
                    }

                    if (canIncrementallyHash) {
                        // Yield the thread we're currently on so that the next 'WriteAsync' invocation can begin
                        // to process. If it's for a different piece it will run concurrently with the remainder of
                        // this method.
                        await MainLoop.SwitchToThreadpool ();
                        incrementalHash!.AppendData (buffer.Span.Slice (0, request.RequestLength));
                    }
                }


                if (tcs != null)
                    await tcs.Task.ConfigureAwait (false);
                else
                    await writeTask.ConfigureAwait (false);
            } finally {
                Interlocked.Add (ref pendingWriteBytes, -request.RequestLength);
            }
        }

        async ReusableTask TryIncrementallyHashFromMemory (ITorrentManagerInfo torrent, int pieceIndex, IncrementalHashData incrementalHash)
        {
            var sizeOfPiece = torrent.TorrentInfo!.BytesPerPiece (pieceIndex);
            using var releaser = BufferPool.Rent (Constants.BlockSize, out Memory<byte> buffer);
            while (incrementalHash.NextOffsetToHash < sizeOfPiece) {
                var remaining = Math.Min (Constants.BlockSize, sizeOfPiece - incrementalHash.NextOffsetToHash);
                if (await Cache.ReadFromCacheAsync (torrent, new BlockInfo (pieceIndex, incrementalHash.NextOffsetToHash, remaining), buffer)) {
                    incrementalHash.AppendData (buffer.Span.Slice (0, remaining));
                } else {
                    break;
                }
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
                if (io.manager == null) {
                    io = WriteQueue.Dequeue ();
                    io.tcs.SetResult (true);
                    continue;
                }

                if (!force && !WriteLimiter.TryProcess (io.request.RequestLength))
                    break;

                _ = WriteQueue.Dequeue ();

                try {
                    await Cache.WriteAsync (io.manager, io.request, io.buffer, io.preferSkipCache);
                    io.tcs.SetResult (true);
                } catch (Exception ex) {
                    io.tcs.SetException (ex);
                }
            }

            while (ReadQueue.Count > 0) {
                io = ReadQueue.Peek ();
                if (io.manager == null)
                    continue;

                if (!force && !ReadLimiter.TryProcess (io.request.RequestLength))
                    break;

                _ = ReadQueue.Dequeue ();

                try {
                    bool result = await Cache.ReadAsync (io.manager, io.request, io.buffer);
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

            WriterReadMonitor.Tick (delta);
            WriterWriteMonitor.Tick (delta);

            WriteLimiter.UpdateChunks (Settings.MaximumDiskWriteRate);
            ReadLimiter.UpdateChunks (Settings.MaximumDiskReadRate);

            ReusableTask processTask = ProcessBufferedIOAsync ();
            return waitForBufferedIO ? processTask : ReusableTask.CompletedTask;
        }

        internal async ReusableTask UpdateSettingsAsync (EngineSettings settings)
        {
            await IOLoop;

            var oldSettings = Settings;
            Settings = settings;

            if (oldSettings.MaximumOpenFiles != settings.MaximumOpenFiles) {
                await Cache.Writer.SetMaximumOpenFilesAsync (settings.MaximumOpenFiles);
            }

            if (oldSettings.DiskCacheBytes != settings.DiskCacheBytes) {
                await Cache.SetCapacityAsync (settings.DiskCacheBytes);
            }
        }

        internal async ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions fileCreationOptions)
        {
            await IOLoop;
            return await Cache.Writer.CreateAsync (file, fileCreationOptions);
        }

        internal async ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file)
        {
            await IOLoop;
            return await Cache.Writer.GetLengthAsync (file);
        }

        internal async ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length)
        {
            await IOLoop;
            return await Cache.Writer.SetLengthAsync (file, length);
        }
    }
}
