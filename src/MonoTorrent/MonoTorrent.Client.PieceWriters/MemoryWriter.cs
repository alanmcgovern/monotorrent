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

        private int capacity;
        private List<CachedBlock> cachedBlocks;
        private IPieceWriter writer;


        public int Capacity
        {
            get { return capacity; }
            set { capacity = value; }
        }

        public int Used
        {
            get { return this.cachedBlocks.Count * Piece.BlockSize; }
        }

        public MemoryWriter(IPieceWriter writer)
            : this(writer, 2 * 1024 * 1024)
        {

        }

        public MemoryWriter(IPieceWriter writer, int capacity)
        {
            Check.Writer(writer);

            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            cachedBlocks = new List<CachedBlock>();
            this.capacity = capacity;
            this.writer = writer;
        }

        public int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File(file);
            Check.Buffer(buffer);

            for (int i = 0; i < cachedBlocks.Count; i++)
            {
                if (cachedBlocks[i].File != file)
                    continue;
                if (cachedBlocks[i].Offset != offset || cachedBlocks[i].File != file || cachedBlocks[i].Count != count)
                    continue;
                Buffer.BlockCopy(cachedBlocks[i].Buffer, 0, buffer, bufferOffset, count);
                return count;
            }

            return writer.Read(file, offset, buffer, bufferOffset, count);
        }

        public void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Write(file, offset, buffer, bufferOffset, count, false);
        }

        public void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count, bool forceWrite)
        {
            if (forceWrite)
            {
                writer.Write(file, offset, buffer, bufferOffset, count);
            }
            else
            {
                if (Used > (Capacity - count))
                    Flush(0);

                byte[] cacheBuffer = ClientEngine.BufferManager.GetBuffer(count);
                Buffer.BlockCopy(buffer, bufferOffset, cacheBuffer, 0, count);

                CachedBlock block = new CachedBlock();
                block.Buffer = cacheBuffer;
                block.Count = count;
                block.Offset = offset;
                block.File = file;
                cachedBlocks.Add(block);
            }
        }
        
        public void Close(TorrentFile file)
        {
            Flush(file);
            writer.Close(file);
        }

        public bool Exists(TorrentFile file)
        {
            return this.writer.Exists(file);
        }

        public void Flush(TorrentFile file)
        {
            for (int i = 0; i < cachedBlocks.Count; i++)
            {
                if (cachedBlocks[i].File == file)
                {
                    CachedBlock b = cachedBlocks[i];
                    writer.Write(b.File, b.Offset, b.Buffer, 0, b.Count);
                    ClientEngine.BufferManager.FreeBuffer(b.Buffer);
                }
            }
            cachedBlocks.RemoveAll(delegate(CachedBlock b) { return b.File == file; });
        }

        public void Flush(int index)
        {
            CachedBlock b = cachedBlocks[index];
            cachedBlocks.RemoveAt (index);
            Write (b.File, b.Offset, b.Buffer, 0, b.Count, true);
            ClientEngine.BufferManager.FreeBuffer(b.Buffer);
        }

        public void Move(TorrentFile file, string newPath, bool overwrite)
        {
            writer.Move(file, newPath, overwrite);
        }

        public void Dispose()
        {
            // Flush everything in memory to disk
            while (cachedBlocks.Count > 0)
                Flush(0);

            // Dispose the held writer
            writer.Dispose();
        }
    }
}
