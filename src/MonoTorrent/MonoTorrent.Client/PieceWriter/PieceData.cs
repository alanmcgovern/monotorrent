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
            get { return id.TorrentManager.FileManager; }
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
            get { return (long)id.TorrentManager.Torrent.PieceLength * pieceIndex + startOffset; }
        }


        internal PieceData(ArraySegment<byte> buffer, int pieceIndex, int startOffset, int count, PeerIdInternal id)
        {
            this.Buffer = buffer;
            this.count = count;
            this.id = id;
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
        }
    }
}
