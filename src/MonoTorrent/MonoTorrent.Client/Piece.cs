//
// Piece.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class Piece : IComparable<Piece>
    {
        internal static readonly int BlockSize = (1 << 14); // 16kB

        #region Member Variables

        private Block[] blocks;
        private int index;
        private int totalReceived;
        private int totalRequested;
        private int totalWritten;

        #endregion MemberVariables


        #region Fields

        public Block this[int index]
        {
            get { return this.blocks[index]; }
        }

        internal Block[] Blocks
        {
            get { return this.blocks; }
        }

        public bool AllBlocksRequested
        {
            get { return this.totalRequested == BlockCount; }
        }

        public bool AllBlocksReceived
        {
            get { return this.totalReceived == BlockCount; }
        }
        
        public bool AllBlocksWritten
        {
            get { return this.totalWritten == BlockCount; }
        }

        public int BlockCount
        {
            get { return this.blocks.Length; }
        }

        public int Index
        {
            get { return this.index; }
        }

        public bool NoBlocksRequested
        {
            get { return this.totalRequested == 0; }
        }

        public int TotalReceived
        {
            get { return this.totalReceived; }
            internal set { this.totalReceived = value; }
        }

        public int TotalRequested
        {
            get { return this.totalRequested; }
            internal set { this.totalRequested = value; }
        }

        public int TotalWritten
        {
            get { return totalWritten; }
            internal set { this.totalWritten = value; }
        }

        #endregion Fields


        #region Constructors

        internal Piece(int pieceIndex, Torrent torrent)
        {
            this.index = pieceIndex;

            if (pieceIndex == torrent.Pieces.Count - 1)      // Request last piece. Special logic needed
                LastPiece(pieceIndex, torrent);

            else
            {
                int numberOfPieces = (int)Math.Ceiling(((double)torrent.PieceLength / BlockSize));

                blocks = new Block[numberOfPieces];

                for (int i = 0; i < numberOfPieces; i++)
                    blocks[i] = new Block(this, i * BlockSize, BlockSize);

                if ((torrent.PieceLength % BlockSize) != 0)     // I don't think this would ever happen. But just in case
                    blocks[blocks.Length - 1] = new Block(this, blocks[blocks.Length - 1].StartOffset, (int)(torrent.PieceLength - blocks[blocks.Length - 1].StartOffset));
            }
        }

        private void LastPiece(int pieceIndex, Torrent torrent)
        {
            int bytesRemaining = Convert.ToInt32(torrent.Size - ((long)torrent.Pieces.Count - 1) * torrent.PieceLength);
            int numberOfBlocks = bytesRemaining / BlockSize;
            if (bytesRemaining % BlockSize != 0)
                numberOfBlocks++;

            blocks = new Block[numberOfBlocks];

            int i = 0;
            while (bytesRemaining - BlockSize > 0)
            {
                blocks[i] = new Block(this, i * BlockSize, BlockSize);
                bytesRemaining -= BlockSize;
                i++;
            }

            blocks[i] = new Block(this, i * BlockSize, bytesRemaining);
        }

        #endregion


        #region Methods

        public int CompareTo(Piece other)
        {
            return other == null ? 1 : Index.CompareTo(other.Index);
        }

        public override bool Equals(object obj)
        {
            Piece p = obj as Piece;
            return (p == null) ? false : this.index.Equals(p.index);
        }

        public System.Collections.IEnumerator GetEnumerator()
        {
            return this.blocks.GetEnumerator();
        }

        public override int GetHashCode()
        {
            return this.index;
        }

        #endregion
    }
}