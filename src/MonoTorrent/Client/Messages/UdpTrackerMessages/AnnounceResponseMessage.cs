using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Tracker.UdpTrackerMessages
{
    class AnnounceResponseMessage : Message
    {
        int action;
        int transactionId;
        int interval;
        int leechers;
        int seeders;

        public override int ByteLength
        {
            get { return (4 * 5); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            action = ReadInt(buffer, offset);
            transactionId = ReadInt(buffer, offset + 4);
            interval = ReadInt(buffer, offset + 8);
            leechers = ReadInt(buffer, offset + 12);
            seeders = ReadInt(buffer, offset + 16);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            offset += Write(buffer, offset, action);
            offset += Write(buffer, offset, transactionId);
            offset += Write(buffer, offset, interval);
            offset += Write(buffer, offset, leechers);
            offset += Write(buffer, offset, seeders);

            return ByteLength;
        }
    }
}
