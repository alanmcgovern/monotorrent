using System;
using System.Collections.Generic;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PeerConnectionFactoryTests
    {
        class TorrentData : ITorrentData
        {
            public IList<ITorrentFileInfo> Files { get; }
            public InfoHash InfoHash { get; }
            public string Name { get; }
            public int PieceLength { get; }
            public long Size { get; }
        }

        [Test]
        public void InvalidPort ()
        {
            Assert.IsNull (Factories.Default.CreatePeerConnection (new Uri ("ipv4://127.0.1.2")));
        }
    }
}
