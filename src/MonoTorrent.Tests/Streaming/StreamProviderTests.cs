using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoTorrent.Client;
using MonoTorrent.Client.PiecePicking;
using MonoTorrent.Streaming;
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
            MagnetLink = new MagnetLink (new InfoHash (new byte[20]), "MagnetDownload");
            Torrent = TestRig.CreateMultiFileTorrent (new[] { new TorrentFile ("path", Piece.BlockSize * 1024) }, Piece.BlockSize * 8);
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
            await provider.StartAsync ();
            CollectionAssert.Contains (Engine.Torrents, provider.Manager);
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
            using var stream = provider.CreateStreamAsync (Torrent.Files[0]);
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
            using var stream = provider.CreateStreamAsync (Torrent.Files[0]);
            Assert.ThrowsAsync<InvalidOperationException> (() => provider.CreateStreamAsync (Torrent.Files[0]));
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
    }
}
