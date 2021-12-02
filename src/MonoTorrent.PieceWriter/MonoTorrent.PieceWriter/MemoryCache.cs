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

using ReusableTasks;

namespace MonoTorrent.PieceWriter
{
    public partial class MemoryCache : IBlockCache
    {
        readonly struct CachedBlock : IEquatable<CachedBlock>
        {
            public readonly bool Flushing;
            public readonly BlockInfo Block;
            public readonly Memory<byte> Buffer;
            public readonly ByteBufferPool.Releaser BufferReleaser;

            public CachedBlock (BlockInfo block, ByteBufferPool.Releaser releaser, Memory<byte> buffer)
                : this (block, releaser, buffer, false)
            {

            }

            CachedBlock (BlockInfo block, ByteBufferPool.Releaser releaser, Memory<byte> buffer, bool flushing)
            {
                Block = block;
                Buffer = buffer;
                BufferReleaser = releaser;
                Flushing = flushing;
            }

            public static bool operator == (CachedBlock left, CachedBlock right)
                => left.Equals (right);

            public static bool operator != (CachedBlock left, CachedBlock right)
                => !left.Equals (right);

            public CachedBlock SetFlushing ()
                => new CachedBlock (Block, BufferReleaser, Buffer, true);

            public override bool Equals (object obj)
                => obj is CachedBlock block && Equals (block);

            public bool Equals (CachedBlock other)
                => other.Block == Block;

            public override int GetHashCode ()
                => Block.GetHashCode ();
        }

        public event EventHandler<BlockInfo> ReadFromCache;
        public event EventHandler<BlockInfo> ReadThroughCache;

        public event EventHandler<BlockInfo> WrittenToCache;
        public event EventHandler<BlockInfo> WrittenThroughCache;

        long cacheHits;
        long cacheMisses;
        long cacheUsed;

        MemoryPool BufferPool { get; }

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
        public long Capacity { get; private set; }

        public IPieceWriter Writer { get; private set; }

        public MemoryCache (MemoryPool bufferPool, long capacity, IPieceWriter writer)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException (nameof (capacity));

            BufferPool = bufferPool;
            Capacity = capacity;
            Writer = writer ?? throw new ArgumentNullException (nameof (writer));

            CachedBlocks = new Dictionary<ITorrentData, List<CachedBlock>> ();
        }

        public async ReusableTask<bool> ReadAsync (ITorrentData torrent, BlockInfo block, Memory<byte> buffer)
        {
            if (await ReadFromCacheAsync (torrent, block, buffer))
                return true;

            Interlocked.Add (ref cacheMisses, block.RequestLength);
            return await ReadFromFilesAsync (torrent, block, buffer).ConfigureAwait (false) == block.RequestLength;
        }

        public ReusableTask<bool> ReadFromCacheAsync (ITorrentData torrent, BlockInfo block, Memory<byte> buffer)
        {
            if (torrent == null)
                throw new ArgumentNullException (nameof (torrent));

            if (CachedBlocks.TryGetValue (torrent, out List<CachedBlock> blocks)) {
                for (int i = 0; i < blocks.Count; i++) {
                    var cached = blocks[i];
                    if (cached.Block != block)
                        continue;

                    cached.Buffer.CopyTo (buffer);
                    if (!cached.Flushing) {
                        blocks[i] = cached.SetFlushing ();
                        FlushBlockAsync (torrent, blocks, cached);
                    }
                    Interlocked.Add (ref cacheHits, block.RequestLength);
                    ReadFromCache?.Invoke (this, block);
                    return ReusableTask.FromResult (true);
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

        public ReusableTask SetCapacityAsync (long capacity)
        {
            Capacity = capacity;
            return ReusableTask.CompletedTask;
        }

        public ReusableTask SetWriterAsync (IPieceWriter writer)
        {
            Writer = writer;
            return ReusableTask.CompletedTask;
        }

        public async ReusableTask WriteAsync (ITorrentData torrent, BlockInfo block, Memory<byte> buffer, bool preferSkipCache)
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
                    var releaser = BufferPool.Rent (block.RequestLength, out Memory<byte> memory);
                    cache = new CachedBlock (block, releaser, memory);
                    blocks.Add (cache.Value);
                    Interlocked.Add (ref cacheUsed, block.RequestLength);
                }
                buffer.CopyTo (cache.Value.Buffer);
                WrittenToCache?.Invoke (this, block);
            }
        }

        static int FindFirstFlushable (List<CachedBlock> blocks)
        {
            for (int i = 0; i < blocks.Count; i++)
                if (!blocks[i].Flushing)
                    return i;
            return -1;
        }

        ReusableTask<int> ReadFromFilesAsync (ITorrentData torrent, BlockInfo block, Memory<byte> buffer)
        {
            ReadThroughCache?.Invoke (this, block);
            return Writer.ReadFromFilesAsync (torrent, block, buffer);
        }

        ReusableTask WriteToFilesAsync (ITorrentData torrent, BlockInfo block, Memory<byte> buffer)
        {
            WrittenThroughCache?.Invoke (this, block);
            return Writer.WriteToFilesAsync (torrent, block, buffer);
        }

        public void Dispose ()
        {
            Writer.Dispose ();
        }
    }
}
