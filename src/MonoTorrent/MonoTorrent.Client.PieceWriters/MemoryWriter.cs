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
    public class MemoryWriter : IPieceWriter
    {
        struct CachedBlock
        {
            public ITorrentFileInfo File;
            public long Offset;
            public byte[] Buffer;
            public ByteBufferPool.Releaser BufferReleaser;
            public int Count;
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

        internal SpeedMonitor ReadMonitor { get; set; }

        internal SpeedMonitor WriteMonitor { get; set; }

        internal IPieceWriter Writer { get; }

        public MemoryWriter (IPieceWriter writer)
            : this (writer, 2 * 1024 * 1024)
        {

        }

        public MemoryWriter (IPieceWriter writer, int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException (nameof (capacity));

            CachedBlocks = new List<CachedBlock> ();
            Capacity = capacity;
            Writer = writer ?? throw new ArgumentNullException (nameof (writer));
        }

        public async ReusableTask<int> ReadAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File (file);
            Check.Buffer (buffer);

            for (int i = 0; i < CachedBlocks.Count; i++) {
                if (CachedBlocks[i].File != file)
                    continue;
                if (CachedBlocks[i].Offset != offset || CachedBlocks[i].File != file || CachedBlocks[i].Count != count)
                    continue;
                Buffer.BlockCopy (CachedBlocks[i].Buffer, 0, buffer, bufferOffset, count);
                Interlocked.Add (ref cacheHits, count);
                await FlushAsync (i);
                return count;
            }

            Interlocked.Add (ref cacheMisses, count);
            ReadMonitor?.AddDelta (count);
            return await Writer.ReadAsync (file, offset, buffer, bufferOffset, count);
        }

        public ReusableTask WriteAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count, bool preferSkipCache)
            => WriteAsync (file, offset, buffer, bufferOffset, count, preferSkipCache, false);

        public async ReusableTask WriteAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count, bool preferSkipCache, bool forceWrite)
        {
            if (preferSkipCache || forceWrite || Capacity < count) {
                await Writer.WriteAsync (file, offset, buffer, bufferOffset, count, preferSkipCache);
                WriteMonitor?.AddDelta (count);
            } else {
                if (CacheUsed > (Capacity - count))
                    await FlushAsync (0);

                var releaser = DiskManager.BufferPool.Rent (count, out byte[] cacheBuffer);
                Buffer.BlockCopy (buffer, bufferOffset, cacheBuffer, 0, count);

                var block = new CachedBlock {
                    Buffer = cacheBuffer,
                    BufferReleaser = releaser,
                    Count = count,
                    Offset = offset,
                    File = file
                };
                CachedBlocks.Add (block);
                Interlocked.Add (ref cacheUsed, block.Count);
            }
        }

        public async ReusableTask CloseAsync (ITorrentFileInfo file)
        {
            await FlushAsync (file);
            await Writer.CloseAsync (file);
        }

        public ReusableTask<bool> ExistsAsync (ITorrentFileInfo file)
        {
            foreach (var item in CachedBlocks)
                if (item.File == file)
                    return ReusableTask.FromResult (true);

            return Writer.ExistsAsync (file);
        }

        public async ReusableTask FlushAsync (ITorrentFileInfo file)
        {
            // When the 'await FlushAsync' call returns it's possible the 'CachedBlocks' List
            // will have been modified. We could have written more data to it, flushed
            // other blocks, or anything! As such we start flushing from the last piece
            // and slowly work our way towards the first piece. This way 'new' pieces
            // added after we began to flush will not be flushed as part of this invocation.
            for (int i = CachedBlocks.Count - 1; i >= 0; i --) {
                // If something else flushes a block between iterations we may
                // now be attempting to flush a block which no longer exists.
                if (i >= CachedBlocks.Count)
                    continue;

                var block = CachedBlocks[i];
                if (block.File != file)
                    continue;

                await FlushAsync (i);
            }
        }

        async ReusableTask FlushAsync (int index)
        {
            CachedBlock b = CachedBlocks[index];
            CachedBlocks.RemoveAt (index);
            Interlocked.Add (ref cacheUsed, -b.Count);

            using (b.BufferReleaser)
                await WriteAsync (b.File, b.Offset, b.Buffer, 0, b.Count, false, true);
        }

        public async ReusableTask MoveAsync (ITorrentFileInfo file, string newPath, bool overwrite)
        {
            await Writer.MoveAsync (file, newPath, overwrite);
        }

        public void Dispose ()
        {
            Debug.Assert (CachedBlocks.Count == 0, "MemoryWriter should have been flushed before being disposed");
            Writer.Dispose ();
        }
    }
}
