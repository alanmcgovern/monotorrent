using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        static TimeSpan CancellationTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds (10);

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            (_tracker, _trackerListener) = GetTracker (_trackerPort);
            _httpSeeder = CreateWebSeeder ();
        }

        [SetUp]
        public void Setup ()
        {
            string tempDirectory = Path.Combine (Path.GetTempPath (), $"{NUnit.Framework.TestContext.CurrentContext.Test.Name}-{Path.GetRandomFileName ()}");
            _directory = Directory.CreateDirectory (tempDirectory);
            _seederDir = _directory.CreateSubdirectory ("Seeder");
            _leecherDir = _directory.CreateSubdirectory ("Leecher");
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
            _httpSeeder.Stop ();
            _httpSeeder.Close ();

            _tracker.Dispose ();
            _trackerListener.Stop ();
        }

        const int _trackerPort = 40000;
        const int _seederPort = 40001;
        const int _leecherPort = 40002;
        const int _webSeedPort = 40003;
        const string _webSeedPrefix = "SeedUrlPrefix";
        const string _torrentName = "IntegrationTests";

        private HttpListener _httpSeeder;
        private TrackerServer _tracker;
        private ITrackerListener _trackerListener;
        private DirectoryInfo _directory;
        private DirectoryInfo _seederDir;
        private DirectoryInfo _leecherDir;

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

        [Test]
        public async Task WebSeedDownload_V1 () => await CreateAndDownloadTorrent (TorrentType.V1Only, createEmptyFile: true, explitlyHashCheck: false, useWebSeedDownload: true);

        [Test]
        public async Task WebSeedDownload_V1V2 () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: true, explitlyHashCheck: false, useWebSeedDownload: true);

        [Test]
        public async Task WebSeedDownload_V1V2_BiggerFile () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: true, explitlyHashCheck: false, useWebSeedDownload: true, fileSize: 131_073);

        [Test]
        public async Task WebSeedDownload_V2 () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: true, explitlyHashCheck: false, useWebSeedDownload: true);

        public async Task CreateAndDownloadTorrent (TorrentType torrentType, bool createEmptyFile, bool explitlyHashCheck, int nonEmptyFileCount = 2, bool useWebSeedDownload = false, int fileSize = 5)
        {
            var emptyFile = new FileInfo (Path.Combine (_seederDir.FullName, "Empty.file"));
            if (createEmptyFile)
                File.WriteAllText (emptyFile.FullName, "");

            var nonEmptyFiles = new List<FileInfo> ();
            for (int i = 0; i < nonEmptyFileCount; i++) {
                var nonEmptyFile = new FileInfo (Path.Combine (_seederDir.FullName, $"NonEmpty{i}.file"));
                var fileText = new String ('a', fileSize);
                File.WriteAllText (nonEmptyFile.FullName, fileText);
                nonEmptyFiles.Add (nonEmptyFile);
            }

            var fileSource = new TorrentFileSource (_seederDir.FullName);
            fileSource.TorrentName = _torrentName;
            TorrentCreator torrentCreator = new TorrentCreator (torrentType);
            torrentCreator.Announce = $"http://localhost:{_trackerPort}/announce";
            if (useWebSeedDownload) {
                torrentCreator.GetrightHttpSeeds.Add ($"http://localhost:{_webSeedPort}/{_webSeedPrefix}/");
            }
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

            var seederManager = !useWebSeedDownload ? await StartTorrent (seederEngine, torrent, _seederDir.FullName, explitlyHashCheck, seederIsSeedingHandler) : null;

            var leecherManager = await StartTorrent (leecherEngine, torrent, _leecherDir.FullName, explitlyHashCheck, leecherIsSeedingHandler);

            var timeout = new CancellationTokenSource (CancellationTimeout);
            timeout.Token.Register (() => { seederIsSeeding.TrySetCanceled (); });
            timeout.Token.Register (() => { leecherIsSeeding.TrySetCanceled (); });

            if (!useWebSeedDownload) {
                Assert.DoesNotThrowAsync (async () => await seederIsSeeding.Task, "Seeder should be seeding after hashcheck completes");
            }
            Assert.DoesNotThrowAsync (async () => await leecherIsSeeding.Task, "Leecher should have downloaded all data");

            foreach (var file in nonEmptyFiles) {
                var leecherNonEmptyFile = new FileInfo (Path.Combine (_leecherDir.FullName, file.Name));
                Assert.IsTrue (leecherNonEmptyFile.Exists, $"Non empty file {file.Name} should exist");
            }

            var leecherEmptyFile = new FileInfo (Path.Combine (_leecherDir.FullName, emptyFile.Name));
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
                WebSeedDelay = TimeSpan.Zero,
            };
            var engine = new ClientEngine (settingBuilder.ToSettings ());
            return engine;
        }

        private HttpListener CreateWebSeeder ()
        {
            HttpListener listener = new HttpListener ();
            listener.Prefixes.Add ($"http://localhost:{_webSeedPort}/");
            listener.Start ();
            listener.BeginGetContext (OnHttpContext, listener);
            return listener;
        }

        private void OnHttpContext (IAsyncResult ar)
        {
            var listener = ar.AsyncState as HttpListener;
            var ctx = listener.EndGetContext (ar);
            listener.BeginGetContext (OnHttpContext, ar.AsyncState);

            var localPath = ctx.Request.Url.LocalPath;
            string relativeSeedingPath = $"/{_webSeedPrefix}/{_torrentName}/";
            if (localPath.Contains (relativeSeedingPath)) {
                var fileName = localPath.Replace (relativeSeedingPath, string.Empty);
                var files = _seederDir.GetFiles ();
                var file = files.FirstOrDefault (x => x.Name == fileName);
                if (file != null) {
                    using FileStream fs = new FileStream (file.FullName, FileMode.Open, FileAccess.Read);
                    long start = 0;
                    long end = fs.Length - 1;
                    var rangeHeader = ctx.Request.Headers["Range"];
                    if (rangeHeader != null) {
                        var startAndEnd = rangeHeader.Replace ("bytes=", "").Split ('-');
                        start = long.Parse (startAndEnd[0]);
                        end = long.Parse (startAndEnd[1]);
                    }
                    var buffer = new byte[end - start + 1];
                    fs.Seek (start, SeekOrigin.Begin);
                    fs.Read (buffer, 0, buffer.Length);
                    ctx.Response.OutputStream.Write (buffer, 0, buffer.Length);
                    ctx.Response.OutputStream.Close ();
                    return;
                }
            }

            ctx.Response.StatusCode = 404;
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
