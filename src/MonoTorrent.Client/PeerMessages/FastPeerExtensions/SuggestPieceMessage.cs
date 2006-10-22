using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Client.PeerMessages
{
#warning The only use for a SuggestPiece message is for when i load a piece into a Disk Cache and want to make use for it
    internal class SuggestPieceMessage : IPeerMessage
    {
        public const byte MessageId = 0x0D;
        private readonly int messageLength = 5;

        #region Member Variables
        /// <summary>
        /// The index of the suggested piece to request
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }
        private int pieceIndex;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new SuggestPiece message
        /// </summary>
        public SuggestPieceMessage()
        {
        }


        /// <summary>
        /// Creates a new SuggestPiece message
        /// </summary>
        /// <param name="pieceIndex">The suggested piece to download</param>
        public SuggestPieceMessage(int pieceIndex)
        {
            this.pieceIndex = pieceIndex;
        }
        #endregion


        #region Methods
        public int Encode(byte[] buffer, int offset)
        {
            buffer[offset + 4] = MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength)), 0, buffer, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.pieceIndex)), 0, buffer, offset + 5, 4);
            return this.messageLength + 4;
        }


        public void Decode(byte[] buffer, int offset, int length)
        {
            this.pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        }


        public void Handle(PeerConnectionID id)
        {
            id.Peer.Connection.SuggestedPieces.Add(this.pieceIndex);
        }


        public int ByteLength
        {
            get { return this.messageLength + 4; }
        }
        #endregion


        #region Overidden Methods
        public override bool Equals(object obj)
        {
            SuggestPieceMessage msg = obj as SuggestPieceMessage;
            if (msg == null)
                return false;

            return this.pieceIndex == msg.pieceIndex;
        }

        public override int GetHashCode()
        {
            return this.pieceIndex.GetHashCode();
        }

        public override string ToString()
        {
            return "SuggestPieceMessage";
        }
        #endregion
    }
}
