using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PeerConnectionFactoryTests
    {
        class TorrentData : ITorrentManagerInfo
        {
            IList<ITorrentFile> ITorrentInfo.Files => Files.ToArray<ITorrentFile> ();
            public IList<ITorrentManagerFile> Files { get; }
            public InfoHashes InfoHashes { get; } = new InfoHashes (null, new InfoHash (new byte[32]));
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
