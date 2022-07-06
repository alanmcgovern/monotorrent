//
// Block.cs
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

namespace MonoTorrent.PiecePicking
{
    partial class StandardPicker
    {
        /// <summary>
        ///
        /// </summary>
        struct Block
        {
            readonly Piece piece;
            bool received;

            public int PieceIndex => piece.Index;

            public bool Received {
                get => received;
                internal set {
                    if (value && !received)
                        piece.TotalReceived++;

                    else if (!value && received)
                        piece.TotalReceived--;

                    received = value;
                }
            }

            public int BlockIndex { get; private set; }

            public bool Requested => RequestedOff != null;

            internal IRequester? RequestedOff { get; private set; }

            internal Block (Piece piece, int blockIndex)
            {
                RequestedOff = null;
                this.piece = piece;
                received = false;
                BlockIndex = blockIndex;
            }

            internal PieceSegment CreateRequest (IRequester peer)
            {
                if (RequestedOff == null)
                    piece.TotalRequested++;

                RequestedOff = peer;
                RequestedOff.AmRequestingPiecesCount++;
                return new PieceSegment (PieceIndex, BlockIndex);
            }

            internal void CancelRequest ()
            {
                if (RequestedOff != null) {
                    piece.TotalRequested--;
                    RequestedOff.AmRequestingPiecesCount--;
                    RequestedOff = null;
                }
            }

            public override bool Equals (object? obj)
            {
                if (!(obj is Block other))
                    return false;

                return PieceIndex == other.PieceIndex && BlockIndex == other.BlockIndex;
            }

            public override int GetHashCode ()
            {
                return PieceIndex ^ (BlockIndex << 24);
            }

            internal void FromRequest (ActivePieceRequest block)
            {
                Received = block.Received;
                RequestedOff = block.RequestedOff;

                piece.TotalRequested += 1;
            }

            internal void TrySetReceived (IRequester peer)
            {
                if (!received) {
                    CancelRequest ();
                    RequestedOff = peer;
                    Received = true;
                }
            }
        }
    }
}
