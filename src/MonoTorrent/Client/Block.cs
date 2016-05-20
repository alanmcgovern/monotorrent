using System;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    /// <summary>
    /// </summary>
    public struct Block
    {
        #region Private Fields

        private readonly Piece piece;
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

        public int RequestLength { get; }

        public bool RequestTimedOut
        {
            get
            {
                // 60 seconds timeout for a request to fulfill
                return !Received && RequestedOff != null &&
                       DateTime.Now - RequestedOff.LastMessageReceived > TimeSpan.FromMinutes(1);
            }
        }

        internal PeerId RequestedOff { get; set; }

        public int StartOffset { get; }

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
            RequestedOff = null;
            this.piece = piece;
            received = false;
            requested = false;
            RequestLength = requestLength;
            StartOffset = startOffset;
            written = false;
        }

        #endregion

        #region Methods

        internal RequestMessage CreateRequest(PeerId id)
        {
            Requested = true;
            RequestedOff = id;
            RequestedOff.AmRequestingPiecesCount++;
            return new RequestMessage(PieceIndex, StartOffset, RequestLength);
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
            return PieceIndex == other.PieceIndex && StartOffset == other.StartOffset &&
                   RequestLength == other.RequestLength;
        }

        public override int GetHashCode()
        {
            return PieceIndex ^ RequestLength ^ StartOffset;
        }

        internal static int IndexOf(Block[] blocks, int startOffset, int blockLength)
        {
            var index = startOffset/Piece.BlockSize;
            if (blocks[index].StartOffset != startOffset || blocks[index].RequestLength != blockLength)
                return -1;
            return index;
        }

        #endregion
    }
}