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
        private int requestedAt;
        private PeerIdInternal requestedOff;
        private int requestLength;
        private bool requested;
        private bool received;
        private bool written;

        #endregion Private Fields


        #region Properties

        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex
        {
            get { return this.piece.Index; }
        }

        /// <summary>
        /// True if this piece has been Received
        /// </summary>
        public bool Received
        {
            get { return this.received; }
            internal set
            {
                if (value && !received)
                    piece.TotalReceived++;

                else if (!value && received)
                    piece.TotalReceived--;

                this.received = value;
            }
        }

        /// <summary>
        /// True if this block has been requested
        /// </summary>
        public bool Requested
        {
            get { return this.requested; }
            internal set
            {
                if (value && !requested)
                    piece.TotalRequested++;

                else if (!value && requested)
                    piece.TotalRequested--;

                this.requested = value;
            }
        }

        /// <summary>
        /// The length in bytes of this block
        /// </summary>
        public int RequestLength
        {
            get { return this.requestLength; }
        }

        public bool RequestTimedOut
        {
            get { return !Received && requestedAt != 0 && (Environment.TickCount - requestedAt) > 60000; } // 60 seconds timeout for a request to fulfill
        }

        /// <summary>
        /// The peer who we requested this piece off
        /// </summary>
        internal PeerIdInternal RequestedOff
        {
            get { return this.requestedOff; }
            set { this.requestedOff = value; }
        }

        /// <summary>
        /// The offset in bytes that this block starts at
        /// </summary>
        public int StartOffset
        {
            get { return this.startOffset; }
        }

        /// <summary>
        /// True if the block has been written to disk
        /// </summary>
        public bool Written
        {
            get { return this.written; }
            internal set
            {
                if (value && !written)
                    piece.TotalWritten++;

                else if (!value && written)
                    piece.TotalWritten--;

                this.written = value;
            }
        }

        #endregion Properties


        #region Constructors

        /// <summary>
        /// Creates a new Block
        /// </summary>
        /// <param name="pieceIndex">The index of the piece this block is from</param>
        /// <param name="startOffset">The offset in bytes that this block starts at</param>
        /// <param name="requestLength">The length in bytes of the block</param>
        internal Block(Piece piece, int startOffset, int requestLength)
        {
            this.requestedAt = 0;
            this.requestedOff = null;
            this.piece = piece;
            this.received = false;
            this.requested = false;
            this.requestLength = requestLength;
            this.startOffset = startOffset;
            this.written = false;
        }

        #endregion


        #region Methods

        /// <summary>
        /// Creates a RequestMessage for this Block
        /// </summary>
        /// <returns></returns>
        internal RequestMessage CreateRequest(PeerIdInternal id)
        {
            this.requestedAt = Environment.TickCount;
            this.requestedOff = id;
            return new RequestMessage(PieceIndex, this.startOffset, this.requestLength);
        }

        internal void CancelRequest()
        {
            this.requested = false;
            this.requestedAt = 0;
            this.requestedOff = null;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Block))
                return false;

            Block other = (Block)obj;
            return this.PieceIndex == other.PieceIndex && this.startOffset == other.startOffset && this.requestLength == other.requestLength;
        }

        public override int GetHashCode()
        {
            return this.PieceIndex ^ this.requestLength ^ this.startOffset;
        }

        #endregion
    }
}
