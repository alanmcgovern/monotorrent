using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.TrackerServer;

using NUnit.Framework;

namespace Tests.MonoTorrent.IntegrationTests
{
    [TestFixture]
    public class IntegrationTests
    {
        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            (_tracker, _trackerListener) = GetTracker (_trackerPort);
        }

        [SetUp]
        public void Setup ()
        {
            string tempDirectory = Path.Combine (Path.GetTempPath (), $"{NUnit.Framework.TestContext.CurrentContext.Test.Name}-{Path.GetRandomFileName ()}");
            _directory = Directory.CreateDirectory (tempDirectory);
        }

        [TearDown]
        public void TearDown ()
        {
            _directory?.Refresh ();
            if (_directory?.Exists == true) {
                _directory.Delete (true);
            }
        }

        [OneTimeTearDown]
        public void Cleanup ()
        {
            _tracker.Dispose ();
            _trackerListener.Stop ();
        }

        const int _trackerPort = 40000;
        const int _seederPort = 40001;
        const int _leecherPort = 40002;

        private TrackerServer _tracker;
        private ITrackerListener _trackerListener;
        private DirectoryInfo _directory;

        [Test]
        public async Task DownloadFileInTorrent_V1 () => await CreateAndDownloadTorrent (TorrentType.V1Only, createEmptyFile: false, explitlyHashCheck: false);

        [Test]
        public async Task DownloadFileInTorrent_V2 () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: false);

        [Test]
        public async Task DownloadFileInTorrent_V2_OnlyOneNonEmptyFile () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: false, nonEmptyFileCount: 1);

        [Test]
        public async Task DownloadFileInTorrent_V2_ThreeNonEmptyFiles () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: false, nonEmptyFileCount: 3);

        [Test]
        public async Task DownloadFileInTorrent_V1V2 () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: false, explitlyHashCheck: false);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V1 () => await CreateAndDownloadTorrent (TorrentType.V1Only, createEmptyFile: true, explitlyHashCheck: false);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V2 () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: true, explitlyHashCheck: false);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V1V2 () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: true, explitlyHashCheck: false);

        [Test]
        public async Task DownloadFileInTorrent_V1_HashCheck () => await CreateAndDownloadTorrent (TorrentType.V1Only, createEmptyFile: false, explitlyHashCheck: true);

        [Test]
        public async Task DownloadFileInTorrent_V2_HashCheck () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: true);

        [Test]
        public async Task DownloadFileInTorrent_V1V2_HashCheck () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: false, explitlyHashCheck: true);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V1_HashCheck () => await CreateAndDownloadTorrent (TorrentType.V1Only, createEmptyFile: true, explitlyHashCheck: true);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V2_HashCheck () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: true, explitlyHashCheck: true);

        [Test]
        public async Task DownloadEmptyFileInTorrent_V1V2_HashCheck () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: true, explitlyHashCheck: true);

        public async Task CreateAndDownloadTorrent (TorrentType torrentType, bool createEmptyFile, bool explitlyHashCheck, int nonEmptyFileCount = 2)
        {
            var seederDir = _directory.CreateSubdirectory ("Seeder");
            var leecherDir = _directory.CreateSubdirectory ("Leecher");

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

            var seederIsSeeding = new TaskCompletionSource<bool> ();
            var leecherIsSeeding = new TaskCompletionSource<object> ();
            EventHandler<TorrentStateChangedEventArgs> seederIsSeedingHandler = (o, e) => {
                if (e.NewState == TorrentState.Seeding)
                    seederIsSeeding.TrySetResult (true);
            };

            EventHandler<TorrentStateChangedEventArgs> leecherIsSeedingHandler = (o, e) => {
                if (e.NewState == TorrentState.Seeding)
                    leecherIsSeeding.TrySetResult (true);
            };

            var seederManager = await StartTorrent (seederEngine, torrent, seederDir.FullName, explitlyHashCheck, seederIsSeedingHandler);
            var leecherManager = await StartTorrent (leecherEngine, torrent, leecherDir.FullName, explitlyHashCheck, leecherIsSeedingHandler);

            var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (10));
            timeout.Token.Register (() => { seederIsSeeding.TrySetCanceled (); });
            timeout.Token.Register (() => { leecherIsSeeding.TrySetCanceled (); });

            Assert.DoesNotThrowAsync (async () => await seederIsSeeding.Task, "Seeder should be seeding after hashcheck completes");
            Assert.DoesNotThrowAsync (async () => await leecherIsSeeding.Task, "Leecher should have downloaded all data");

            foreach (var file in nonEmptyFiles) {
                var leecherNonEmptyFile = new FileInfo (Path.Combine (leecherDir.FullName, file.Name));
                Assert.IsTrue (leecherNonEmptyFile.Exists, $"Non empty file {file.Name} should exist");
            }

            var leecherEmptyFile = new FileInfo (Path.Combine (leecherDir.FullName, emptyFile.Name));
            Assert.AreEqual (createEmptyFile, leecherEmptyFile.Exists, "Empty file should exist when created");
        }

        private (TrackerServer, ITrackerListener) GetTracker (int port)
        {
            var tracker = new TrackerServer ();
            tracker.AllowUnregisteredTorrents = true;
            var listenAddress = $"http://127.0.0.1:{port}/";

            var listener = TrackerListenerFactory.CreateHttp (listenAddress);
            tracker.RegisterListener (listener);
            listener.Start ();
            return (tracker, listener);
        }

        private ClientEngine GetEngine (int port)
        {
            // Give an example of how settings can be modified for the engine.
            var settingBuilder = new EngineSettingsBuilder {
                // Use a fixed port to accept incoming connections from other peers for testing purposes. Production usages should use a random port, 0, if possible.
                ListenEndPoint = new IPEndPoint (IPAddress.Any, port),
                ReportedAddress = new IPEndPoint (IPAddress.Parse ("127.0.0.1"), port),
                AutoSaveLoadFastResume = false,
                CacheDirectory = _directory.FullName,
                DhtEndPoint = null,
                AllowPortForwarding = false,
            };
            var engine = new ClientEngine (settingBuilder.ToSettings ());
            return engine;
        }

        private async Task<TorrentManager> StartTorrent (ClientEngine clientEngine, Torrent torrent, string saveDirectory, bool explicitlyHashCheck, EventHandler<TorrentStateChangedEventArgs> handler)
        {
            TorrentSettingsBuilder torrentSettingsBuilder = new TorrentSettingsBuilder () {
                CreateContainingDirectory = false,
            };
            TorrentManager manager = await clientEngine.AddAsync (torrent, saveDirectory, torrentSettingsBuilder.ToSettings ());
            manager.TorrentStateChanged += handler;
            if (explicitlyHashCheck)
                await manager.HashCheckAsync (true);
            else
                await manager.StartAsync ();
            return manager;
        }
    }
}
