using System;
using System.Collections.Generic;
using System.Linq;

using MonoTorrent.BEncoding;
using MonoTorrent.Messages.Peer.Libtorrent;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PeerExchangeManagerTests
    {
        class PeerExchangeSource : IPeerExchangeSource
        {
            public TorrentSettings Settings { get; } = new TorrentSettings ();

#pragma warning disable CS0067
            public event EventHandler<PeerConnectedEventArgs> PeerConnected;
            public event EventHandler<PeerDisconnectedEventArgs> PeerDisconnected;
#pragma warning restore CS0067
        }

        byte counter = 0;
        PeerId CreatePeer () => PeerId.CreateNull (10, new InfoHash (Enumerable.Repeat<byte> (counter++, 20).ToArray ()));

        [Test]
        public void TestPeerExchangeManager ()
        {
            var peer = CreatePeer ();
            var pex = new PeerExchangeManager (new PeerExchangeSource (), peer);
            pex.OnAdd (null, new PeerConnectedEventArgs (null, CreatePeer ()));
            pex.OnAdd (null, new PeerConnectedEventArgs (null, CreatePeer ()));
            pex.OnAdd (null, new PeerConnectedEventArgs (null, CreatePeer ()));
            pex.OnAdd (null, new PeerConnectedEventArgs (null, CreatePeer ()));
            pex.OnDrop (null, new PeerDisconnectedEventArgs (null, CreatePeer ()));
            pex.OnDrop (null, new PeerDisconnectedEventArgs (null, CreatePeer ()));

            pex.OnTick ();

            var message = (PeerExchangeMessage) peer.MessageQueue.TryDequeue ();
            Assert.AreEqual (4 * 6, message.Added.Length);
            Assert.AreEqual (4, message.AddedDotF.Length);
            Assert.AreEqual (2 * 6, message.Dropped.Length);
        }
    }
}
