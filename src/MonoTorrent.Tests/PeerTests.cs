using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace MonoTorrent.Client.Tests
{
    [TestFixture]
    public class PeerTests
    {
        [Test]
        public void CompactPeer()
        {
            string peerId = "12345abcde12345abcde";
            Uri uri = new Uri("tcp://192.168.0.5:12345");
            Peer p = new Peer(peerId, uri);

            byte[] compact = p.CompactPeer();
            Peer peer = Peer.Decode((BEncoding.BEncodedString)compact)[0];
            Assert.AreEqual(p.ConnectionUri, peer.ConnectionUri);
        }
    }
}
