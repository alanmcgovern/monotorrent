using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.TrackerServer;

using NUnit.Framework;

namespace Tests.MonoTorrent.IntegrationTests
{
    [TestFixture]
    public class IntegrationTests : IDisposable
    {
        public IntegrationTests ()
        {
            _tracker = GetTracker (_trackerPort);
        }

        public void Dispose () => _tracker.Dispose ();

        const int _trackerPort = 40000;
        const int _seederPort = 40001;
        const int _leecherPort = 40002;
        private readonly TrackerServer _tracker;

        [Test]
        public async Task DownloadFileInTorrent_V1 () => await CreateAndDownloadTorrent (TorrentType.V1Only, false);

        [Test]
        public async Task DownloadFileInTorrent_V2 () => await CreateAndDownloadTorrent (TorrentType.V2Only, false);

        [Test]
        public async Task DownloadFileInTorrent_V2_OnlyOneNonEmptyFile () => await CreateAndDownloadTorrent (TorrentType.V2Only, false, nonEmptyFileCount: 1);

        [Test]
        public async Task DownloadFileInTorrent_V2_ThreeNonEmptyFiles () => await CreateAndDownloadTorrent (TorrentType.V2Only, false, nonEmptyFileCount: 3);

        [Test]
        public async Task DownloadFileInTorrent_V1V2 () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, false);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V1 () => await CreateAndDownloadTorrent (TorrentType.V1Only, true);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V2 () => await CreateAndDownloadTorrent (TorrentType.V2Only, true);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V1V2 () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, true);

        public async Task CreateAndDownloadTorrent (TorrentType torrentType, bool createEmptyFile, int nonEmptyFileCount = 2)
        {

            var dir = new DirectoryInfo (nameof (CreateAndDownloadTorrent));
            if (dir.Exists)
                dir.Delete (true);

            dir.Create ();
            var seederDir = dir.CreateSubdirectory ("Seeder");
            var leecherDir = dir.CreateSubdirectory ("Leecher");

            var emptyFile = new FileInfo (Path.Combine (seederDir.FullName, "Empty.file"));
            if (createEmptyFile)
                File.WriteAllText (emptyFile.FullName, "");

            var nonEmptyFiles = new List<FileInfo> ();
            for (int i = 0; i < nonEmptyFileCount; i++) {
                var nonEmptyFile = new FileInfo (Path.Combine (seederDir.FullName, $"NonEmpty{i}.file"));
                File.WriteAllText (nonEmptyFile.FullName, $"aoeu{i}");
                nonEmptyFiles.Add (nonEmptyFile);
            }

            var fileSource = new TorrentFileSource (seederDir.FullName);
            fileSource.TorrentName = nameof (CreateAndDownloadTorrent);
            TorrentCreator torrentCreator = new TorrentCreator (torrentType);
            torrentCreator.Announce = $"http://localhost:{_trackerPort}/announce";
            var encodedTorrent = await torrentCreator.CreateAsync (fileSource);
            var torrent = Torrent.Load (encodedTorrent);

            using ClientEngine seederEngine = GetEngine (_seederPort);
            using ClientEngine leecherEngine = GetEngine (_leecherPort);

            var seederManager = await StartTorrent (seederEngine, torrent, seederDir.FullName);
            var leecherManager = await StartTorrent (leecherEngine, torrent, leecherDir.FullName);

            Assert.AreEqual (TorrentState.Seeding, seederManager.State);

            Stopwatch sw = Stopwatch.StartNew ();
            while (!leecherManager.Complete && sw.Elapsed.TotalSeconds < 5) {
                await Task.Delay (100);
            }

            Assert.IsTrue (leecherManager.Complete, "Torrent should complete");

            foreach (var file in nonEmptyFiles) {
                var leecherNonEmptyFile = new FileInfo (Path.Combine (leecherDir.FullName, file.Name));
                Assert.IsTrue (leecherNonEmptyFile.Exists, $"Non empty file {file.Name} should exist");
            }

            var leecherEmptyFile = new FileInfo (Path.Combine (leecherDir.FullName, emptyFile.Name));
            Assert.AreEqual (createEmptyFile, leecherEmptyFile.Exists, "Empty file should exist when created");
        }

        private TrackerServer GetTracker (int port)
        {
            var tracker = new TrackerServer ();
            tracker.AllowUnregisteredTorrents = true;
            var listenAddress = $"http://*:{port}/";

            var listener = TrackerListenerFactory.CreateHttp (listenAddress);
            tracker.RegisterListener (listener);
            listener.Start ();
            return tracker;
        }

        private ClientEngine GetEngine (int port)
        {
            // Give an example of how settings can be modified for the engine.
            var settingBuilder = new EngineSettingsBuilder {
                // Use a fixed port to accept incoming connections from other peers for testing purposes. Production usages should use a random port, 0, if possible.
                ListenEndPoint = new IPEndPoint (IPAddress.Any, port),
                ReportedAddress = new IPEndPoint (IPAddress.Parse ("127.0.0.1"), port),
                AutoSaveLoadFastResume = false,
            };
            var engine = new ClientEngine (settingBuilder.ToSettings ());
            return engine;
        }

        private async Task<TorrentManager> StartTorrent (ClientEngine clientEngine, Torrent torrent, string saveDirectory)
        {
            TorrentSettingsBuilder torrentSettingsBuilder = new TorrentSettingsBuilder () {
                CreateContainingDirectory = false,
            };
            TorrentManager manager = await clientEngine.AddAsync (torrent, saveDirectory, torrentSettingsBuilder.ToSettings ());
            await manager.HashCheckAsync (true);
            return manager;
        }
    }
}
