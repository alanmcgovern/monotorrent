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

using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.Logging;
using MonoTorrent.PieceWriter;
using MonoTorrent.TrackerServer;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.IntegrationTests
{
    [TestFixture]
    public class LargeFiles_FakeIPieceWriter
    {
        class FakePieceWriter : IPieceWriter
        {
            public bool IsSeeder { get; set; }
            public int OpenFiles { get; }
            public int MaximumOpenFiles { get; } = 10;
            public ITorrentInfo TorrentInfo { get; set; }

            public Dictionary<ITorrentManagerFile, BitField> Available = new Dictionary<ITorrentManagerFile, BitField> ();

            public ReusableTask CloseAsync (ITorrentManagerFile file)
            {
                return default;
            }

            public void Dispose ()
            {

            }

            public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
            {
                return ReusableTask.FromResult (true);
            }

            public ReusableTask FlushAsync (ITorrentManagerFile file)
            {
                return default;
            }

            public ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite)
            {
                return default;
            }

            public ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
            {
                if (!Available.TryGetValue (file, out var bf)) {
                    if (IsSeeder)
                        Available[file] = bf = new BitField (file.BitField).SetAll (true);
                    else
                        return ReusableTask.FromResult (0);
                }

                int requestedBlock;
                if (TorrentInfo != null)
                    // When seeding/leeching a torrent we need an accurate calculation which is piece boundary aware
                    requestedBlock = TorrentInfo.ByteOffsetToPieceIndex (file.OffsetInTorrent + offset) - file.StartPieceIndex;
                else
                    // when hashing the files to create the .torrent metadata we can use a simplistic calculation as it's
                    // treated as 'just a bunch of files'
                    requestedBlock = (int) ((file.OffsetInTorrent + offset) / PieceLength) - file.StartPieceIndex;

                if (bf[(int) requestedBlock]) {
                    buffer.Span.Fill ((byte) 'a');
                    return ReusableTask.FromResult (buffer.Length);
                }
                return ReusableTask.FromResult (0);
            }

            public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
            {
                return default;
            }

            public ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
            {
                if (!Available.TryGetValue (file, out var bf))
                    Available[file] = bf = new BitField (file.BitField);
                int writtenBlock;
                if (TorrentInfo != null)
                    // When seeding/leeching a torrent we need an accurate calculation which is piece boundary aware
                    writtenBlock = TorrentInfo.ByteOffsetToPieceIndex (file.OffsetInTorrent + offset) - file.StartPieceIndex;
                else
                    // when hashing the files to create the .torrent metadata we can use a simplistic calculation as it's
                    // treated as 'just a bunch of files'
                    writtenBlock = (int) ((file.OffsetInTorrent + offset) / PieceLength) - file.StartPieceIndex;
                bf[(int) writtenBlock] = true;

                return default;
            }

            public ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file)
            {
                // pretend the file exists but is empty if leeching
                return ReusableTask.FromResult<long?> (IsSeeder ? file.Length : 0);
            }

            public ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions options)
            {
                throw new NotImplementedException ();
            }
        }

        class CustomTorrentFileSource : ITorrentFileSource
        {
            public IEnumerable<FileMapping> Files { get; set; }
            public string TorrentName { get; set; }
        }

        [Test]
        public async Task V1Only () => await CreateAndDownloadTorrent (TorrentType.V1Only);

        [Test]
        public async Task V1V2Hybrid () => await CreateAndDownloadTorrent (TorrentType.V1V2Hybrid);

        [Test]
        public async Task V2Only () => await CreateAndDownloadTorrent (TorrentType.V2Only);

        public static int PieceLength = Constants.BlockSize;
        static readonly TimeSpan CancellationTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds (60);

        public IPAddress AnyAddress { get; }
        public IPAddress LoopbackAddress { get; }

        FakePieceWriter SeederWriter { get; set; }
        FakePieceWriter LeecherWriter { get; set; }

        public LargeFiles_FakeIPieceWriter ()
            => (AnyAddress, LoopbackAddress) = (IPAddress.Any, IPAddress.Loopback);

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            (_tracker, _trackerListener) = GetTracker ();
            _httpSeeder = CreateWebSeeder ();
        }

        [SetUp]
        public void Setup ()
        {
            LoggerFactory.Register (new TextWriterLogger (TestContext.Out));
            _failHttpRequest = false;
            string tempDirectory = Path.Combine (Path.GetTempPath (), $"{NUnit.Framework.TestContext.CurrentContext.Test.Name}-{Path.GetRandomFileName ()}");
            _directory = Directory.CreateDirectory (tempDirectory);
            _seederDir = _directory.CreateSubdirectory ("Seeder");
            _leecherDir = _directory.CreateSubdirectory ("Leecher");

            LeecherWriter = new FakePieceWriter { IsSeeder = false };
            SeederWriter = new FakePieceWriter { IsSeeder = true };

            streams = new List<FileStream> ();
            leecherEngine = GetEngine (0, Factories.Default.WithPieceWriterCreator (t => LeecherWriter));
            seederEngine = GetEngine (0, Factories.Default.WithPieceWriterCreator (t => SeederWriter));
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

        async Task CreateAndDownloadTorrent (TorrentType torrentType)
        {
            var nonEmptyFiles = new List<(string name, long size)> ();
            nonEmptyFiles.Add ((Path.Combine (_seederDir.FullName, $"NonEmpty1.file"), (long) int.MaxValue + 15123L));
            nonEmptyFiles.Add ((Path.Combine (_seederDir.FullName, $"NonEmpty2.file"), (long) int.MaxValue + 25123L));

            var fileSource = new CustomTorrentFileSource {
                Files = nonEmptyFiles.Select (t => {
                    return new FileMapping (t.name, Path.GetFileName (t.name), t.size);
                })
            };
            fileSource.TorrentName = _torrentName;
            TorrentCreator torrentCreator = new TorrentCreator (torrentType, Factories.Default.WithPieceWriterCreator (t => SeederWriter)) {
                PieceLength = PieceLength
            };
            torrentCreator.Announce = $"http://{new IPEndPoint (LoopbackAddress, _trackerPort)}/announce";
            var encodedTorrent = await torrentCreator.CreateAsync (fileSource);
            var torrent = Torrent.Load (encodedTorrent);

            // So we can calculate byte offset -> piece index.
            SeederWriter.TorrentInfo = torrent;
            LeecherWriter.TorrentInfo = torrent;

            var seederIsSeeding = new TaskCompletionSource<bool> ();
            var leecherIsSeeding = new TaskCompletionSource<bool> ();
            EventHandler<TorrentStateChangedEventArgs> seederIsSeedingHandler = (o, e) => {
                if (e.NewState == TorrentState.Seeding)
                    seederIsSeeding.TrySetResult (true);
                else if (e.NewState == TorrentState.Downloading)
                    seederIsSeeding.TrySetResult (false);
                else if (e.NewState == TorrentState.Error)
                    seederIsSeeding.TrySetException (e.TorrentManager.Error.Exception);
            };

            EventHandler<TorrentStateChangedEventArgs> leecherIsSeedingHandler = (o, e) => {
                if (e.NewState == TorrentState.Seeding)
                    leecherIsSeeding.TrySetResult (true);
                else if (e.NewState == TorrentState.Error)
                    leecherIsSeeding.TrySetException (e.TorrentManager.Error.Exception);
            };

            // FastResume it to 100% completion
            var fastResumeComplete = new FastResume (torrent.InfoHashes, new BitField (torrent.PieceCount).SetAll (true), new BitField (torrent.PieceCount));
            var seederManager = await StartTorrent (seederEngine, torrent, _seederDir.FullName, seederIsSeedingHandler, fastResumeComplete);


            // Only download the last few pieces from each file.
            var bf = new BitField (torrent.PieceCount).SetAll (true);
            foreach (var file in torrent.Files) {
                bf[file.EndPieceIndex] = false;
                bf[file.EndPieceIndex - 1] = false;
            }
            // And remove a psuedo random assortment of other pieces from before/after the 2gb mark.
            var random = new Random (12345); // this is reproducible as the seed is hardcoded.
            foreach (var file in torrent.Files) {
                for (int i = 0; i < 14; i++) {
                    bf[random.Next (file.StartPieceIndex, file.StartPieceIndex + 100)] = false;
                    bf[random.Next (file.EndPieceIndex - 100, file.EndPieceIndex)] = false;
                }
            }

            var fastResumeIncomplete = new FastResume (torrent.InfoHashes, bf, new BitField (bf).SetAll (false));
            var leecherManager = await StartTorrent (leecherEngine, torrent, _leecherDir.FullName, leecherIsSeedingHandler, fastResumeIncomplete);

            var timeout = new CancellationTokenSource (CancellationTimeout);
            timeout.Token.Register (() => { seederIsSeeding.TrySetCanceled (); });
            timeout.Token.Register (() => { leecherIsSeeding.TrySetCanceled (); });

            Assert.DoesNotThrowAsync (async () => await seederIsSeeding.Task, "Seeder should be seeding after hashcheck completes");
            Assert.True (seederManager.Complete, "Seeder should have all data");
            Assert.DoesNotThrowAsync (async () => await leecherIsSeeding.Task, "Leecher should have downloaded all data");

            foreach (var file in nonEmptyFiles) {
                Assert.IsTrue (LeecherWriter.Available.Keys.Any (t => t.FullPath == file.name.Replace ("Seeder", "Leecher") && t.BitField.AllTrue));
            }
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

        private async Task<TorrentManager> StartTorrent (ClientEngine clientEngine, Torrent torrent, string saveDirectory, EventHandler<TorrentStateChangedEventArgs> handler, FastResume fastResume = null)
        {
            TorrentSettingsBuilder torrentSettingsBuilder = new TorrentSettingsBuilder () {
                CreateContainingDirectory = false,
            };
            TorrentManager manager = await clientEngine.AddAsync (torrent, saveDirectory, torrentSettingsBuilder.ToSettings ());

            // load up some fastresume if it exists
            if (fastResume != null)
                await manager.LoadFastResumeAsync (fastResume);

            manager.TorrentStateChanged += handler;
            await manager.StartAsync ();
            return manager;
        }
    }
}
