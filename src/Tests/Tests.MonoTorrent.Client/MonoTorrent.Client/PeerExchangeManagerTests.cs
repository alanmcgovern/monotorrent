using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        }

        byte counter = 0;
        PeerId CreatePeer () => PeerId.CreateNull (10, new InfoHash (Enumerable.Repeat<byte> (counter++, 20).ToArray ()));

        [Test]
        public async Task TestPeerExchangeManager ()
        {
            var peer = CreatePeer ();
            var pex = new PeerExchangeManager (new PeerExchangeSource (), peer);

            await ClientEngine.MainLoop;

            pex.OnAdd (CreatePeer ());
            pex.OnAdd (CreatePeer ());
            pex.OnAdd (CreatePeer ());
            pex.OnAdd (CreatePeer ());
            pex.OnDrop (CreatePeer ());
            pex.OnDrop (CreatePeer ());

            pex.OnTick ();

            var message = (PeerExchangeMessage) peer.MessageQueue.TryDequeue ();
            Assert.AreEqual (4 * 6, message.Added.Length);
            Assert.AreEqual (4, message.AddedDotF.Length);
            Assert.AreEqual (2 * 6, message.Dropped.Length);
        }
    }
}
