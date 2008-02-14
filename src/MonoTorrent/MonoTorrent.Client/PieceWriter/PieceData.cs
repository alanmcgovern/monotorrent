using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class PieceData
    {
        public ArraySegment<byte> Buffer;
        private int count;
        private PeerIdInternal id;
        private Piece piece;
        private int pieceIndex;
        private int startOffset;
		private FileManager fileManager;


        public int BlockIndex
        {
            get { return PiecePickerBase.GetBlockIndex(piece.Blocks, startOffset, count); }
        }

        public int Count
        {
           get { return count; }
        }

        internal PeerIdInternal Id
        {
            get { return id; }
        }

        public FileManager Manager
        {
			get { return fileManager; }
        }

        public Piece Piece
        {
            get { return piece; }
            set { piece = value; }
        }

        public int PieceIndex
        {
            get { return pieceIndex; }
        }

        public int StartOffset
        {
            get { return startOffset; }
        }

        public long WriteOffset
        {
            get { return (long)fileManager.PieceLength * pieceIndex + startOffset; }
        }


        internal PieceData(ArraySegment<byte> buffer, int pieceIndex, int startOffset, int count, PeerIdInternal id)
        {
            this.Buffer = buffer;
            this.count = count;
            this.id = id;
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
			this.fileManager = id.TorrentManager.FileManager;
        }

		public PieceData(ArraySegment<byte> buffer, int pieceIndex, int startOffset, int count, FileManager manager)
			: this(buffer, pieceIndex, startOffset, count, (PeerIdInternal)null)
		{
			fileManager = manager;
		}
    }
}
