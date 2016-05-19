using MonoTorrent.BEncoding;
using System;
using System.Collections.Generic;
using Xunit;

namespace MonoTorrent.Client
{
    public class PeerTests
    {
        //static void Main(string[] args)
        //{
        //    PeerTests t = new PeerTests();
        //    t.Setup();
        //    t.CorruptList();
        //}
        private List<Peer> peers;

        public PeerTests()
        {
            peers = new List<Peer>();
            for (int i = 0; i < 10; i++)
            {
                Uri uri = new Uri(string.Format("tcp://192.168.0.{0}:1", i));
                peers.Add(new Peer(new string(i.ToString()[0], 20), uri));
            }
            peers.Add(new Peer(new string('a', 20), new Uri("tcp://255.255.255.255:6530")));
            peers.Add(new Peer(new string('b', 20), new Uri("tcp://255.0.0.0:123")));
            peers.Add(new Peer(new string('c', 20), new Uri("tcp://0.0.255.0:312")));
            peers.Add(new Peer(new string('a', 20), new Uri("tcp://0.0.0.255:3454")));
        }

        [Fact]
        public void CompactPeer()
        {
            string peerId = "12345abcde12345abcde";
            Uri uri = new Uri("tcp://192.168.0.5:12345");
            Peer p = new Peer(peerId, uri);

            byte[] compact = p.CompactPeer();
            Peer peer = Peer.Decode((BEncoding.BEncodedString) compact)[0];
            Assert.Equal(p.ConnectionUri, peer.ConnectionUri);
        }

        [Fact]
        public void CorruptDictionary()
        {
            BEncodedList l = new BEncodedList();
            BEncodedDictionary d = new BEncodedDictionary();
            l.Add(d);
            IList<Peer> decoded = Peer.Decode(l);
            Assert.Equal(0, decoded.Count);
        }

        [Fact]
        public void CorruptList()
        {
            BEncodedList list = new BEncodedList();
            for (int i = 0; i < peers.Count; i++)
                list.Add((BEncodedString) peers[i].CompactPeer());

            list.Insert(2, new BEncodedNumber(5));
            VerifyDecodedPeers(Peer.Decode(list));

            list.Clear();
            list.Add(new BEncodedString(new byte[3]));
            IList<Peer> decoded = Peer.Decode(list);
            Assert.Equal(0, decoded.Count);
        }

        [Fact]
        public void CorruptString()
        {
            IList<Peer> p = Peer.Decode((BEncodedString) "1234");
            Assert.Equal(0, p.Count);

            byte[] b = new byte[] {255, 255, 255, 255, 255, 255};
            p = Peer.Decode((BEncodedString) b);
            Assert.Equal(1, p.Count);

            b = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9};
            p = Peer.Decode((BEncodedString) b);
            Assert.Equal(1, p.Count);
        }

        [Fact]
        public void DecodeList()
        {
            // List of String
            BEncodedList list = new BEncodedList();
            foreach (Peer p in peers)
                list.Add((BEncodedString) p.CompactPeer());

            VerifyDecodedPeers(Peer.Decode(list));
        }

        [Fact]
        public void DecodeDictionary()
        {
            BEncodedList list = new BEncodedList();
            foreach (Peer p in peers)
            {
                BEncodedDictionary dict = new BEncodedDictionary();
                dict.Add("ip", (BEncodedString) p.ConnectionUri.Host);
                dict.Add("port", (BEncodedNumber) p.ConnectionUri.Port);
                dict.Add("peer id", (BEncodedString) p.PeerId);
                list.Add(dict);
            }

            VerifyDecodedPeers(Peer.Decode(list));
        }

        [Fact]
        public void DecodeCompact()
        {
            byte[] bytes = new byte[peers.Count*6];
            for (int i = 0; i < peers.Count; i++)
                peers[i].CompactPeer(bytes, i*6);
            VerifyDecodedPeers(Peer.Decode((BEncodedString) bytes));
        }


        private void VerifyDecodedPeers(List<Peer> decoded)
        {
            Assert.Equal(peers.Count, decoded.Count);
            foreach (Peer dec in decoded)
                Assert.True(peers.Exists(delegate(Peer p) { return p.ConnectionUri.Equals(dec.ConnectionUri); }));
        }
    }
}