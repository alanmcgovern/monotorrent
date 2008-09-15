using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class BufferedIO : ICloneable
    {
        internal ArraySegment<byte> buffer;
        private int actualCount;
        private int count;
        private string path;
        private int pieceIndex;
        private int pieceOffset;
        private int pieceLength;
        private PeerId peerId;
        private TorrentFile[] files;
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
        public string Path
        {
            get { return path; }
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
            get { return (long)pieceIndex * pieceLength + pieceOffset; }
        }
        public TorrentFile[] Files
        {
            get { return files; }
        }

        public ManualResetEvent WaitHandle
        {
            get { return waitHandle; }
            set { waitHandle = value; }
        }

        internal BufferedIO(ArraySegment<byte> buffer, long offset, int count, int pieceLength, TorrentFile[] files, string path)
        {
            this.path = path;
            this.files = files;
            this.pieceLength = pieceLength;
            Initialise(buffer, offset, count);
        }

        public BufferedIO(ArraySegment<byte> buffer, int pieceIndex, int blockIndex, int count, int pieceLength, TorrentFile[] files, string path)
        {
            this.path = path;
            this.files = files;
            this.pieceLength = pieceLength;
            Initialise(buffer, (long)pieceIndex * pieceLength + blockIndex * MonoTorrent.Client.Piece.BlockSize, count);
        }

        private void Initialise(ArraySegment<byte> buffer, long offset, int count)
        {
            this.buffer = buffer;
            this.count = count;
            pieceIndex = (int)(offset / pieceLength);
            pieceOffset = (int)(offset % pieceLength);
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
