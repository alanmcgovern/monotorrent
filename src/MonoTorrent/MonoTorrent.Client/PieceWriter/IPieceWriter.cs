using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.Threading;

namespace MonoTorrent.Client.PieceWriters
{
    public abstract class PieceWriter
    {
        protected PieceWriter()
        {
            //pressures = new List<Pressure>();
        }

        private IEnumerable<int> AllBlocks(TorrentManager manager)
        {
            for (int i = 0; i < manager.Torrent.PieceLength / Piece.BlockSize; i++)
                yield return i;
        }

        public abstract void CloseFileStreams(TorrentManager manager);

        public virtual void Dispose()
        {

        }

        public abstract void Flush(TorrentManager manager);

        public abstract int Read(BufferedIO data);

        public int ReadChunk(BufferedIO data)
        {
            BufferedIO clone = (BufferedIO)((ICloneable)data).Clone();
            int read = 0;
            int totalRead = 0;

            while (totalRead != data.Count)
            {
                read = Read(clone);
                clone.buffer = new ArraySegment<byte>(clone.buffer.Array, clone.buffer.Offset + read, clone.buffer.Count - read);
                clone.PieceOffset += read;
                clone.Count -= read;
                totalRead += read;

                if (read == 0)
                    break;
            }

            data.ActualCount = totalRead;
            return totalRead;
        }

        public abstract void Write(BufferedIO data);

        /*
        protected List<Pressure> pressures;
        public void AddPressure(TorrentManager manager, int pieceIndex)
        {
            foreach (int i in AllBlocks(manager))
                AddPressure(manager, pieceIndex, i);
        }
        public virtual void AddPressure(TorrentManager manager, int pieceIndex, int blockIndex)
        {
        }
        public void RemovePressure(TorrentManager manager, int pieceIndex)
        {
            foreach (int i in AllBlocks(manager))
                RemovePressure(manager, pieceIndex, i);
        }
        public virtual void RemovePressure(TorrentManager manager, int pieceIndex, int blockIndex)
        {

        }
        protected Pressure FindPressure(FileManager manager, int pieceIndex, int blockIndex)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");

            return pressures.Find(delegate(Pressure p)
            {
                return p.PieceIndex == pieceIndex && p.BlockIndex == blockIndex && p.Manager.FileManager == manager;
            });
        }
        */
    }
}