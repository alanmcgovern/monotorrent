using System;
using System.CodeDom;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.Client;
using MonoTorrent.Client.PiecePicking;
using NUnit.Framework;

namespace MonoTorrent.Streaming
{
    [TestFixture]
    public class StreamProviderTests
    {
        ClientEngine Engine { get; set; }
        MagnetLink MagnetLink { get; set; }
        Torrent Torrent { get; set; }


        [SetUp]
        public void Setup ()
        {
            Engine = new ClientEngine ();
            Torrent = TestRig.CreateMultiFileTorrent (new[] { new TorrentFile ("path", Piece.BlockSize * 1024) }, Piece.BlockSize * 8);
            MagnetLink = new MagnetLink (Torrent.InfoHash, "MagnetDownload");
        }

        [TearDown]
        public async Task Teardown ()
        {
            await Engine.StopAll ();
            Engine.Dispose ();
        }

        [Test]
        public async Task DownloadMagnetLink ()
        {
            var provider = new StreamProvider (Engine, "testDir", MagnetLink, "magnetLinkDir");
            Assert.IsNull (provider.Files);

            await provider.StartAsync ();
            CollectionAssert.Contains (Engine.Torrents, provider.Manager);
        }

        [Test]
        public void DownloadMagnetLink_CachedTorrent ()
        {
            var metadataCacheDir = Path.Combine (Path.GetTempPath (), "magnetLinkDir");
            var filePath = Path.Combine (metadataCacheDir, $"{MagnetLink.InfoHash.ToHex()}.torrent");
            Directory.CreateDirectory (metadataCacheDir);
            try {
                File.WriteAllBytes (filePath, Torrent.ToDictionary ().Encode ());
                var provider = new StreamProvider (Engine, "testDir", MagnetLink, metadataCacheDir);
                Assert.IsNotNull (provider.Files);
            } finally {
                Directory.Delete (metadataCacheDir, true);
            }
        }

        [Test]
        public void DownloadMagnetLink_IncorrectCachedTorrent ()
        {
            var magnetLinkOtherInfoHash = new MagnetLink (new InfoHash (new byte[20]), "OtherHash");
            var metadataCacheDir = Path.Combine (Path.GetTempPath (), "magnetLinkDir");
            var filePath = Path.Combine (metadataCacheDir, $"{magnetLinkOtherInfoHash.InfoHash.ToHex ()}.torrent");
            Directory.CreateDirectory (metadataCacheDir);
            try {
                // Write the data for one info hash into a torrent for another info hash.
                File.WriteAllBytes (filePath, Torrent.ToDictionary ().Encode ());
                var provider = new StreamProvider (Engine, "testDir", magnetLinkOtherInfoHash, metadataCacheDir);
                Assert.IsNull (provider.Files);
            } finally {
                Directory.Delete (metadataCacheDir, true);
            }
        }

        [Test]
        public async Task DownloadSameTorrentTwice()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();

            var provider2 = new StreamProvider (Engine, "testDir", Torrent);
            Assert.ThrowsAsync<InvalidOperationException> (() => provider2.StartAsync ());
        }

        [Test]
        public async Task CreateStream ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();
            using var stream = await provider.CreateStreamAsync (Torrent.Files[0], false, CancellationToken.None);
            Assert.IsNotNull (stream);
        }

        [Test]
        public void CreateStreamBeforeStart ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            Assert.ThrowsAsync<InvalidOperationException> (() => provider.CreateHttpStreamAsync (Torrent.Files[0]));
        }

        [Test]
        public async Task CreateStreamTwice ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();
            using var stream = await provider.CreateStreamAsync (Torrent.Files[0], false, CancellationToken.None);
            Assert.ThrowsAsync<InvalidOperationException> (() => provider.CreateStreamAsync (Torrent.Files[0], false, CancellationToken.None));
        }

        [Test]
        public async Task Pause ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();
            await provider.PauseAsync ();
            Assert.IsTrue (provider.Active);
            Assert.IsTrue (provider.Paused);
        }

        [Test]
        public async Task PauseThenResume ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();
            await provider.PauseAsync ();
            await provider.ResumeAsync ();
            Assert.IsTrue (provider.Active);
            Assert.IsFalse (provider.Paused);
        }

        [Test]
        public void PauseTwice ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            Assert.ThrowsAsync<InvalidOperationException> (() => provider.PauseAsync ());
        }

        [Test]
        public void PauseWithoutStarting ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            Assert.ThrowsAsync<InvalidOperationException> (() => provider.PauseAsync ());
        }

        [Test]
        public async Task StartManagerManually ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            // It hasn't been registered with the engine
            Assert.ThrowsAsync<TorrentException> (() => provider.Manager.StartAsync ());
            await Engine.Register (provider.Manager);
            await provider.Manager.StartAsync ();

            // People should not register and start the manager manually.
            Assert.ThrowsAsync<InvalidOperationException> (() => provider.StartAsync ());
        }

        [Test]
        public async Task StopManagerManually ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();
            await provider.Manager.StopAsync ();

            Assert.ThrowsAsync<InvalidOperationException> (() => provider.StopAsync ());
        }

        [Test]
        public async Task StartNormally ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            Assert.IsEmpty (Engine.Torrents);
            await provider.StartAsync ();
            CollectionAssert.Contains (Engine.Torrents, provider.Manager);
        }

        [Test]
        public async Task StartTwice ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();
            Assert.ThrowsAsync<InvalidOperationException> (() => provider.StartAsync ());
        }

        [Test]
        public async Task StopNormally ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();
            await provider.StopAsync ();
            Assert.IsEmpty (Engine.Torrents);
        }

        [Test]
        public async Task StopTwice ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            await provider.StartAsync ();
            await provider.StopAsync ();
            Assert.ThrowsAsync<InvalidOperationException> (() => provider.StopAsync ());
        }

        [Test]
        public async Task UsesStreamingPicker ()
        {
            var provider = new StreamProvider (Engine, "testDir", Torrent);
            Assert.IsInstanceOf<StreamingPiecePicker> (provider.Manager.PieceManager.Picker.BasePicker.BasePicker.BasePicker);
            await provider.StartAsync ();
            Assert.IsInstanceOf<StreamingPiecePicker> (provider.Manager.PieceManager.Picker.BasePicker.BasePicker.BasePicker);
        }

        [Test]
        public async Task WaitForMetadata_Cancellation ()
        {
            var provider = new StreamProvider (Engine, "testDir", MagnetLink, "magnetDir");
            await provider.StartAsync ();

            var metadataTask = provider.WaitForMetadataAsync ();
            await provider.StopAsync ();
            Assert.ThrowsAsync<TaskCanceledException> (() => metadataTask);
        }
    }
}
