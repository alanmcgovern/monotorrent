using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriter
{
    internal class MemIO
    {
        public FileManager manager;
        public byte[] buffer;
        public int bufferOffset;
        public long offset;
        public int count;
        public BufferedIO io;

        public MemIO(FileManager manager, BufferedIO io, byte[] buffer, int bufferOffset, long offset, int count)
        {
            this.manager = manager;
            this.io = io;
            this.buffer = buffer;
            this.bufferOffset = bufferOffset;
            this.offset = offset;
            this.count = count;
        }
    }
    public class MemoryWriter : IPieceWriter
    {
        private int capacity;
        private List<MemIO> memoryBuffer;
        private IPieceWriter writer;


        public int Capacity
        {
            get { return capacity; }
        }

        public int Used
        {
            get
            {
                int count = 0;
                memoryBuffer.ForEach(delegate(MemIO i) { count += i.count; });
                return count;
            }
        }

        internal MemoryWriter(IPieceWriter writer)
            : this(writer, 8 * 1024 * 1024)
        {

        }

        internal MemoryWriter(IPieceWriter writer, int capacity)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");

            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            memoryBuffer = new List<MemIO>();
            this.capacity = capacity;
            this.writer = writer;
        }

        public void Dispose()
        {

        }


        public int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count)
        {
            int origCount = count;
            while (count != 0)
            {
                memoryBuffer.Sort(delegate(MemIO left, MemIO right) { return left.offset.CompareTo(right.offset); });
                MemIO io = memoryBuffer.Find(delegate(MemIO m) { return ((offset >= m.offset) && (offset < (m.offset + m.count))); });
                if (io != null)
                {
                    int toCopy = Math.Min(count, io.count + (int)(io.offset - offset));
                    Buffer.BlockCopy(io.buffer, io.bufferOffset + (int)(io.offset - offset), buffer, bufferOffset + (origCount - count), toCopy);
                    offset += toCopy;
                    count -= toCopy;
                }
                else
                    break;
            }
            if(count == 0)
                return origCount;

            return writer.Read(manager, buffer, bufferOffset, offset, count) + (origCount - count);
        }

        private void Write(MemIO m)
        {
            Write(m.io, m.buffer, m.bufferOffset, m.offset, m.count, true);
        }


        public void Write(BufferedIO io, byte[] buffer, int bufferOffset, long offset, int count)
        {
            Write(io, buffer, bufferOffset, offset, count, false);
        }

        public void Write(BufferedIO io, byte[] buffer, int bufferOffset, long offset, int count, bool forceWrite)
        {
            if (forceWrite)
            {
                writer.Write(io, buffer, bufferOffset, offset, count);
                return;
            }

            if (Used >= (Capacity - count))
                FlushSome();

            memoryBuffer.Add(new MemIO(io.Id.TorrentManager.FileManager, io, buffer, bufferOffset, offset, count));
        }

        private void FlushSome()
        {
            int count = Math.Min(5, memoryBuffer.Count);
            for (int i = 0; i < count; i++)
                Write(memoryBuffer[i]);

            memoryBuffer.RemoveRange(0, count);
        }



        public void CloseFileStreams(TorrentManager manager)
        {
            Flush(manager);
            writer.CloseFileStreams(manager);
        }


        public void Flush(TorrentManager manager)
        {
            memoryBuffer.ForEach(delegate(MemIO io) { if (io.manager == manager.FileManager) Write(io); });
            memoryBuffer.RemoveAll(delegate(MemIO io) { return io.manager == manager.FileManager; });
        }
    }
}
