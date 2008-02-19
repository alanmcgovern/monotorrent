using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.PieceWriters
{
    public class Pressure
    {
        private int blockIndex;
        private int pieceIndex;
        private int pressure;
        private TorrentManager manager;

        public int BlockIndex
        {
            get { return blockIndex; }
        }

        public int PieceIndex
        {
            get { return pieceIndex; }
        }

        public int Value
        {
            get { return pressure; }
            set { pressure = value; }
        }

        public TorrentManager Manager
        {
            get { return manager; }
        }

        public Pressure(TorrentManager manager, int pieceIndex, int blockIndex)
            : this(manager, pieceIndex, blockIndex, 0)
        {

        }
        public Pressure(TorrentManager manager, int pieceIndex, int blockIndex, int pressure)
        {
            this.manager = manager;
            this.pieceIndex = pieceIndex;
            this.blockIndex = blockIndex;
            this.pressure = pressure;
        }
    }
}
