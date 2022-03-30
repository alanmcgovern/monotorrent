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
        internal class Piece : IComparable<Piece>, ICacheable
        {
            internal const int BlockSize = (1 << 14); // 16kB

            HashSet<IPeer> PeersInvolved { get; }

            public Block this[int index] => Blocks[index];

            /// <summary>
            /// Set to true when the original peer times out sending a piece, disconnects, or chokes us.
            /// This allows other peers to immediately begin downloading blocks from this piece to complete
            /// it.
            /// </summary>
            internal bool Abandoned { get; set; }

            internal Block[] Blocks { get; set; }

            public bool AllBlocksRequested => TotalRequested == Blocks.Length;

            public bool AllBlocksReceived => TotalReceived == Blocks.Length;

            public bool AllBlocksWritten => TotalWritten == Blocks.Length;

            public int BlockCount => Blocks.Length;

            public int Index { get; private set; }

            public bool NoBlocksRequested => TotalRequested == 0;

            public int TotalReceived { get; internal set; }

            public int TotalRequested { get; internal set; }

            public int TotalWritten { get; internal set; }


            internal Piece (int pieceIndex, int length)
            {
                Blocks = Array.Empty<Block> ();
                PeersInvolved = new HashSet<IPeer> ();
                Initialise (pieceIndex, length);
            }

            public int CompareTo (Piece? other)
                => other == null ? 1 : Index.CompareTo (other.Index);

            public override bool Equals (object? obj)
                => obj is Piece p && Index.Equals (p.Index);

            public System.Collections.IEnumerator GetEnumerator ()
            {
                return Blocks.GetEnumerator ();
            }

            public override int GetHashCode ()
            {
                return Index;
            }

            public void Initialise ()
                => Initialise (-1, -1);

            public Piece Initialise (int pieceIndex, int length)
            {
                Index = pieceIndex;

                Abandoned = false;
                TotalReceived = 0;
                TotalRequested = 0;
                TotalWritten = 0;

                if (length != -1) {
                    var expectedLength = (length + BlockSize - 1) / BlockSize;
                    if (Blocks.Length != expectedLength)
                        Blocks = new Block[expectedLength];

                    for (int i = 0; i < Blocks.Length - 1; i++)
                        Blocks[i] = new Block (this, i * BlockSize, BlockSize);

                    Blocks[Blocks.Length - 1] = new Block (this, (Blocks.Length - 1) * BlockSize, length - (Blocks.Length - 1) * BlockSize);
                }

                return this;
            }

            string ToDebuggerString ()
            {
                return $"Piece {Index}";
            }

            internal IList<IPeer> CalculatePeersInvolved ()
            {
                PeersInvolved.Clear ();
                foreach (var block in Blocks)
                    if (block.RequestedOff != null)
                        PeersInvolved.Add (block.RequestedOff);

                var result = new IPeer[PeersInvolved.Count];
                PeersInvolved.CopyTo (result);
                return result;
            }
        }
    }
}
