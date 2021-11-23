using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class ConnectionManagerTests
    {
        [Test]
        public async Task SortByLeastConnections ()
        {
            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests ());
            var manager = new ConnectionManager ("test", engine.Settings, engine.Factories, engine.DiskManager);

            var torrents = new[] {
                await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ())), "tmp"),
                await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 1, 20).ToArray ())), "tmp"),
                await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 2, 20).ToArray ())), "tmp")
            };

            torrents[0].Peers.ConnectedPeers.Add (PeerId.CreateNull (1));
            torrents[0].Peers.ConnectedPeers.Add (PeerId.CreateNull (1));
            torrents[2].Peers.ConnectedPeers.Add (PeerId.CreateNull (1));

            foreach (var torrent in torrents)
                manager.Add (torrent);

            manager.TryConnect ();

            Assert.AreEqual (torrents[1], manager.Torrents[0]);
            Assert.AreEqual (torrents[2], manager.Torrents[1]);
            Assert.AreEqual (torrents[0], manager.Torrents[2]);
        }
    }
}
