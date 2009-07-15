using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class BufferedIO
    {
        internal ArraySegment<byte> buffer;
        private int actualCount;
		private MainLoopTask callback;
        private int count;
        private string path;
        private long offset;
        private int pieceLength;
        private PeerId peerId;
        private TorrentFile[] files;
        private TorrentManager manager;
        private ManualResetEvent waitHandle;

        public int ActualCount
        {
            get { return actualCount; }
            set { actualCount = value; }
        }
        public int BlockIndex
        {
            get { return PieceOffset / MonoTorrent.Client.Piece.BlockSize; }
        }
        public ArraySegment<byte> Buffer
        {
            get { return buffer; }
        }

		internal MainLoopTask Callback
		{
			get { return callback; }
			set { callback = value; }
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
            set { path = value; }
        }
        public int PieceIndex
        {
            get { return (int)(offset / pieceLength); }
        }
        public int PieceOffset
        {
            get { return (int)(offset % pieceLength); ; }
        }
        internal Piece Piece;
        public long Offset
        {
            get { return offset; }
            set { offset = value; }
        }
        public TorrentFile[] Files
        {
            get { return files; }
        }

        internal TorrentManager Manager
        {
            get { return this.manager; }
        }

        public ManualResetEvent WaitHandle
        {
            get { return waitHandle; }
            set { waitHandle = value; }
        }

        internal BufferedIO(TorrentManager manager, ArraySegment<byte> buffer, long offset, int count, int pieceLength, TorrentFile[] files, string path)
        {
            this.manager = manager;
            this.path = path;
            this.files = files;
            this.pieceLength = pieceLength;
            Initialise(buffer, offset, count);
        }

        public BufferedIO(TorrentManager manager, ArraySegment<byte> buffer, int pieceIndex, int blockIndex, int count, int pieceLength, TorrentFile[] files, string path)
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
            this.offset = offset;
        }

        public override string ToString()
        {
            return string.Format("Piece: {0} Block: {1} Count: {2}", PieceIndex, BlockIndex, count);
        }
    }
}
