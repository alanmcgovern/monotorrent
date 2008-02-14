using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriter
{
    public class MemoryWriter : IPieceWriter
    {
        private int capacity;
        private List<PieceData> memoryBuffer;
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
                memoryBuffer.ForEach(delegate(PieceData i) { count += i.Count; });
                return count;
            }
        }

        public MemoryWriter(IPieceWriter writer)
            : this(writer, 1 * 1024 * 1024)
        {

        }

		public MemoryWriter(IPieceWriter writer, int capacity)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");

            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            memoryBuffer = new List<PieceData>();
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
                memoryBuffer.Sort(delegate(PieceData left, PieceData right) { return left.WriteOffset.CompareTo(right.WriteOffset); });
                PieceData io = memoryBuffer.Find(delegate(PieceData m) { return ((offset >= m.WriteOffset) && (offset < (m.WriteOffset + m.Count))); });
                if (io != null)
                {
                    int toCopy = Math.Min(count, io.Count + (int)(io.WriteOffset - offset));
                    Buffer.BlockCopy(io.Buffer.Array, io.Buffer.Offset + (int)(io.WriteOffset - offset), buffer, bufferOffset + (origCount - count), toCopy);
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


        public void Write(PieceData data)
        {
            Write(data, false);
        }

        public void Write(PieceData data, bool forceWrite)
        {
            if (forceWrite)
            {
                writer.Write(data);
                return;
            }

            if (Used >= (Capacity - data.Count))
                FlushSome();

            memoryBuffer.Add(data);
        }

        private void FlushSome()
        {
            int count = Math.Min(5, memoryBuffer.Count);
            for (int i = 0; i < count; i++)
            {
                Write(memoryBuffer[i], true);
                ClientEngine.BufferManager.FreeBuffer(ref memoryBuffer[i].Buffer);
            }
            memoryBuffer.RemoveRange(0, count);
        }



        public void CloseFileStreams(TorrentManager manager)
        {
            Flush(manager);
            writer.CloseFileStreams(manager);
        }


        public void Flush(TorrentManager manager)
        {
            memoryBuffer.ForEach(delegate(PieceData io)
            {
                if (io.Manager != manager.FileManager)
                    return;

                Write(io, true);
                ClientEngine.BufferManager.FreeBuffer(ref io.Buffer);
            });
            memoryBuffer.RemoveAll(delegate(PieceData io) { return io.Manager == manager.FileManager; });
        }
    }
}
