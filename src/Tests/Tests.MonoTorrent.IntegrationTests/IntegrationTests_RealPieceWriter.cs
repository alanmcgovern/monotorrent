using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

using Mono.Nat.Logging;

using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.Logging;
using MonoTorrent.PiecePicking;
using MonoTorrent.PieceWriter;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.IntegrationTests
{
    [TestFixture]
    public class IPv4IntegrationTests : IntegrationTestsBase
    {
        public IPv4IntegrationTests ()
            : base (IPAddress.Any, IPAddress.Loopback)
        {

        }
    }

    [TestFixture]
    public class IPv6IntegrationTests : IntegrationTestsBase
    {
        public IPv6IntegrationTests ()
            : base (IPAddress.IPv6Any, IPAddress.IPv6Loopback)
        {

        }
    }

    public abstract class IntegrationTestsBase
    {
        const int PieceLength = 32768;
        static readonly TimeSpan CancellationTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds (60);

        public IPAddress AnyAddress { get; }
        public IPAddress LoopbackAddress { get; }

        protected IntegrationTestsBase (IPAddress anyAddress, IPAddress loopbackAddress)
            => (AnyAddress, LoopbackAddress) = (anyAddress, loopbackAddress);

        protected virtual Factories LeecherFactory => Factories.Default;
        protected virtual Factories SeederFactory => Factories.Default;


        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            MonoTorrent.Logging.LoggerFactory.Create ("test");
            (_tracker, _trackerListener) = GetTracker ();
            _httpSeeder = CreateWebSeeder ();
        }

        [SetUp]
        public void Setup ()
        {
            LoggerFactory.Register (new TextWriterLogger (TestContext.Out));
            _failHttpRequest = false;
            string tempDirectory = Path.Combine (Path.GetTempPath (), "monotorrent_tests", $"{NUnit.Framework.TestContext.CurrentContext.Test.Name}-{Path.GetRandomFileName ()}");

            _directory = Directory.CreateDirectory (tempDirectory);
            _seederDir = _directory.CreateSubdirectory ("Seeder");
            _leecherDir = _directory.CreateSubdirectory ("Leecher");

            streams = new List<FileStream> ();
            seederEngine = GetEngine (0, SeederFactory);
            leecherEngine = GetEngine (0, LeecherFactory);
        }

        [TearDown]
        public async Task TearDown ()
        {
            if (seederEngine != null)
                await seederEngine.StopAllAsync ();
            if (leecherEngine != null)
                await leecherEngine.StopAllAsync ();

            foreach (var stream in streams)
                stream.Dispose ();
            streams = null;

            _directory?.Refresh ();
            if (_directory?.Exists == true) {
                _directory.Delete (true);
            }
            LoggerFactory.Register (null);
        }

        [OneTimeTearDown]
        public void Cleanup ()
        {
            _httpSeeder.Stop ();
            _httpSeeder.Close ();

            _tracker.Dispose ();
            _trackerListener.Stop ();
        }

        int _webSeedPort;
        int _trackerPort;
        const string _webSeedPrefix = "SeedUrlPrefix";
        const string _torrentName = "IntegrationTests";

        private HttpListener _httpSeeder;
        private MonoTorrent.TrackerServer.TrackerServer _tracker;
        private ITrackerListener _trackerListener;
        private DirectoryInfo _directory;
        private DirectoryInfo _seederDir;
        private DirectoryInfo _leecherDir;

        private bool _failHttpRequest;

        ClientEngine seederEngine;
        ClientEngine leecherEngine;
        List<FileStream> streams;

        [Test]
        public async Task DownloadFileInTorrent_V1 () => await CreateAndDownloadTorrent (TorrentType.V1Only, createEmptyFile: false, explitlyHashCheck: false);

        [Test]
        public async Task DownloadFileInTorrent_V2 () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: false);

        [Test]
        public async Task DownloadFileInTorrent_V2_MagnetLink () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: false, magnetLinkLeecher: true);

        [Test]
        public async Task DownloadFileInTorrent_V2_MagnetLink_Large () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: false, magnetLinkLeecher: true, fileSize: PieceLength * 17);

        [Test]
        public async Task DownloadFileInTorrent_V2_OnlyOneNonEmptyFile () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: false, nonEmptyFileCount: 1);

        [Test]
        public async Task DownloadFileInTorrent_V2_ThreeNonEmptyFiles () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: false, explitlyHashCheck: false, nonEmptyFileCount: 3);

        [Test]
        public async Task DownloadFileInTorrent_V1V2 () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: false, explitlyHashCheck: false);

        // 100 byte files will not have a 'layers' key as there's only 1 piece.
        [Test]
        public async Task DownloadFileInTorrent_V1V2_MagnetLink_NoLayers () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: false, explitlyHashCheck: false, magnetLinkLeecher: true, fileSize: 100);

        // 5MB files will have a 'layers' key as there will be many pieces.
        // Make sure we correctly upgrade BEP52 connections when receiving an incoming connection
        [Test]
        public async Task DownloadFileInTorrent_V1V2_MagnetLinkWithLayers_SeederIncoming () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: false, explitlyHashCheck: false, magnetLinkLeecher: true, fileSize: 5 * 1024 * 1024, seederConnectionDirection: Direction.Incoming);

        // 5MB files will have a 'layers' key as there will be many pieces.
        // Make sure we correctly upgrade BEP52 connections when initiating an outgoing connection
        [Test]
        public async Task DownloadFileInTorrent_V1V2_MagnetLinkWithLayers_SeederOutgoing () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: false, explitlyHashCheck: false, magnetLinkLeecher: true, fileSize: 5 * 1024 * 1024, seederConnectionDirection: Direction.Outgoing);

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
        public async Task WebSeedDownload_V1V2_OneFileWithPadding () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: true, explitlyHashCheck: false, useWebSeedDownload: true);

        [Test]
        public async Task WebSeedDownload_V1V2_TenFilesWithPadding () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: true, explitlyHashCheck: false, nonEmptyFileCount: 10, useWebSeedDownload: true, fileSize: 987_654);

        [Test]
        public async Task WebSeedDownload_V1V2_RetryWebSeeder ()
        {
            _failHttpRequest = true;
            await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid, createEmptyFile: true, explitlyHashCheck: false, useWebSeedDownload: true);
        }

        [Test]
        public async Task WebSeedDownload_V2 () => await CreateAndDownloadTorrent (TorrentType.V2Only, createEmptyFile: true, explitlyHashCheck: false, useWebSeedDownload: true);

        public async Task CreateAndDownloadTorrent (TorrentType torrentType, bool createEmptyFile, bool explitlyHashCheck, int nonEmptyFileCount = 2, bool useWebSeedDownload = false, long fileSize = 5, IPieceWriter writer = null, bool magnetLinkLeecher = false, Direction? seederConnectionDirection = null)
        {
            var emptyFile = new FileInfo (Path.Combine (_seederDir.FullName, "Empty.file"));
            if (createEmptyFile)
                File.WriteAllText (emptyFile.FullName, "");

            int counter = 0;
            var buffer = new byte[16 * 1024];
            var nonEmptyFiles = new List<FileInfo> ();
            for (int i = 0; i < nonEmptyFileCount; i++) {
                var nonEmptyFile = new FileInfo (Path.Combine (_seederDir.FullName, $"NonEmpty{i}.file"));
                var fs = new FileStream (nonEmptyFile.FullName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite, 4096);
                streams.Add (fs);

                long written = 0;
                while (written < fileSize + i) {
                    var toWrite = Math.Min ((fileSize + i) - written, buffer.Length);
                    for (int j = 0; j < toWrite; j++)
                        buffer[j] = (byte) counter++;

                    fs.Write (buffer, 0, (int) toWrite);
                    written += toWrite;
                }

                fs.Flush ();
                nonEmptyFiles.Add (nonEmptyFile);
            }

            var fileSource = new TorrentFileSource (_seederDir.FullName);
            fileSource.TorrentName = _torrentName;
            TorrentCreator torrentCreator = new TorrentCreator (torrentType);
            torrentCreator.Announce = $"http://{new IPEndPoint (LoopbackAddress, _trackerPort)}/announce";
            torrentCreator.PieceLength = PieceLength;
            if (useWebSeedDownload) {
                torrentCreator.GetrightHttpSeeds.Add ($"http://{new IPEndPoint (LoopbackAddress, _webSeedPort)}/{_webSeedPrefix}/");
            }

            var encodedTorrent = await torrentCreator.CreateAsync (fileSource);
            var torrent = Torrent.Load (encodedTorrent);

            var seederIsSeeding = new TaskCompletionSource<bool> ();
            var leecherIsSeeding = new TaskCompletionSource<bool> ();
            var leecherIsReady = new TaskCompletionSource<bool> ();

            var timeout = new CancellationTokenSource (CancellationTimeout);
            timeout.Token.Register (() => { seederIsSeeding.TrySetCanceled (); });
            timeout.Token.Register (() => { leecherIsSeeding.TrySetCanceled (); });
            timeout.Token.Register (() => { leecherIsReady.TrySetCanceled (); });

            EventHandler<TorrentStateChangedEventArgs> seederIsSeedingHandler = (o, e) => {
                if (e.NewState == TorrentState.Seeding)
                    seederIsSeeding.TrySetResult (true);
                if (e.NewState == TorrentState.Downloading)
                    seederIsSeeding.TrySetResult (false);
                if (e.NewState == TorrentState.Error)
                    seederIsSeeding.TrySetException (e.TorrentManager.Error.Exception);
            };

            EventHandler<TorrentStateChangedEventArgs> leecherIsSeedingHandler = (o, e) => {
                if (e.NewState == TorrentState.Downloading || e.NewState == TorrentState.FetchingHashes || e.NewState == TorrentState.Metadata)
                    leecherIsReady.TrySetResult (true);
                if (e.NewState == TorrentState.Seeding)
                    leecherIsSeeding.TrySetResult (true);
                if (e.NewState == TorrentState.Error)
                    leecherIsSeeding.TrySetException (e.TorrentManager.Error.Exception);
            };

            if (seederConnectionDirection.HasValue) {
                var engine = seederConnectionDirection == Direction.Incoming ? leecherEngine : seederEngine;

                var settings = new EngineSettingsBuilder (engine.Settings);
                settings.ListenEndPoints.Clear ();
                settings.ReportedListenEndPoints = new Dictionary<string, IPEndPoint> {
                        // report two fake non-routable addresses.
                        { "ipv4", new IPEndPoint (IPAddress.Parse ("127.0.0.153"), 12345) },
                        { "ipv6", new IPEndPoint (IPAddress.Parse ("127.0.0.153"), 12345) },
                    };
                await engine.UpdateSettingsAsync (settings.ToSettings ());
            }

            var seederManager = !useWebSeedDownload ? await StartTorrent (seederEngine, torrent, _seederDir.FullName, explitlyHashCheck, seederIsSeedingHandler) : null;
            if (seederManager is null)
                seederIsSeeding.TrySetResult (true);

            var magnetLink = new MagnetLink (torrent.InfoHashes, "testing", torrent.AnnounceUrls.SelectMany (t => t).ToList (), null, torrent.Size);
            var leecherManager = magnetLinkLeecher
                ? await StartTorrent (leecherEngine, magnetLink, _leecherDir.FullName, explitlyHashCheck, leecherIsSeedingHandler)
                : await StartTorrent (leecherEngine, torrent, _leecherDir.FullName, explitlyHashCheck, leecherIsSeedingHandler);

            // Wait for both managers to finish hashing/prepping!
            await seederIsSeeding.Task;
            await leecherIsReady.Task;

            // manually add the leecher to the seeder so we aren't unintentionally dependent on annouce ordering
            if (seederConnectionDirection == Direction.Incoming) {
                var listenerPort = seederEngine.PeerListeners.Single ().LocalEndPoint.Port;
                var ipAddress = new IPEndPoint (LoopbackAddress, listenerPort);
                await leecherEngine.Torrents[0].AddPeerAsync (new PeerInfo (new Uri ($"{(LoopbackAddress.AddressFamily == AddressFamily.InterNetwork ? "ipv4" : "ipv6")}://{ipAddress}")));
            } else if (seederConnectionDirection == Direction.Outgoing) {
                var listenerPort = leecherEngine.PeerListeners.Single ().LocalEndPoint.Port;
                var ipAddress = new IPEndPoint (LoopbackAddress, listenerPort);
                await seederEngine.Torrents[0].AddPeerAsync (new PeerInfo (new Uri ($"{(LoopbackAddress.AddressFamily == AddressFamily.InterNetwork ? "ipv4" : "ipv6")}://{ipAddress}")));
            }

            if (!useWebSeedDownload) {
                Assert.DoesNotThrowAsync (async () => await seederIsSeeding.Task, "Seeder should be seeding after hashcheck completes");
                Assert.True (seederManager.Complete, "Seeder should have all data");
            }
            Assert.DoesNotThrowAsync (async () => await leecherIsSeeding.Task, "Leecher should have downloaded all data");

            foreach (var file in nonEmptyFiles) {
                var leecherNonEmptyFile = new FileInfo (Path.Combine (_leecherDir.FullName, file.Name));
                Assert.IsTrue (leecherNonEmptyFile.Exists, $"Non empty file {file.Name} should exist");
            }

            var leecherEmptyFile = new FileInfo (Path.Combine (_leecherDir.FullName, emptyFile.Name));
            Assert.AreEqual (createEmptyFile, leecherEmptyFile.Exists, "Empty file should exist when created");
        }

        private (MonoTorrent.TrackerServer.TrackerServer, ITrackerListener) GetTracker ()
        {
            for (_trackerPort = 4000; _trackerPort < 4100; _trackerPort++) {
                try {
                    var tracker = new MonoTorrent.TrackerServer.TrackerServer ();
                    tracker.AllowUnregisteredTorrents = true;
                    var listenAddress = $"http://{new IPEndPoint (LoopbackAddress, _trackerPort)}/";

                    var listener = TrackerListenerFactory.CreateHttp (listenAddress);
                    listener.Start ();
                    tracker.RegisterListener (listener);
                    return (tracker, listener);
                } catch (Exception ex) {
                    Console.WriteLine ("Couldn't get a tracker port for integration tests:");
                    Console.WriteLine (ex);
                    continue;
                }
            }
            throw new Exception ("No ports were free?");
        }

        private ClientEngine GetEngine (int port, Factories factories)
        {
            // Give an example of how settings can be modified for the engine.
            var type = AnyAddress.AddressFamily == AddressFamily.InterNetwork ? "ipv4" : "ipv6";
            var settingBuilder = new EngineSettingsBuilder {
                // Use a fixed port to accept incoming connections from other peers for testing purposes. Production usages should use a random port, 0, if possible.
                ListenEndPoints = new Dictionary<string, IPEndPoint> { { type, new IPEndPoint (AnyAddress, port) } },
                ReportedListenEndPoints = new Dictionary<string, IPEndPoint> { { type, new IPEndPoint (LoopbackAddress, 0) } },
                AutoSaveLoadFastResume = false,
                CacheDirectory = _directory.FullName,
                DhtEndPoint = null,
                AllowPortForwarding = false,
                WebSeedDelay = TimeSpan.Zero,
                AllowLocalPeerDiscovery = false,
            };
            var engine = new ClientEngine (settingBuilder.ToSettings (), factories);
            return engine;
        }

        private HttpListener CreateWebSeeder ()
        {
            for (_webSeedPort = 5000; _webSeedPort < 5100; _webSeedPort++) {
                try {
                    HttpListener listener = new HttpListener ();
                    listener.Prefixes.Add ($"http://{new IPEndPoint (LoopbackAddress, _webSeedPort)}/");
                    listener.Start ();
                    listener.BeginGetContext (OnHttpContext, listener);
                    return listener;
                } catch (Exception ex) {
                    Console.WriteLine ("Couldn't get a port for integration tests:");
                    Console.WriteLine (ex);
                }
            }
            throw new Exception ("No ports were free?");
        }

        private void OnHttpContext (IAsyncResult ar)
        {
            if (!_httpSeeder.IsListening)
                return;

            HttpListenerContext ctx;

            try {
                ctx = _httpSeeder.EndGetContext (ar);
                _httpSeeder.BeginGetContext (OnHttpContext, ar.AsyncState);


                var localPath = ctx.Request.Url.LocalPath;
                string relativeSeedingPath = $"/{_webSeedPrefix}/{_torrentName}/";
                if (_failHttpRequest) {
                    _failHttpRequest = false;
                    ctx.Response.StatusCode = 500;
                    ctx.Response.Close ();
                } else if (!localPath.Contains (relativeSeedingPath)) {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close ();
                } else {
                    var fileName = localPath.Replace (relativeSeedingPath, string.Empty);
                    var files = _seederDir.GetFiles ();
                    var file = files.FirstOrDefault (x => x.Name == fileName);
                    if (file == null) {
                        ctx.Response.StatusCode = 406;
                        ctx.Response.Close ();
                    } else {
                        using FileStream fs = new FileStream (file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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
                        if (fs.Read (buffer, 0, buffer.Length) == buffer.Length) {
                            ctx.Response.OutputStream.Write (buffer, 0, buffer.Length);
                            ctx.Response.OutputStream.Close ();
                        } else {
                            ctx.Response.StatusCode = 405;
                            ctx.Response.Close ();
                        }
                    }

                }
            } catch {
                // Do nothing!
                return;
            }
        }

        private Task<TorrentManager> StartTorrent (ClientEngine clientEngine, Torrent torrent, string saveDirectory, bool explicitlyHashCheck, EventHandler<TorrentStateChangedEventArgs> handler)
            => StartTorrent (clientEngine, torrent, null, saveDirectory, explicitlyHashCheck, handler);

        private Task<TorrentManager> StartTorrent (ClientEngine clientEngine, MagnetLink torrent, string saveDirectory, bool explicitlyHashCheck, EventHandler<TorrentStateChangedEventArgs> handler)
            => StartTorrent (clientEngine, null, torrent, saveDirectory, explicitlyHashCheck, handler);

        private async Task<TorrentManager> StartTorrent (ClientEngine clientEngine, Torrent torrent, MagnetLink magnetLink, string saveDirectory, bool explicitlyHashCheck, EventHandler<TorrentStateChangedEventArgs> handler)
        {
            TorrentSettingsBuilder torrentSettingsBuilder = new TorrentSettingsBuilder () {
                CreateContainingDirectory = false,
            };

            TorrentManager manager = torrent != null
                ? await clientEngine.AddAsync (torrent, saveDirectory, torrentSettingsBuilder.ToSettings ())
                : await clientEngine.AddAsync (magnetLink, saveDirectory, torrentSettingsBuilder.ToSettings ());

            manager.TorrentStateChanged += handler;
            if (explicitlyHashCheck)
                await manager.HashCheckAsync (true);
            else
                await manager.StartAsync ();

            return manager;
        }
    }
}
