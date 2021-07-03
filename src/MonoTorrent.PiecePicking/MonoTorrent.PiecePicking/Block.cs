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

        public bool Requested => RequestedOff != null;

        public int RequestLength { get; }

        internal IPeer RequestedOff { get; private set; }

        public int StartOffset { get; }


        internal Block (Piece piece, int startOffset, int requestLength)
        {
            RequestedOff = null;
            this.piece = piece;
            received = false;
            RequestLength = requestLength;
            StartOffset = startOffset;
        }

        internal BlockInfo CreateRequest (IPeer peer)
        {
            if (RequestedOff == null)
                piece.TotalRequested++;

            RequestedOff = peer;
            RequestedOff.AmRequestingPiecesCount++;
            return new BlockInfo (PieceIndex, StartOffset, RequestLength);
        }

        internal void CancelRequest ()
        {
            if (RequestedOff != null) {
                piece.TotalRequested--;
                RequestedOff.AmRequestingPiecesCount--;
                RequestedOff = null;
            }
        }

        public override bool Equals (object obj)
        {
            if (!(obj is Block other))
                return false;

            return PieceIndex == other.PieceIndex && StartOffset == other.StartOffset && RequestLength == other.RequestLength;
        }

        public override int GetHashCode ()
        {
            return PieceIndex ^ RequestLength ^ StartOffset;
        }

        internal static int IndexOf (Block[] blocks, int startOffset, int blockLength)
        {
            int index = startOffset / Piece.BlockSize;
            if (blocks[index].StartOffset != startOffset || blocks[index].RequestLength != blockLength)
                return -1;
            return index;
        }

        internal void FromRequest (ActivePieceRequest block)
        {
            Received = block.Received;
            RequestedOff = block.RequestedOff;

            piece.TotalRequested += 1;
        }

        internal void TrySetReceived (IPeer peer)
        {
            if (!received) {
                CancelRequest ();
                RequestedOff = peer;
                Received = true;
            }
        }
    }
}
