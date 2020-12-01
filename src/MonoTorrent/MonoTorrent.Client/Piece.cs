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
using System.Diagnostics;

namespace MonoTorrent.Client
{
    [DebuggerDisplay ("{" + nameof (ToDebuggerString) + " ()}")]
    public class Piece : IComparable<Piece>
    {
        internal const int BlockSize = (1 << 14); // 16kB

        #region Member Variables

        #endregion MemberVariables


        #region Fields

        public Block this[int index] => Blocks[index];

        internal Block[] Blocks { get; set; }

        public bool AllBlocksRequested => TotalRequested == BlockCount;

        public bool AllBlocksReceived => TotalReceived == BlockCount;

        public bool AllBlocksWritten => TotalWritten == BlockCount;

        public int BlockCount => Blocks.Length;

        public int Index { get; }

        public bool NoBlocksRequested => TotalRequested == 0;

        public int TotalReceived { get; internal set; }

        public int TotalRequested { get; internal set; }

        public int TotalWritten { get; internal set; }

        #endregion Fields


        #region Constructors

        internal Piece (int pieceIndex, int pieceLength, long torrentSize)
        {
            Index = pieceIndex;

            // Request last piece. Special logic needed
            if ((torrentSize - (long) pieceIndex * pieceLength) < pieceLength)
                LastPiece (pieceIndex, pieceLength, torrentSize);

            else {
                int numberOfPieces = (int) Math.Ceiling (((double) pieceLength / BlockSize));

                Blocks = new Block[numberOfPieces];

                for (int i = 0; i < numberOfPieces; i++)
                    Blocks[i] = new Block (this, i * BlockSize, BlockSize);

                if ((pieceLength % BlockSize) != 0)     // I don't think this would ever happen. But just in case
                    Blocks[Blocks.Length - 1] = new Block (this, Blocks[Blocks.Length - 1].StartOffset, pieceLength - Blocks[Blocks.Length - 1].StartOffset);
            }
        }

        void LastPiece (int pieceIndex, int pieceLength, long torrentSize)
        {
            int bytesRemaining = (int) (torrentSize - ((long) pieceIndex * pieceLength));
            int numberOfBlocks = bytesRemaining / BlockSize;
            if (bytesRemaining % BlockSize != 0)
                numberOfBlocks++;

            Blocks = new Block[numberOfBlocks];

            int i = 0;
            while (bytesRemaining - BlockSize > 0) {
                Blocks[i] = new Block (this, i * BlockSize, BlockSize);
                bytesRemaining -= BlockSize;
                i++;
            }

            Blocks[i] = new Block (this, i * BlockSize, bytesRemaining);
        }

        #endregion


        #region Methods

        public int CompareTo (Piece other)
        {
            return other == null ? 1 : Index.CompareTo (other.Index);
        }

        public override bool Equals (object obj)
        {
            return (!(obj is Piece p)) ? false : Index.Equals (p.Index);
        }

        public System.Collections.IEnumerator GetEnumerator ()
        {
            return Blocks.GetEnumerator ();
        }

        public override int GetHashCode ()
        {
            return Index;
        }

        string ToDebuggerString ()
        {
            return $"Piece {Index}";
        }

        #endregion
    }
}