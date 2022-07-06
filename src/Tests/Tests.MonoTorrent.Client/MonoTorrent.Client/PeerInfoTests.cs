using System;
using System.Collections.Generic;
using System.Linq;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PeerInfoTests
    {
        private List<PeerInfo> peers;

        [SetUp]
        public void Setup ()
        {
            peers = new List<PeerInfo> ();
            for (int i = 0; i < 10; i++) {
                Uri uri = new Uri ($"ipv4://192.168.0.{i}:1");
                peers.Add (new PeerInfo (uri, new string (i.ToString ()[0], 20)));
            }
            peers.Add (new PeerInfo (new Uri ("ipv4://255.255.255.255:6530"), new string ('a', 20)));
            peers.Add (new PeerInfo (new Uri ("ipv4://255.0.0.0:123"), new string ('b', 20)));
            peers.Add (new PeerInfo (new Uri ("ipv4://0.0.255.0:312"), new string ('c', 20)));
            peers.Add (new PeerInfo (new Uri ("ipv4://0.0.0.255:3454"), new string ('a', 20)));
        }

        [Test]
        public void CompactPeer ()
        {
            string peerId = "12345abcde12345abcde";
            Uri uri = new Uri ("ipv4://192.168.0.5:12345");
            PeerInfo p = new PeerInfo (uri, peerId);

            byte[] compact = p.CompactPeer ();
            var peer = PeerDecoder.Decode (compact)[0];
            Assert.AreEqual (p.ConnectionUri, peer.ConnectionUri, "#1");
            Assert.AreEqual (p, peer, "#2");
        }

        [Test]
        public void CorruptDictionary ()
        {
            BEncodedList l = new BEncodedList ();
            BEncodedDictionary d = new BEncodedDictionary ();
            l.Add (d);
            IList<PeerInfo> decoded = PeerDecoder.Decode (l);
            Assert.AreEqual (0, decoded.Count, "#1");
        }

        [Test]
        public void CorruptList ()
        {
            BEncodedList list = new BEncodedList ();
            for (int i = 0; i < peers.Count; i++)
                list.Add ((BEncodedString) peers[i].CompactPeer ());

            list.Insert (2, new BEncodedNumber (5));
            VerifyDecodedPeers (PeerDecoder.Decode (list));

            list.Clear ();
            list.Add (new BEncodedString (new byte[3]));
            IList<PeerInfo> decoded = PeerDecoder.Decode (list);
            Assert.AreEqual (0, decoded.Count, "#1");
        }

        [Test]
        public void CorruptString ()
        {
            IList<PeerInfo> p = PeerDecoder.Decode ("1234");
            Assert.AreEqual (0, p.Count, "#1");

            byte[] b = { 255, 255, 255, 255, 255, 255 };
            p = PeerDecoder.Decode (b);
            Assert.AreEqual (1, p.Count, "#2");

            b = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            p = PeerDecoder.Decode (b);
            Assert.AreEqual (1, p.Count, "#3");
        }

        [Test]
        public void DecodeList ()
        {
            // List of String
            BEncodedList list = new BEncodedList ();
            foreach (PeerInfo p in peers)
                list.Add ((BEncodedString) p.CompactPeer ());

            VerifyDecodedPeers (PeerDecoder.Decode (list));
        }

        [Test]
        public void DecodeDictionary ()
        {
            var list = new BEncodedList ();
            foreach (PeerInfo p in peers) {
                var dict = new BEncodedDictionary {
                    {"ip", (BEncodedString) p.ConnectionUri.Host},
                    {"port", (BEncodedNumber) p.ConnectionUri.Port},
                    {"peer id", p.PeerId}
                };
                list.Add (dict);
            }

            VerifyDecodedPeers (PeerDecoder.Decode (list));
        }

        [Test]
        public void DecodeCompact ()
        {
            byte[] bytes = new byte[peers.Count * 6];
            for (int i = 0; i < peers.Count; i++)
                peers[i].CompactPeer (bytes.AsSpan (i * 6, 6));
            VerifyDecodedPeers (PeerDecoder.Decode (bytes));
        }

        [Test]
        public void DecodePeerId ()
        {
            var peerId = new BEncodedString (Enumerable.Repeat ((byte) 255, 20).ToArray ());
            var dict = new BEncodedDictionary {
                {"ip", (BEncodedString) "1237.1.2.3"},
                {"port", (BEncodedNumber) 12345}
            };
            dict["peer id"] = peerId;

            var peer = PeerDecoder.Decode (new BEncodedList { dict }).Single ();
            Assert.AreEqual (peerId, peer.PeerId, "#1");
        }

        [Test]
        public void Equality_EmptyPeerId ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111")), new InfoHash (new byte[20]));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111")), new InfoHash (new byte[20]));

            Assert.AreEqual (one, other, "#1");
            Assert.AreEqual (one.GetHashCode (), other.GetHashCode (), "#2");
        }

        [Test]
        public void Equality_EmptyPeerId_DifferentIP ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111")), new InfoHash (new byte[20]));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://2.2.2.2:2222")), new InfoHash (new byte[20]));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_EmptyPeerId_DifferentPort ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111")), new InfoHash (new byte[20]));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:2222")), new InfoHash (new byte[20]));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_SamePeerId_DifferentIP ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111"), "test"), new InfoHash (new byte[20]));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://2.2.2.2:2222"), "test"), InfoHash.FromMemory (new byte[20]));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_SamePeerId_DifferentPort ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111"), "test"), new InfoHash (new byte[20]));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:2222"), "test"), new InfoHash (new byte[20]));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_SamePeerId_SameIP ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111"), "test"), new InfoHash (new byte[20]));
            var otherOne = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111"), "test"), new InfoHash (new byte[20]));

            Assert.AreEqual (one, otherOne, "#1");
            Assert.AreEqual (one.GetHashCode (), otherOne.GetHashCode (), "#2");
        }

        private void VerifyDecodedPeers (IList<PeerInfo> decoded)
        {
            Assert.AreEqual (peers.Count, decoded.Count, "#1");
            foreach (PeerInfo dec in decoded)
                Assert.IsTrue (peers.Exists (p => p.ConnectionUri.Equals (dec.ConnectionUri)));
        }
    }
}
