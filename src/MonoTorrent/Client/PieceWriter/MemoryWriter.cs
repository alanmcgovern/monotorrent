using System;
using System.Collections.Generic;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriters
{
    public class MemoryWriter : PieceWriter
    {
        private readonly List<CachedBlock> cachedBlocks;

        private readonly PieceWriter writer;

        public MemoryWriter(PieceWriter writer)
            : this(writer, 2*1024*1024)
        {
        }

        public MemoryWriter(PieceWriter writer, int capacity)
        {
            Check.Writer(writer);

            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            cachedBlocks = new List<CachedBlock>();
            Capacity = capacity;
            this.writer = writer;
        }


        public int Capacity { get; set; }

        public int Used
        {
            get { return cachedBlocks.Count*Piece.BlockSize; }
        }

        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File(file);
            Check.Buffer(buffer);

            for (var i = 0; i < cachedBlocks.Count; i++)
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

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
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
                if (Used > Capacity - count)
                    Flush(0);

                var cacheBuffer = BufferManager.EmptyBuffer;
                ClientEngine.BufferManager.GetBuffer(ref cacheBuffer, count);
                Buffer.BlockCopy(buffer, bufferOffset, cacheBuffer, 0, count);

                var block = new CachedBlock();
                block.Buffer = cacheBuffer;
                block.Count = count;
                block.Offset = offset;
                block.File = file;
                cachedBlocks.Add(block);
            }
        }

        public override void Close(TorrentFile file)
        {
            Flush(file);
            writer.Close(file);
        }

        public override bool Exists(TorrentFile file)
        {
            return writer.Exists(file);
        }

        public override void Flush(TorrentFile file)
        {
            for (var i = 0; i < cachedBlocks.Count; i++)
            {
                if (cachedBlocks[i].File == file)
                {
                    var b = cachedBlocks[i];
                    writer.Write(b.File, b.Offset, b.Buffer, 0, b.Count);
                    ClientEngine.BufferManager.FreeBuffer(ref b.Buffer);
                }
            }
            cachedBlocks.RemoveAll(delegate(CachedBlock b) { return b.File == file; });
        }

        public void Flush(int index)
        {
            var b = cachedBlocks[index];
            cachedBlocks.RemoveAt(index);
            Write(b.File, b.Offset, b.Buffer, 0, b.Count, true);
            ClientEngine.BufferManager.FreeBuffer(ref b.Buffer);
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
            writer.Move(oldPath, newPath, ignoreExisting);
        }

        public override void Dispose()
        {
            // Flush everything in memory to disk
            while (cachedBlocks.Count > 0)
                Flush(0);

            // Dispose the held writer
            writer.Dispose();

            base.Dispose();
        }

        private struct CachedBlock
        {
            public TorrentFile File;
            public long Offset;
            public byte[] Buffer;
            public int Count;
        }
    }
}