using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class ScrapeResponseMessage : UdpTrackerMessage
    {
        int action;
        int transactionId;

        public override int ByteLength
        {
            get { return 8; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            action = ReadInt(buffer, offset); offset += 4;
            transactionId = ReadInt(buffer, offset); offset += 4;
        }

        public override int Encode(byte[] buffer, int offset)
        {
            Write(buffer, offset, action);
            Write(buffer, offset, transactionId);
            
            return ByteLength;
        }
    }
}
