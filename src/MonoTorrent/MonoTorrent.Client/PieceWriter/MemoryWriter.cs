using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriters
{
    public class MemoryWriter : PieceWriter
    {
        private int capacity;
        private List<PieceData> memoryBuffer;
        private PieceWriter writer;


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

        public MemoryWriter(PieceWriter writer)
            : this(writer, 2 * 1024 * 1024)
        {

        }

        public MemoryWriter(PieceWriter writer, int capacity)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");

            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            memoryBuffer = new List<PieceData>();
            this.capacity = capacity;
            this.writer = writer;
        }

        public override int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count)
        {
            memoryBuffer.Sort(delegate(PieceData left, PieceData right) { return left.WriteOffset.CompareTo(right.WriteOffset); });
            PieceData io = memoryBuffer.Find(delegate(PieceData m) { return ((offset >= m.WriteOffset) && (offset < (m.WriteOffset + m.Count))); });

            if (io == null)
                return writer.Read(manager, buffer, bufferOffset, offset, count);

            int toCopy = Math.Min(count, io.Count + (int)(io.WriteOffset - offset));
            Buffer.BlockCopy(io.Buffer.Array, io.Buffer.Offset + (int)(io.WriteOffset - offset), buffer, bufferOffset, toCopy);
            return toCopy;
        }

        public override void Write(PieceData data)
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

            if (Used > (Capacity - data.Count))
                FlushSome();

            memoryBuffer.Add(data);
        }
        
        private void FlushSome()
        {
            if (memoryBuffer.Count == 0)
                return;

            memoryBuffer.Sort(delegate(PieceData left, PieceData right)
            {
                Pressure lp = FindPressure(left.Manager, left.PieceIndex, left.BlockIndex);
                Pressure rp = FindPressure(right.Manager, right.PieceIndex, right.BlockIndex);
                // If there are no pressures associated with this piece, then return 0
                if (lp == null && rp == null || lp == rp)
                    return 0;

                // If only one of the pressures is null, we pretend that its pressure is 0
                // and compare the other pressure with that
                if (lp == null)
                    return rp.Value.CompareTo(0);

                if (rp == null)
                    return lp.Value.CompareTo(0);

                return lp.Value.CompareTo(rp.Value);
            });

            PieceData data = memoryBuffer[0];
            Write(data, true);
            memoryBuffer.RemoveAt(0);
            pressures.Remove(FindPressure(data.Manager, data.PieceIndex, data.BlockIndex));
        }

        public override void CloseFileStreams(TorrentManager manager)
        {
            Flush(manager);
            writer.CloseFileStreams(manager);
        }

        public override void Flush(TorrentManager manager)
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

        public override void AddPressure(TorrentManager manager, int pieceIndex, int blockIndex)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");

            Pressure p = FindPressure(manager.FileManager, pieceIndex, blockIndex);
            if (p != null)
                p.Value++;
            else
                pressures.Add(new Pressure(manager, pieceIndex, blockIndex, 1));

            writer.AddPressure(manager, pieceIndex, blockIndex);
        }

        public override void RemovePressure(TorrentManager manager, int pieceIndex, int blockIndex)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");

            Pressure p = FindPressure(manager.FileManager, pieceIndex, blockIndex);
            if (p != null)
                p.Value--;
            else
                pressures.Add(new Pressure(manager, pieceIndex, blockIndex, -1));

            writer.RemovePressure(manager, pieceIndex, blockIndex);
        }
    }
}
