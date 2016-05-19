using System;
using Xunit;

namespace MonoTorrent.Client
{
    public class PeerTest
    {
        [Fact]
        public void CompactPeerTest()
        {
            var peerId = "12345abcde12345abcde";
            var uri = new Uri("tcp://192.168.0.5:12345");
            var p = new Peer(peerId, uri);
            var compact = p.CompactPeer();
            var peer = Peer.Decode(compact)[0];
            Assert.Equal(p.ConnectionUri, peer.ConnectionUri);
        }
    }
}