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

        IPieceWriter Writer { get; }

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
                return count;
            }

            Interlocked.Add (ref cacheMisses, count);
            return await Writer.ReadAsync (file, offset, buffer, bufferOffset, count);
        }

        public async ReusableTask WriteAsync  (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            await WriteAsync (file, offset, buffer, bufferOffset, count, false);
        }

        public async ReusableTask WriteAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count, bool forceWrite)
        {
            if (forceWrite) {
                await Writer.WriteAsync (file, offset, buffer, bufferOffset, count);
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
            foreach (var block in CachedBlocks) {
                if (block.File != file)
                    continue;
                Interlocked.Add (ref cacheUsed, -block.Count);
                using (block.BufferReleaser)
                    await Writer.WriteAsync (block.File, block.Offset, block.Buffer, 0, block.Count);
            }
            CachedBlocks.RemoveAll (b => b.File == file);
        }

        async ReusableTask FlushAsync (int index)
        {
            CachedBlock b = CachedBlocks[index];
            CachedBlocks.RemoveAt (index);
            Interlocked.Add (ref cacheUsed, -b.Count);

            using (b.BufferReleaser)
                await WriteAsync (b.File, b.Offset, b.Buffer, 0, b.Count, true);
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
