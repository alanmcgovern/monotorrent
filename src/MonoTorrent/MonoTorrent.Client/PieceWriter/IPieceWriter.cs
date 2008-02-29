using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriters
{
    public abstract class PieceWriter
    {
        protected List<Pressure> pressures;

        protected PieceWriter()
        {
            pressures = new List<Pressure>();
        }

        private IEnumerable<int> AllBlocks(TorrentManager manager)
        {
            for (int i = 0; i < manager.Torrent.PieceLength / Piece.BlockSize; i++)
                yield return i;
        }

        public void AddPressure(TorrentManager manager, int pieceIndex)
        {
            foreach (int i in AllBlocks(manager))
                AddPressure(manager, pieceIndex, i);
        }

        public virtual void AddPressure(TorrentManager manager, int pieceIndex, int blockIndex)
        {
        }

        public abstract void CloseFileStreams(TorrentManager manager);

        public virtual void Dispose()
        {

        }

        public abstract void Flush(TorrentManager manager);

        protected Pressure FindPressure(FileManager manager, int pieceIndex, int blockIndex)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");

            return pressures.Find(delegate (Pressure p) {
                return p.PieceIndex == pieceIndex && p.BlockIndex == blockIndex && p.Manager.FileManager == manager;
            });
        }

        public abstract int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count);

        public int ReadChunk(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count)
        {
            int read = 0;
            int totalRead = 0;

            while (totalRead != count)
            {
                read = Read(manager, buffer, bufferOffset + totalRead, offset + totalRead, count - totalRead);
                totalRead += read;

                if (read == 0)
                    return totalRead;
            }

            return totalRead;
        }

        public void RemovePressure(TorrentManager manager, int pieceIndex)
        {
            foreach (int i in AllBlocks(manager))
                RemovePressure(manager, pieceIndex, i);
        }

        public virtual void RemovePressure(TorrentManager manager, int pieceIndex, int blockIndex)
        {

        }

        public abstract void Write(PieceData data);
    }
}