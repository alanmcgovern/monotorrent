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

namespace MonoTorrent.Client.PieceWriters
{
    public class MemoryWriter : IPieceWriter
    {
        struct CachedBlock
        {
            public TorrentFile File;
            public long Offset;
            public byte[] Buffer;
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

        public int Read (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
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
            return Writer.Read (file, offset, buffer, bufferOffset, count);
        }

        public void Write (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Write (file, offset, buffer, bufferOffset, count, false);
        }

        public void Write (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count, bool forceWrite)
        {
            if (forceWrite) {
                Writer.Write (file, offset, buffer, bufferOffset, count);
            } else {
                if (CacheUsed > (Capacity - count))
                    Flush (0);

                byte[] cacheBuffer = ClientEngine.BufferPool.Rent (count);
                Buffer.BlockCopy (buffer, bufferOffset, cacheBuffer, 0, count);

                var block = new CachedBlock {
                    Buffer = cacheBuffer,
                    Count = count,
                    Offset = offset,
                    File = file
                };
                CachedBlocks.Add (block);
                Interlocked.Add (ref cacheUsed, block.Count);
            }
        }

        public void Close (TorrentFile file)
        {
            Flush (file);
            Writer.Close (file);
        }

        public bool Exists (TorrentFile file)
        {
            return Writer.Exists (file);
        }

        public void Flush (TorrentFile file)
        {
            CachedBlocks.RemoveAll (delegate (CachedBlock b) {
                if (b.File != file)
                    return false;

                Interlocked.Add (ref cacheUsed, -b.Count);
                Writer.Write (b.File, b.Offset, b.Buffer, 0, b.Count);
                ClientEngine.BufferPool.Return (b.Buffer);
                return true;
            });
        }

        void Flush (int index)
        {
            CachedBlock b = CachedBlocks[index];
            CachedBlocks.RemoveAt (index);
            Interlocked.Add (ref cacheUsed, -b.Count);
            Write (b.File, b.Offset, b.Buffer, 0, b.Count, true);
            ClientEngine.BufferPool.Return (b.Buffer);
        }

        public void Move (TorrentFile file, string newPath, bool overwrite)
        {
            Writer.Move (file, newPath, overwrite);
        }

        public void Dispose ()
        {
            // Flush everything currently held in memory
            while (CachedBlocks.Count > 0)
                Flush (CachedBlocks.Count - 1);

            // Dispose the held writer
            Writer.Dispose ();
        }
    }
}
