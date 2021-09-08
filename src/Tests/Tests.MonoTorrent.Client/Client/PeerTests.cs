using System;
using System.Collections.Generic;
using System.Linq;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PeerTests
    {
        private List<Peer> peers;

        [SetUp]
        public void Setup ()
        {
            peers = new List<Peer> ();
            for (int i = 0; i < 10; i++) {
                Uri uri = new Uri ($"ipv4://192.168.0.{i}:1");
                peers.Add (new Peer (new string (i.ToString ()[0], 20), uri));
            }
            peers.Add (new Peer (new string ('a', 20), new Uri ("ipv4://255.255.255.255:6530")));
            peers.Add (new Peer (new string ('b', 20), new Uri ("ipv4://255.0.0.0:123")));
            peers.Add (new Peer (new string ('c', 20), new Uri ("ipv4://0.0.255.0:312")));
            peers.Add (new Peer (new string ('a', 20), new Uri ("ipv4://0.0.0.255:3454")));
        }

        [Test]
        public void CompactPeer ()
        {
            string peerId = "12345abcde12345abcde";
            Uri uri = new Uri ("ipv4://192.168.0.5:12345");
            Peer p = new Peer (peerId, uri);

            byte[] compact = p.CompactPeer ();
            Peer peer = Peer.Decode (compact)[0];
            Assert.AreEqual (p.ConnectionUri, peer.ConnectionUri, "#1");
        }

        [Test]
        public void CorruptDictionary ()
        {
            BEncodedList l = new BEncodedList ();
            BEncodedDictionary d = new BEncodedDictionary ();
            l.Add (d);
            IList<Peer> decoded = Peer.Decode (l);
            Assert.AreEqual (0, decoded.Count, "#1");
        }

        [Test]
        public void CorruptList ()
        {
            BEncodedList list = new BEncodedList ();
            for (int i = 0; i < peers.Count; i++)
                list.Add ((BEncodedString) peers[i].CompactPeer ());

            list.Insert (2, new BEncodedNumber (5));
            VerifyDecodedPeers (Peer.Decode (list));

            list.Clear ();
            list.Add (new BEncodedString (new byte[3]));
            IList<Peer> decoded = Peer.Decode (list);
            Assert.AreEqual (0, decoded.Count, "#1");
        }

        [Test]
        public void CorruptString ()
        {
            IList<Peer> p = Peer.Decode ("1234");
            Assert.AreEqual (0, p.Count, "#1");

            byte[] b = { 255, 255, 255, 255, 255, 255 };
            p = Peer.Decode (b);
            Assert.AreEqual (1, p.Count, "#2");

            b = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            p = Peer.Decode (b);
            Assert.AreEqual (1, p.Count, "#3");
        }

        [Test]
        public void DecodeList ()
        {
            // List of String
            BEncodedList list = new BEncodedList ();
            foreach (Peer p in peers)
                list.Add ((BEncodedString) p.CompactPeer ());

            VerifyDecodedPeers (Peer.Decode (list));
        }

        [Test]
        public void DecodeDictionary ()
        {
            var list = new BEncodedList ();
            foreach (Peer p in peers) {
                var dict = new BEncodedDictionary {
                    {"ip", (BEncodedString) p.ConnectionUri.Host},
                    {"port", (BEncodedNumber) p.ConnectionUri.Port},
                    {"peer id", p.PeerId}
                };
                list.Add (dict);
            }

            VerifyDecodedPeers (Peer.Decode (list));
        }

        [Test]
        public void DecodeCompact ()
        {
            byte[] bytes = new byte[peers.Count * 6];
            for (int i = 0; i < peers.Count; i++)
                peers[i].CompactPeer (bytes, i * 6);
            VerifyDecodedPeers (Peer.Decode (bytes));
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

            var peer = Peer.Decode (new BEncodedList {dict}).Single ();
            Assert.AreEqual (peerId, peer.PeerId, "#1");
        }

        [Test]
        public void Equality_EmptyPeerId ()
        {
            var one = new Peer ("", new Uri ("ipv4://1.1.1.1:1111"));
            var other = new Peer ("", new Uri ("ipv4://1.1.1.1:1111"));

            Assert.AreEqual (one, other, "#1");
            Assert.AreEqual (one.GetHashCode (), other.GetHashCode (), "#2");
        }

        [Test]
        public void Equality_EmptyPeerId_DifferentIP ()
        {
            var one = new Peer ("", new Uri ("ipv4://1.1.1.1:1111"));
            var other = new Peer ("", new Uri ("ipv4://2.2.2.2:2222"));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_EmptyPeerId_DifferentPort ()
        {
            var one = new Peer ("", new Uri ("ipv4://1.1.1.1:1111"));
            var other = new Peer ("", new Uri ("ipv4://1.1.1.1:2222"));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_SamePeerId_DifferentIP ()
        {
            var one = new Peer ("test", new Uri ("ipv4://1.1.1.1:1111"));
            var other = new Peer ("test", new Uri ("ipv4://2.2.2.2:2222"));

            Assert.AreEqual (one, other, "#1");
            Assert.AreEqual (one.GetHashCode (), other.GetHashCode (), "#2");
        }

        [Test]
        public void Equality_SamePeerId_DifferentPort ()
        {
            var one = new Peer ("test", new Uri ("ipv4://1.1.1.1:1111"));
            var other = new Peer ("test", new Uri ("ipv4://1.1.1.1:2222"));

            Assert.AreEqual (one, other, "#1");
        }

        [Test]
        public void Equality_SamePeerId_SameIP ()
        {
            var one = new Peer ("test", new Uri ("ipv4://1.1.1.1:1111"));
            var otherOne = new Peer ("test", new Uri ("ipv4://1.1.1.1:1111"));

            Assert.AreEqual (one, otherOne, "#1");
            Assert.AreEqual (one.GetHashCode (), otherOne.GetHashCode (), "#2");
        }

        private void VerifyDecodedPeers (IList<Peer> decoded)
        {
            Assert.AreEqual (peers.Count, decoded.Count, "#1");
            foreach (Peer dec in decoded)
                Assert.IsTrue (peers.Exists (p => p.ConnectionUri.Equals (dec.ConnectionUri)));
        }
    }
}
