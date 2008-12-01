using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;
using System.Net;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class AnnounceResponseMessage : UdpTrackerMessage
    {
        int interval;
        int leechers;
        int seeders;

        List<Peer> peers = new List<Peer>();

        public override int ByteLength
        {
            get { return (4 * 5 + peers.Count * 6); }
        }

        public List<Peer> Peers
        {
            get { return peers; }
        }

        public AnnounceResponseMessage()
        {
            Action = 1;
        }

        public AnnounceResponseMessage(int transactionId, int interval, int leechers, int seeders, List<Peer> peers)
            :this()
        {
            TransactionId = transactionId;
            this.interval = interval;
            this.leechers = leechers;
            this.seeders = seeders;
            this.peers = peers;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            Action = ReadInt(buffer, offset);
            TransactionId = ReadInt(buffer, offset + 4);
            interval = ReadInt(buffer, offset + 8);
            leechers = ReadInt(buffer, offset + 12);
            seeders = ReadInt(buffer, offset + 16);

            LoadPeerDetails(buffer, 20);
        }

        private void LoadPeerDetails(byte[] buffer, int offset)
        {
            while(offset <= (buffer.Length - 6))
            {
                int ip = IPAddress.NetworkToHostOrder(ReadInt(buffer, ref offset));
                ushort port = (ushort)ReadShort(buffer, ref offset);
                peers.Add(new Peer("", new Uri("tcp://" + new IPEndPoint(new IPAddress(ip), port).ToString())));
            }
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            written += Write(buffer, written, interval);
            written += Write(buffer, written, leechers);
            written += Write(buffer, written, seeders);

            for (int i=0; i < peers.Count; i++)
                Peers[i].CompactPeer(buffer, written + (i * 6));

            return written - offset;
        }
    }
}
