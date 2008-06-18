using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
    public class BufferedIO : ICloneable
    {
        internal ArraySegment<byte> buffer;
        private TorrentManager manager;
        private int actualCount;
        private int count;
        private int pieceIndex;
        private int pieceOffset;
        private PeerId peerId;
        private ManualResetEvent waitHandle;

        public int ActualCount
        {
            get { return actualCount; }
            set { actualCount = value; }
        }
        public int BlockIndex
        {
            get { return pieceOffset / MonoTorrent.Client.Piece.BlockSize; }
        }
        public ArraySegment<byte> Buffer
        {
            get { return buffer; }
        }
        public int Count
        {
            get { return count; }
            set { count = value; }
        }
        internal PeerId Id
        {
            get { return peerId; }
            set { peerId = value; }
        }
        public int PieceIndex
        {
            get { return pieceIndex; }
        }
        public int PieceOffset
        {
            get { return pieceOffset; }
            set { pieceOffset = value; }
        }
        internal Piece Piece;
        public long Offset
        {
            get { return (long)pieceIndex * manager.Torrent.PieceLength + pieceOffset; }
        }
        public TorrentManager Manager
        {
            get { return manager; }
        }

        public ManualResetEvent WaitHandle
        {
            get { return waitHandle; }
            set { waitHandle = value; }
        }

        internal BufferedIO(ArraySegment<byte> buffer, long offset, int count, TorrentManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");

            Initialise(buffer, offset, count, manager);
        }

        public BufferedIO(ArraySegment<byte> buffer, int pieceIndex, int blockIndex, int count, TorrentManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");

            Initialise(buffer, (long)pieceIndex * manager.Torrent.PieceLength + blockIndex * MonoTorrent.Client.Piece.BlockSize, count, manager);
        }

        private void Initialise(ArraySegment<byte> buffer, long offset, int count, TorrentManager manager)
        {
            this.buffer = buffer;
            this.count = count;
            this.manager = manager;
            pieceIndex = (int)(offset / manager.Torrent.PieceLength);
            pieceOffset = (int)(offset % manager.Torrent.PieceLength);
        }

        public override string ToString()
        {
            return string.Format("Piece: {0} Block: {1} Count: {2}", pieceIndex, BlockIndex, count);
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
