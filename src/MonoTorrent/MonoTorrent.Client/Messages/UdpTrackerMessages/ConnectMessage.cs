using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    public class ConnectMessage : UdpTrackerMessage
    {
        private long connectionId;

        public long ConnectionId
        {
            get { return connectionId; }
        }

        public ConnectMessage()
        {
            Action = 0;                                                 // Connect message
            connectionId = IPAddress.NetworkToHostOrder(0x41727101980); // Init connectionId as per spec
            TransactionId = DateTime.Now.GetHashCode();                 // Random ID created from current datetime
        }

        public override int ByteLength
        {
            get { return 8 + 4 + 4; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            connectionId = ReadLong(buffer, ref offset);
            Action = ReadInt(buffer, ref offset);
            TransactionId = ReadInt(buffer, ref offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, written, connectionId);
            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);

            return written - offset;
        }
    }
}
