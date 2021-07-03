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
using System.Threading;

using MonoTorrent.PiecePicking;
using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    partial class MemoryCache : IBlockCache
    {
        readonly struct CachedBlock : IEquatable<CachedBlock>
        {
            public readonly bool Flushing;
            public readonly BlockInfo Block;
            public readonly byte[] Buffer => BufferReleaser.Buffer.Data;
            public readonly ByteBufferPool.Releaser BufferReleaser;

            public CachedBlock (BlockInfo block, ByteBufferPool.Releaser releaser)
                : this (block, releaser, false)
            {

            }

            CachedBlock (BlockInfo block, ByteBufferPool.Releaser releaser, bool flushing)
            {
                Block = block;
                BufferReleaser = releaser;
                Flushing = flushing;
            }

            public static bool operator == (CachedBlock left, CachedBlock right)
                => left.Equals (right);

            public static bool operator != (CachedBlock left, CachedBlock right)
                => !left.Equals (right);

            public CachedBlock SetFlushing ()
                => new CachedBlock (Block, BufferReleaser, true);

            public override bool Equals (object obj)
                => obj is CachedBlock block && Equals (block);

            public bool Equals (CachedBlock other)
                => other.Block == Block;

            public override int GetHashCode ()
                => Block.GetHashCode ();
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
        Dictionary<ITorrentData, List<CachedBlock>> CachedBlocks { get; }

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

            CachedBlocks = new Dictionary<ITorrentData, List<CachedBlock>> ();
            ReadMonitor = new SpeedMonitor ();
            WriteMonitor = new SpeedMonitor ();
        }

        public async ReusableTask<bool> ReadAsync (ITorrentData torrent, BlockInfo block, byte[] buffer)
        {
            if (await ReadFromCacheAsync (torrent, block, buffer))
                return true;

            Interlocked.Add (ref cacheMisses, block.RequestLength);
            return await ReadFromFilesAsync (torrent, block, buffer).ConfigureAwait (false) == block.RequestLength;
        }

        public ReusableTask<bool> ReadFromCacheAsync (ITorrentData torrent, BlockInfo block, byte[] buffer)
        {
            if (torrent == null)
                throw new ArgumentNullException (nameof (torrent));
            if (buffer == null)
                throw new ArgumentNullException (nameof (buffer));

            if (CachedBlocks.TryGetValue (torrent, out List<CachedBlock> blocks)) {
                for (int i = 0; i < blocks.Count; i++) {
                    var cached = blocks[i];
                    if (cached.Block != block)
                        continue;

                    Buffer.BlockCopy (cached.Buffer, 0, buffer, 0, block.RequestLength);
                    if (!cached.Flushing) {
                        blocks[i] = cached.SetFlushing ();
                        FlushBlockAsync (torrent, blocks, cached);
                    }
                    Interlocked.Add (ref cacheHits, block.RequestLength);
                    return ReusableTask.FromResult(true);
                }
            }

            return ReusableTask.FromResult (false);
        }

        async void FlushBlockAsync (ITorrentData torrent, List<CachedBlock> blocks, CachedBlock cached)
        {
            // FIXME: How do we handle failures from this?
            using (cached.BufferReleaser) {
                await WriteToFilesAsync (torrent, cached.Block, cached.Buffer);
                Interlocked.Add (ref cacheUsed, -cached.Block.RequestLength);
                blocks.Remove (cached);
            }
        }

        public async ReusableTask WriteAsync (ITorrentData torrent, BlockInfo block, byte[] buffer, bool preferSkipCache)
        {
            if (preferSkipCache || Capacity < block.RequestLength) {
                await WriteToFilesAsync (torrent, block, buffer);
            } else {
                if (!CachedBlocks.TryGetValue (torrent, out List<CachedBlock> blocks))
                    CachedBlocks[torrent] = blocks = new List<CachedBlock> ();

                if (CacheUsed > (Capacity - block.RequestLength)) {
                    var firstFlushable = FindFirstFlushable (blocks);
                    if (firstFlushable < 0) {
                        await WriteToFilesAsync (torrent, block, buffer);
                        return;
                    } else {
                        var cached = blocks[firstFlushable];
                        blocks[firstFlushable] = cached.SetFlushing ();

                        using (cached.BufferReleaser)
                            await WriteToFilesAsync (torrent, cached.Block, cached.Buffer);

                        Interlocked.Add (ref cacheUsed, -cached.Block.RequestLength);
                        blocks.Remove (cached);
                    }
                }

                CachedBlock? cache = null;
                for (int i = 0; i < blocks.Count && !cache.HasValue; i++) {
                    if (blocks[i].Block == block)
                        cache = blocks[i];
                }

                if (!cache.HasValue) {
                    cache = new CachedBlock (block, DiskManager.BufferPool.Rent (block.RequestLength, out byte[] _));
                    blocks.Add (cache.Value);
                    Interlocked.Add (ref cacheUsed, block.RequestLength);
                }
                Buffer.BlockCopy (buffer, 0, cache.Value.Buffer, 0, block.RequestLength);
            }
        }

        static int FindFirstFlushable (List<CachedBlock> blocks)
        {
            for (int i = 0; i < blocks.Count; i++)
                if (!blocks[i].Flushing)
                    return i;
            return -1;
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
            Writer.Dispose ();
        }
    }
}
