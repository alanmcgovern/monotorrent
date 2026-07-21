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
using System.Collections.Generic;
using System.Diagnostics;

namespace MonoTorrent.PiecePicking
{
    partial class StandardPicker
    {
        [DebuggerDisplay ("{" + nameof (ToDebuggerString) + " ()}")]
        class Piece : IComparable<Piece>, ICacheable
        {
            /// <summary>
            /// Set to true when the original peer times out sending a piece, disconnects, or chokes us.
            /// This allows other peers to immediately begin downloading blocks from this piece to complete
            /// it.
            /// </summary>
            internal bool Abandoned { get; set; }

            Block[] blocks;

            internal Span<Block> Blocks => blocks.AsSpan (0, BlockCount);

            public bool AllBlocksRequested => TotalRequested == Blocks.Length;

            public bool AllBlocksReceived => TotalReceived == Blocks.Length;

            public bool AllBlocksWritten => TotalWritten == Blocks.Length;

            public int BlockCount{ get; private set; }

            public int Index { get; private set; }

            public bool NoBlocksRequested => TotalRequested == 0;

            public int TotalReceived { get; internal set; }

            public int TotalRequested { get; internal set; }

            public int TotalWritten { get; internal set; }


            internal Piece ()
            {
                blocks = Array.Empty<Block> ();
                BlockCount = 0;
            }

            public int CompareTo (Piece? other)
                => other == null ? 1 : Index.CompareTo (other.Index);

            public override bool Equals (object? obj)
                => obj is Piece p && Index.Equals (p.Index);

            public override int GetHashCode ()
            {
                return Index;
            }

            public void Initialise ()
                => Initialise (-1, 0);

            public Piece Initialise (int pieceIndex, int blockCount)
            {
                Index = pieceIndex;

                Abandoned = false;
                BlockCount = blockCount;
                TotalReceived = 0;
                TotalRequested = 0;
                TotalWritten = 0;

                if (blockCount > 0 && blocks.Length < BlockCount)
                    blocks = new Block[blockCount];

                int blockIndex = 0;
                foreach (ref var b in Blocks)
                    b = new Block (this, blockIndex++);

                return this;
            }

            string ToDebuggerString ()
            {
                return $"Piece {Index}";
            }

            internal void CalculatePeersInvolved (HashSet<IRequester> peersInvolved)
            {
                foreach (var block in Blocks)
                    if (block.RequestedOff != null)
                        peersInvolved.Add (block.RequestedOff);
            }
        }
    }
}
