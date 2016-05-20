using System;
using System.Collections.Generic;
using System.Net;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    internal class AnnounceResponseMessage : UdpTrackerMessage
    {
        public AnnounceResponseMessage()
            : this(0, TimeSpan.Zero, 0, 0, new List<Peer>())
        {
        }

        public AnnounceResponseMessage(int transactionId, TimeSpan interval, int leechers, int seeders, List<Peer> peers)
            : base(1, transactionId)
        {
            Interval = interval;
            Leechers = leechers;
            Seeders = seeders;
            Peers = peers;
        }

        public override int ByteLength
        {
            get { return 4*5 + Peers.Count*6; }
        }

        public int Leechers { get; private set; }

        public TimeSpan Interval { get; private set; }

        public int Seeders { get; private set; }

        public List<Peer> Peers { get; }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (Action != ReadInt(buffer, offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, offset + 4);
            Interval = TimeSpan.FromSeconds(ReadInt(buffer, offset + 8));
            Leechers = ReadInt(buffer, offset + 12);
            Seeders = ReadInt(buffer, offset + 16);

            LoadPeerDetails(buffer, 20);
        }

        private void LoadPeerDetails(byte[] buffer, int offset)
        {
            while (offset <= buffer.Length - 6)
            {
                var ip = IPAddress.NetworkToHostOrder(ReadInt(buffer, ref offset));
                var port = (ushort) ReadShort(buffer, ref offset);
                Peers.Add(new Peer("", new Uri("tcp://" + new IPEndPoint(new IPAddress(ip), port))));
            }
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            written += Write(buffer, written, (int) Interval.TotalSeconds);
            written += Write(buffer, written, Leechers);
            written += Write(buffer, written, Seeders);

            for (var i = 0; i < Peers.Count; i++)
                Peers[i].CompactPeer(buffer, written + i*6);

            return written - offset;
        }
    }
}