using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;
using System.Threading;

namespace MonoTorrent.Client.PieceWriters
{
    public class MemoryWriter : PieceWriter
    {
        private int capacity;
        private List<BufferedIO> memoryBuffer;
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
                memoryBuffer.ForEach(delegate(BufferedIO i) { count += i.Count; });
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

            memoryBuffer = new List<BufferedIO>();
            this.capacity = capacity;
            this.writer = writer;
        }

        public override int Read(BufferedIO data)
        {
            int count = data.Count;
            long offset = data.Offset;
            memoryBuffer.Sort(delegate(BufferedIO left, BufferedIO right) { return left.Offset.CompareTo(right.Offset); });
            BufferedIO io = memoryBuffer.Find(delegate(BufferedIO m) { return ((offset >= m.Offset) && (offset < (m.Offset + m.Count))); });

            if (io == null)
                return writer.Read(data);

            int toCopy = Math.Min(count, io.Count + (int)(io.Offset - offset));
            Buffer.BlockCopy(io.buffer.Array, io.buffer.Offset + (int)(io.Offset - offset), data.buffer.Array, data.buffer.Offset, toCopy);
            data.ActualCount += toCopy;
            return toCopy;
        }

        public override void Write(BufferedIO data)
        {
            Write(data, false);
        }

        public void Write(BufferedIO data, bool forceWrite)
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

            memoryBuffer.Sort(delegate(BufferedIO left, BufferedIO right)
            {
                Pressure lp = FindPressure(left.Manager.FileManager, left.PieceIndex, left.PieceOffset / Piece.BlockSize);
                Pressure rp = FindPressure(right.Manager.FileManager, right.PieceIndex, left.PieceOffset / Piece.BlockSize);
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

            BufferedIO data = memoryBuffer[0];
            Write(data, true);
            memoryBuffer.RemoveAt(0);
            pressures.Remove(FindPressure(data.Manager.FileManager, data.PieceIndex, data.PieceOffset / Piece.BlockSize));
        }

        public override WaitHandle CloseFileStreams(TorrentManager manager)
        {
            Flush(manager);
            return writer.CloseFileStreams(manager);
        }

        public override void Flush(TorrentManager manager)
        {
            memoryBuffer.ForEach(delegate(BufferedIO io)
            {
                if (io.Manager != manager)
                    return;

                Write(io, true);
                ClientEngine.BufferManager.FreeBuffer(ref io.buffer);
            });
            memoryBuffer.RemoveAll(delegate(BufferedIO io) { return io.Manager == manager; });
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
