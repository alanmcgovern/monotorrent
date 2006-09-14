using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Client.PeerMessages
{
    public class RejectRequestMessage : IPeerMessage
    {
        public const byte MessageId = 0x10;
        public readonly int messageLength = 13;

        #region Member Variables
        /// <summary>
        /// The offset in bytes of the block of data
        /// </summary>
        public int StartOffset
        {
            get { return this.startOffset; }
        }
        private int startOffset;

        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }
        private int pieceIndex;

        /// <summary>
        /// The length of the block of data
        /// </summary>
        public int RequestLength
        {
            get { return this.requestLength; }
        }
        private int requestLength;
        #endregion


        #region Constructors
        public RejectRequestMessage()
        {
        }


        public RejectRequestMessage(PieceMessage message)
            :this(message.PieceIndex, message.StartOffset, message.BlockLength)
        {
        }


        public RejectRequestMessage(int pieceIndex, int startOffset, int requestLength)
        {
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
            this.requestLength = requestLength;
        }
        #endregion

        
        #region Methods
        public int Encode(byte[] buffer, int offset)
        {
            buffer[offset + 4] = MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.messageLength)), 0, buffer, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.pieceIndex)), 0, buffer, offset + 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.startOffset)), 0, buffer, offset + 9, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.requestLength)), 0, buffer, offset + 13, 4);
            return this.messageLength + 4;
        }


        public void Decode(byte[] buffer, int offset, int length)
        {
            this.pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
            offset += 4;
            this.startOffset = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
            offset += 4;
            this.requestLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
            offset += 4;
        }


        public void Handle(PeerConnectionID id)
        {
            id.TorrentManager.PieceManager.ReceivedRejectRequest(id, this);
        }


        public int ByteLength
        {
            get { return this.messageLength + 4; }
        }
        #endregion


        #region Overidden Methods
        public override bool Equals(object obj)
        {
            RejectRequestMessage msg = obj as RejectRequestMessage;
            if (msg == null)
                return false;

            return (this.pieceIndex == msg.pieceIndex
                && this.startOffset == msg.startOffset
                && this.requestLength == msg.requestLength);
        }


        public override int GetHashCode()
        {
            return (this.pieceIndex.GetHashCode()
                    ^ this.requestLength.GetHashCode()
                    ^ this.startOffset.GetHashCode());
        }


        public override string ToString()
        {
            return "RejectRequestMessage";
        }
        #endregion
    }
}
