using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public partial class DiskManager
    {
    public class BufferedIO : ICacheable
    {
        internal byte[] buffer;
        private int actualCount;
		private DiskIOCallback callback;
        private int count;
        private long offset;
        private int pieceLength;
        private PeerId peerId;
        private IList<TorrentFile> files;
        private TorrentManager manager;
        private bool complete;

        public int ActualCount
        {
            get { return actualCount; }
            set { actualCount = value; }
        }
        public int BlockIndex
        {
            get { return PieceOffset / MonoTorrent.Client.Piece.BlockSize; }
        }
        public byte[] Buffer
        {
            get { return buffer; }
        }

		internal DiskIOCallback Callback
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
        public int PieceIndex
        {
            get { return (int)(offset / pieceLength); }
        }
        public int PieceLength
        {
            get { return pieceLength; }
        }
        public int PieceOffset
        {
            get { return (int)(offset % pieceLength); ; }
        }
        public long Offset
        {
            get { return offset; }
            set { offset = value; }
        }
        public IList<TorrentFile> Files
        {
            get { return files; }
        }

        internal TorrentManager Manager
        {
            get { return this.manager; }
        }

        public bool Complete
        {
            get { return complete; }
            set { complete = value; }
        }

        public BufferedIO()
        {

        }

        public void Initialise()
        {
            Initialise(null, BufferManager.EmptyBuffer, 0, 0, 0, null);
        }

        public void Initialise(TorrentManager manager, byte[] buffer, long offset, int count, int pieceLength, IList<TorrentFile> files)
        {
            this.actualCount = 0;
            this.buffer = buffer;
            this.callback = null;
            this.complete = false;
            this.count = count;
            this.files = files;
            this.manager = manager;
            this.offset = offset;
            this.peerId = null;
            this.pieceLength = pieceLength;
        }

        public override string ToString()
        {
            return string.Format("Piece: {0} Block: {1} Count: {2}", PieceIndex, BlockIndex, count);
        }
        }
    }
}
