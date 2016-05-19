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


using System;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public struct Block
    {
        #region Private Fields

        private Piece piece;
        private int startOffset;
        private PeerId requestedOff;
        private int requestLength;
        private bool requested;
        private bool received;
        private bool written;

        #endregion Private Fields

        #region Properties

        public int PieceIndex
        {
            get { return piece.Index; }
        }

        public bool Received
        {
            get { return received; }
            internal set
            {
                if (value && !received)
                    piece.TotalReceived++;

                else if (!value && received)
                    piece.TotalReceived--;

                received = value;
            }
        }

        public bool Requested
        {
            get { return requested; }
            internal set
            {
                if (value && !requested)
                    piece.TotalRequested++;

                else if (!value && requested)
                    piece.TotalRequested--;

                requested = value;
            }
        }

        public int RequestLength
        {
            get { return requestLength; }
        }

        public bool RequestTimedOut
        {
            get
            {
                // 60 seconds timeout for a request to fulfill
                return !Received && requestedOff != null &&
                       DateTime.Now - requestedOff.LastMessageReceived > TimeSpan.FromMinutes(1);
            }
        }

        internal PeerId RequestedOff
        {
            get { return requestedOff; }
            set { requestedOff = value; }
        }

        public int StartOffset
        {
            get { return startOffset; }
        }

        public bool Written
        {
            get { return written; }
            internal set
            {
                if (value && !written)
                    piece.TotalWritten++;

                else if (!value && written)
                    piece.TotalWritten--;

                written = value;
            }
        }

        #endregion Properties

        #region Constructors

        internal Block(Piece piece, int startOffset, int requestLength)
        {
            requestedOff = null;
            this.piece = piece;
            received = false;
            requested = false;
            this.requestLength = requestLength;
            this.startOffset = startOffset;
            written = false;
        }

        #endregion

        #region Methods

        internal RequestMessage CreateRequest(PeerId id)
        {
            Requested = true;
            RequestedOff = id;
            RequestedOff.AmRequestingPiecesCount++;
            return new RequestMessage(PieceIndex, startOffset, requestLength);
        }

        internal void CancelRequest()
        {
            Requested = false;
            RequestedOff.AmRequestingPiecesCount--;
            RequestedOff = null;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Block))
                return false;

            var other = (Block) obj;
            return PieceIndex == other.PieceIndex && startOffset == other.startOffset &&
                   requestLength == other.requestLength;
        }

        public override int GetHashCode()
        {
            return PieceIndex ^ requestLength ^ startOffset;
        }

        internal static int IndexOf(Block[] blocks, int startOffset, int blockLength)
        {
            var index = startOffset/Piece.BlockSize;
            if (blocks[index].startOffset != startOffset || blocks[index].RequestLength != blockLength)
                return -1;
            return index;
        }

        #endregion
    }
}