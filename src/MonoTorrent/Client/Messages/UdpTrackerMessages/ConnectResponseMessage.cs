using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Messages;
namespace MonoTorrent.Client.Tracker.UdpTrackerMessages
{
    class ConnectResponseMessage : MonoTorrent.Client.Messages.Message
    {
        public override int ByteLength
        {
            get { return 8 + 4 + 4; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
        }

        public override int Encode(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
