using System;
using System.Net;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    public class ConnectMessage : UdpTrackerMessage
    {
        public ConnectMessage()
            : base(0, DateTime.Now.GetHashCode())
        {
            ConnectionId = IPAddress.NetworkToHostOrder(0x41727101980); // Init connectionId as per spec
        }

        public long ConnectionId { get; private set; }

        public override int ByteLength
        {
            get { return 8 + 4 + 4; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            ConnectionId = ReadLong(buffer, ref offset);
            if (Action != ReadInt(buffer, ref offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, ref offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, ConnectionId);
            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);

            return written - offset;
        }
    }
}