using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client.PeerMessages
{
    internal class AllowedFastMessage : IPeerMessage
    {
        public const int MessageId = 0x11;
        private readonly int messageLength = 5;

        #region Member Variables
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }
        private int pieceIndex;
        #endregion


        #region Constructors
        public AllowedFastMessage()
        {
        }

        public AllowedFastMessage(int pieceIndex)
        {
            this.pieceIndex = pieceIndex;
        }
        #endregion


        #region Methods
        public int Encode(byte[] buffer, int offset)
        {
            buffer[offset + 4] = MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength)), 0, buffer, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.pieceIndex)), 0, buffer, offset+5, 4);
            return this.messageLength + 4;
        }


        public void Decode(byte[] buffer, int offset, int length)
        {
            this.pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        }


        public void Handle(PeerConnectionID id)
        {
            ((TCPConnection)id.Peer.Connection).AllowedFastPieces.Add(this.pieceIndex);
        }


        public int ByteLength
        {
            get { return this.messageLength + 4; }
        }
        #endregion


        #region Overidden Methods
        public override bool Equals(object obj)
        {
            AllowedFastMessage msg = obj as AllowedFastMessage;
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
            return "AllowedFastMessage";
        }
        #endregion
    }
}
