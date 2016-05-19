using System;
using System.Collections;

namespace MonoTorrent.Client
{
    public class Piece : IComparable<Piece>
    {
        internal const int BlockSize = 1 << 14; // 16kB

        #region Member Variables

        #endregion MemberVariables

        #region Fields

        public Block this[int index]
        {
            get { return Blocks[index]; }
        }

        internal Block[] Blocks { get; private set; }

        public bool AllBlocksRequested
        {
            get { return TotalRequested == BlockCount; }
        }

        public bool AllBlocksReceived
        {
            get { return TotalReceived == BlockCount; }
        }

        public bool AllBlocksWritten
        {
            get { return TotalWritten == BlockCount; }
        }

        public int BlockCount
        {
            get { return Blocks.Length; }
        }

        public int Index { get; }

        public bool NoBlocksRequested
        {
            get { return TotalRequested == 0; }
        }

        public int TotalReceived { get; internal set; }

        public int TotalRequested { get; internal set; }

        public int TotalWritten { get; internal set; }

        #endregion Fields

        #region Constructors

        internal Piece(int pieceIndex, int pieceLength, long torrentSize)
        {
            Index = pieceIndex;

            // Request last piece. Special logic needed
            if (torrentSize - (long) pieceIndex*pieceLength < pieceLength)
                LastPiece(pieceIndex, pieceLength, torrentSize);

            else
            {
                var numberOfPieces = (int) Math.Ceiling((double) pieceLength/BlockSize);

                Blocks = new Block[numberOfPieces];

                for (var i = 0; i < numberOfPieces; i++)
                    Blocks[i] = new Block(this, i*BlockSize, BlockSize);

                if (pieceLength%BlockSize != 0) // I don't think this would ever happen. But just in case
                    Blocks[Blocks.Length - 1] = new Block(this, Blocks[Blocks.Length - 1].StartOffset,
                        pieceLength - Blocks[Blocks.Length - 1].StartOffset);
            }
        }

        private void LastPiece(int pieceIndex, int pieceLength, long torrentSize)
        {
            var bytesRemaining = (int) (torrentSize - (long) pieceIndex*pieceLength);
            var numberOfBlocks = bytesRemaining/BlockSize;
            if (bytesRemaining%BlockSize != 0)
                numberOfBlocks++;

            Blocks = new Block[numberOfBlocks];

            var i = 0;
            while (bytesRemaining - BlockSize > 0)
            {
                Blocks[i] = new Block(this, i*BlockSize, BlockSize);
                bytesRemaining -= BlockSize;
                i++;
            }

            Blocks[i] = new Block(this, i*BlockSize, bytesRemaining);
        }

        #endregion

        #region Methods

        public int CompareTo(Piece other)
        {
            return other == null ? 1 : Index.CompareTo(other.Index);
        }

        public override bool Equals(object obj)
        {
            var p = obj as Piece;
            return p == null ? false : Index.Equals(p.Index);
        }

        public IEnumerator GetEnumerator()
        {
            return Blocks.GetEnumerator();
        }

        public override int GetHashCode()
        {
            return Index;
        }

        #endregion
    }
}