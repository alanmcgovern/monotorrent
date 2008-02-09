using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class ScrapeMessage : UdpTrackerMessage
    {
        long connectionId;
        int action;
        int transactionId;
        short numberOfHashes;
        ushort extensions;

        public override int ByteLength
        {
            get { return 8 + 4 + 4 + 2 + 2; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            connectionId = ReadLong(buffer, offset); offset += 8;
            action = ReadInt(buffer, offset); offset += 4;
            transactionId = ReadInt(buffer, offset); offset += 4;
            numberOfHashes = ReadShort(buffer, offset); offset += 2;
            extensions = (ushort)ReadShort(buffer, offset); offset += 2;
        }

        public override int Encode(byte[] buffer, int offset)
        {
            offset += Write(buffer, offset, connectionId);
            offset += Write(buffer, offset, action);
            offset += Write(buffer, offset, transactionId);
            offset += Write(buffer, offset, numberOfHashes);
            offset += Write(buffer, offset, extensions);

            return ByteLength;
        }
    }
}