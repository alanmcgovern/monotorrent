using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Client.PeerMessages
{
    public class HaveNoneMessage : IPeerMessage
    {
        public const byte MessageId = 0x0F;
        private readonly int messageLength = 1;

        #region Constructors
        public HaveNoneMessage()
        {
        }
        #endregion


        #region Methods
        public int Encode(byte[] buffer, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength)), 0, buffer, offset, 4);
            buffer[offset + 4] = MessageId;
            return this.messageLength + 4;
        }


        public void Decode(byte[] buffer, int offset, int length)
        {
            // No decoding needed
        }


        public void Handle(PeerConnectionID id)
        {
            id.Peer.Connection.BitField.SetAll(false);
        }


        public int ByteLength
        {
            get { return this.messageLength + 4; }
        }
        #endregion


        #region Overidden Methods
        public override bool Equals(object obj)
        {
            return obj is HaveNoneMessage;
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        public override string ToString()
        {
            return "HaveNoneMessage";
        }
        #endregion
    }
}
