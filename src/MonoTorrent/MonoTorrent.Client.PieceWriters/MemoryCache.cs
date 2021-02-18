//
// MemoryWriter.cs
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
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    partial class MemoryCache : IBlockCache
    {
        struct CachedBlock
        {
            public BlockInfo Block;
            public byte[] Buffer => BufferReleaser.Buffer.Data;
            public ByteBufferPool.Releaser BufferReleaser;
            public ITorrentData Torrent;
        }

        long cacheHits;
        long cacheMisses;
        long cacheUsed;

        /// <summary>
        /// The number of bytes which were read from the cache when fulfilling a Read request.
        /// </summary>
        public long CacheHits => cacheHits;

        /// <summary>
        /// The number of bytes which could not be read from the cache when fulfilling a Read request.
        /// </summary>
        public long CacheMisses => cacheMisses;

        /// <summary>
        /// The number of bytes currently used by the cache.
        /// </summary>
        public long CacheUsed => cacheUsed;

        /// <summary>
        /// The blocks which have been cached in memory
        /// </summary>
        List<CachedBlock> CachedBlocks { get; }

        /// <summary>
        /// The size of the in memory cache, in bytes.
        /// </summary>
        public int Capacity { get; set; }

        internal SpeedMonitor ReadMonitor { get; }

        internal SpeedMonitor WriteMonitor { get; }

        internal IPieceWriter Writer { get; set; }

        internal MemoryCache (int capacity, IPieceWriter writer)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException (nameof (capacity));

            Capacity = capacity;
            Writer = writer ?? throw new ArgumentNullException (nameof (writer));

            CachedBlocks = new List<CachedBlock> ();
            ReadMonitor = new SpeedMonitor ();
            WriteMonitor = new SpeedMonitor ();
        }

        public async ReusableTask<int> ReadAsync (ITorrentData torrent, BlockInfo block, byte[] buffer)
        {
            if (torrent == null)
                throw new ArgumentNullException (nameof (torrent));
            if (buffer == null)
                throw new ArgumentNullException (nameof (buffer));

            for (int i = 0; i < CachedBlocks.Count; i++) {
                var cached = CachedBlocks[i];
                if (cached.Block != block)
                    continue;
                CachedBlocks.RemoveAt (i);
                Interlocked.Add (ref cacheUsed, -block.RequestLength);

                using (cached.BufferReleaser) {
                    var asyncWrite = WriteToFilesAsync (torrent, block, cached.Buffer);
                    Buffer.BlockCopy (cached.Buffer, 0, buffer, 0, block.RequestLength);
                    Interlocked.Add (ref cacheHits, block.RequestLength);
                    await asyncWrite.ConfigureAwait (false);
                    return block.RequestLength;
                }
            }

            Interlocked.Add (ref cacheMisses, block.RequestLength);
            return await ReadFromFilesAsync (torrent, block, buffer).ConfigureAwait (false);
        }

        public async ReusableTask WriteAsync (ITorrentData torrent, BlockInfo block, byte[] buffer, bool preferSkipCache)
        {
            if (preferSkipCache || Capacity < block.RequestLength) {
                await WriteToFilesAsync (torrent, block, buffer);
            } else {
                if (CacheUsed > (Capacity - block.RequestLength)) {
                    var cached = CachedBlocks[0];
                    CachedBlocks.RemoveAt (0);
                    Interlocked.Add (ref cacheUsed, -block.RequestLength);

                    using (cached.BufferReleaser)
                        await WriteToFilesAsync (cached.Torrent, cached.Block, cached.Buffer);
                }

                CachedBlock? cache = null;
                for (int i = 0; i < CachedBlocks.Count && !cache.HasValue; i++) {
                    if (CachedBlocks[i].Block == block)
                        cache = CachedBlocks[i];
                }

                if (!cache.HasValue) {
                    cache = new CachedBlock {
                        Block = block,
                        BufferReleaser = DiskManager.BufferPool.Rent (block.RequestLength, out byte[] _),
                        Torrent = torrent,
                    };
                    CachedBlocks.Add (cache.Value);
                    Interlocked.Add (ref cacheUsed, block.RequestLength);
                }
                Buffer.BlockCopy (buffer, 0, cache.Value.Buffer, 0, block.RequestLength);
            }
        }

        ReusableTask<int> ReadFromFilesAsync (ITorrentData torrent, BlockInfo block, byte[] buffer)
        {
            ReadMonitor.AddDelta (block.RequestLength);
            return Writer.ReadFromFilesAsync (torrent, block, buffer);
        }

        ReusableTask WriteToFilesAsync (ITorrentData torrent, BlockInfo block, byte[] buffer)
        {
            WriteMonitor.AddDelta (block.RequestLength);
            return Writer.WriteToFilesAsync (torrent, block, buffer);
        }

        public void Dispose ()
        {
            Debug.Assert (CachedBlocks.Count == 0, "MemoryWriter should have been flushed before being disposed");
            Writer.Dispose ();
        }
    }
}
