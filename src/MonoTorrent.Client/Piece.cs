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
    /// <summary>
    /// This class represents a Piece in the torrent
    /// </summary>
    public class Piece
    {
        /// <summary>
        /// The official client rejects any request above 16kb, so even thought it adds more overhead
        /// I use the same size requests. All other clients accept up to 128kB requests (afaik).
        /// In the future the Piece picker could adaptively choose blocksize.
        /// </summary>
        public const int BlockSize = (1 << 14); // 16kB

        #region Member Variables

        /// <summary>
        /// The blocks that this piece is composed of
        /// </summary>
        internal Block[] Blocks
        {
            get { return this.blocks; }
        }
        private Block[] blocks;


        /// <summary>
        /// The index of the piece
        /// </summary>
        public int Index
        {
            get { return this.index; }
        }
        private int index;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new Piece
        /// </summary>
        /// <param name="pieceIndex">The index of the piece</param>
        /// <param name="torrent">The Torrent the piece is from</param>
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
                    blocks[i] = new Block(pieceIndex, i * BlockSize, BlockSize);

                if ((torrent.PieceLength % BlockSize) != 0)     // I don't think this would ever happen. But just in case
                    blocks[blocks.Length - 1] = new Block(pieceIndex, blocks[blocks.Length - 1].StartOffset, (int)(torrent.PieceLength - blocks[blocks.Length - 1].StartOffset));
            }
        }


        /// <summary>
        /// Special logic required to create the "LastPiece" for a torrent
        /// </summary>
        /// <param name="pieceIndex">The index of the piece</param>
        /// <param name="torrent">The ITorrent the piece is coming from</param>
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
                blocks[i] = new Block(pieceIndex, i * BlockSize, BlockSize);
                bytesRemaining -= BlockSize;
                i++;
            }

            blocks[i] = new Block(pieceIndex, i * BlockSize, bytesRemaining);
        }

        #endregion


        #region Methods

        public bool AllBlocksRequested
        {
            get
            {
                for (int i = 0; i < this.blocks.Length; i++)
                    if (!this.blocks[i].Requested)
                        return false;

                return true;
            }
        }


        public bool AllBlocksReceived
        {
            get
            {
                for (int i = 0; i < this.blocks.Length; i++)
                    if (!this.blocks[i].Received)
                        return false;

                return true;
            }
        }


        public bool AllBlocksWritten
        {
            get
            {
                for (int i = 0; i < this.blocks.Length; i++)
                    if (!this.blocks[i].Written)
                        return false;

                return true;
            }
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


        public bool NoBlocksRequested
        {
            get
            {
                for (int i = 0; i < this.blocks.Length; i++)
                    if (this.blocks[i].Requested)
                        return false;

                return true;
            }
        }

        public int BlockCount
        {
            get { return this.blocks.Length; }
        }
        public Block this[int index]
        {
            get { return this.blocks[index]; }
        }

        #endregion
    }
}