using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.PeerMessages
{
    internal class UnknownMessage : IPeerMessage
    {
        #region IPeerMessage Members

        public int Encode(byte[] buffer, int offset)
        {
            return 0;
        }

        public void Decode(byte[] buffer, int offset, int length)
        {
            return;
        }

        public void Handle(PeerConnectionID id)
        {
            return;
        }

        public int ByteLength
        {
            get { return 0; }
        }

        #endregion
    }
}
