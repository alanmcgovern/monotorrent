using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;
using System.Net;

namespace MonoTorrent.Client.Tracker.UdpTrackerMessages
{
    class AnnounceResponseMessage : Message
    {
        int action;
        int transactionId;
        int interval;
        int leechers;
        int seeders;

        List<Peer> peers = new List<Peer>();

        public override int ByteLength
        {
            get { return (4 * 5); }
        }

        public List<Peer> Peers
        {
            get { return peers; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            action = ReadInt(buffer, offset);
            transactionId = ReadInt(buffer, offset + 4);
            interval = ReadInt(buffer, offset + 8);
            leechers = ReadInt(buffer, offset + 12);
            seeders = ReadInt(buffer, offset + 16);

            LoadPeerDetails(buffer, 20);
        }

        private void LoadPeerDetails(byte[] buffer, int offset)
        {
            for (int i = offset; i < buffer.Length; i += 6)
            {
                int ip = IPAddress.HostToNetworkOrder(ReadInt(buffer, offset));
                ushort port = (ushort)ReadShort(buffer, offset + 4);
                peers.Add(new Peer("", new Uri("tcp://" + new IPAddress(BitConverter.GetBytes(ip)).ToString() + ":" + port.ToString())));
            }
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
