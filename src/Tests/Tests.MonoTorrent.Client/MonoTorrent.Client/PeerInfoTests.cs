using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

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
            var peer = PeerInfo.FromCompact (compact, AddressFamily.InterNetwork)[0];
            Assert.AreEqual (p.ConnectionUri, peer.ConnectionUri, "#1");
            Assert.AreEqual (p, peer, "#2");
        }

        [Test]
        public void CompactPeerIPV6 ()
        {
            string peerId = "12345abcde12345abcde";
            Uri uri = new Uri ($"ipv6://[{IPAddress.IPv6Any}]:12345");
            PeerInfo p = new PeerInfo (uri, peerId);

            byte[] compact = p.CompactPeer ();
            Assert.AreEqual (18, compact.Length);

            var peer = PeerInfo.FromCompact (compact, AddressFamily.InterNetworkV6)[0];
            Assert.AreEqual (p.ConnectionUri, peer.ConnectionUri, "#1");
            Assert.AreEqual (p, peer, "#2");
        }

        [Test]
        public void CorruptString ()
        {
            IList<PeerInfo> p = PeerInfo.FromCompact (new BEncodedString ("1234").Span, AddressFamily.InterNetwork);
            Assert.AreEqual (0, p.Count, "#1");

            byte[] b = { 255, 255, 255, 255, 255, 255 };
            p = PeerInfo.FromCompact (b, AddressFamily.InterNetwork);
            Assert.AreEqual (1, p.Count, "#2");

            b = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            p = PeerInfo.FromCompact (b, AddressFamily.InterNetwork);
            Assert.AreEqual (1, p.Count, "#3");
        }

        [Test]
        public void DecodeCompact ()
        {
            int stride = 6;
            byte[] bytes = new byte[peers.Count * stride];
            for (int i = 0; i < peers.Count; i++)
                if (!peers[i].TryWriteCompactPeer (bytes.AsSpan (i * stride, stride), out int written) || written != stride)
                    Assert.Fail ("Incorrect number of bytes written");
            VerifyDecodedPeers (PeerInfo.FromCompact (bytes, AddressFamily.InterNetwork));
        }


        [Test]
        public void Equality_EmptyPeerId ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111")));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111")));

            Assert.AreEqual (one, other, "#1");
            Assert.AreEqual (one.GetHashCode (), other.GetHashCode (), "#2");
        }

        [Test]
        public void Equality_EmptyPeerId_DifferentIP ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111")));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://2.2.2.2:2222")));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_EmptyPeerId_DifferentPort ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111")));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:2222")));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_SamePeerId_DifferentIP ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111"), "test"));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://2.2.2.2:2222"), "test"));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_SamePeerId_DifferentPort ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111"), "test"));
            var other = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:2222"), "test"));

            Assert.AreNotEqual (one, other, "#1");
        }

        [Test]
        public void Equality_SamePeerId_SameIP ()
        {
            var one = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111"), "test"));
            var otherOne = new Peer (new PeerInfo (new Uri ("ipv4://1.1.1.1:1111"), "test"));

            Assert.AreEqual (one, otherOne, "#1");
            Assert.AreEqual (one.GetHashCode (), otherOne.GetHashCode (), "#2");
        }

        void VerifyDecodedPeers (IList<PeerInfo> decoded)
        {
            Assert.AreEqual (peers.Count, decoded.Count, "#1");
            foreach (PeerInfo dec in decoded)
                Assert.IsTrue (peers.Exists (p => p.ConnectionUri.Equals (dec.ConnectionUri)));
        }

        static void VerifyDecodedPeers (IList<PeerInfo> decoded, List<PeerInfo> actual)
        {
            Assert.AreEqual (actual.Count, decoded.Count, "#1");
            foreach (PeerInfo dec in decoded)
                Assert.IsTrue (actual.Exists (p => p.ConnectionUri.Equals (dec.ConnectionUri)));
        }
    }
}
