using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.PieceWriter;
using MonoTorrent.TrackerServer;

using ReusableTasks;

namespace ClientSample
{
    class NullWriter : IPieceWriter
    {
        public int OpenFiles => 0;
        public int MaximumOpenFiles => 0;

        public ReusableTask CloseAsync (ITorrentManagerFile file)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions options)
        {
            throw new NotImplementedException ();
        }

        public void Dispose ()
        {
        }

        public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
        {
            return ReusableTask.FromResult (false);
        }

        public ReusableTask FlushAsync (ITorrentManagerFile file)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file)
        {
            throw new NotImplementedException ();
        }

        public ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
        {
            return ReusableTask.FromResult (0);
        }

        public ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length)
        {
            throw new NotImplementedException ();
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            return ReusableTask.CompletedTask;
        }
    }

    class StressTest
    {
        const int DataSize = 100 * 1024 * 1024 - 1024;
        const int MaxDownloaders = 16;
        static string DataDir = Path.GetFullPath ("data_dir");

        class InMemoryCache : IBlockCache
        {
            public long CacheHits { get; }
            public long CacheUsed { get; }
            public long Capacity { get; }
            public CachePolicy Policy { get; }
            public IPieceWriter Writer { get; set; }

#pragma warning disable CS0067
            public event EventHandler<BlockInfo> ReadFromCache;
            public event EventHandler<BlockInfo> ReadThroughCache;
            public event EventHandler<BlockInfo> WrittenToCache;
            public event EventHandler<BlockInfo> WrittenThroughCache;
#pragma warning restore CS0067

            Dictionary<BlockInfo, ReadOnlyMemory<byte>> Cache = new Dictionary<BlockInfo, ReadOnlyMemory<byte>> ();

            public InMemoryCache (Dictionary<BlockInfo, ReadOnlyMemory<byte>> cache)
                => (Cache) = (cache);

            public void Dispose ()
            {
                throw new NotImplementedException ();
            }

            public ReusableTask<bool> ReadAsync (ITorrentManagerInfo torrent, BlockInfo block, Memory<byte> buffer)
            {
                Cache[block].CopyTo (buffer);
                return ReusableTask.FromResult (true);
            }

            public ReusableTask<bool> ReadFromCacheAsync (ITorrentManagerInfo torrent, BlockInfo block, Memory<byte> buffer)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask SetCapacityAsync (long capacity)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask SetPolicyAsync (CachePolicy policy)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask SetWriterAsync (IPieceWriter writer)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask WriteAsync (ITorrentManagerInfo torrent, BlockInfo block, Memory<byte> buffer, bool preferSkipCache)
            {
                throw new NotImplementedException ();
            }
        }

        InMemoryCache SeederDataCache { get; set; }

        public async Task RunAsync ()
        {
            //LoggerFactory.Creator = className => new TextLogger (Console.Out, className);

            Directory.CreateDirectory (DataDir);
            // Generate some fake data on-disk
            var buffer = Enumerable.Range (0, 16 * 1024).Select (s => (byte) s).ToArray ();
            using (var fileStream = File.OpenWrite (Path.Combine (DataDir, "file.data"))) {
                for (int i = 0; i < DataSize / buffer.Length; i++)
                    fileStream.Write (buffer, 0, buffer.Length);
                fileStream.SetLength (DataSize);
            }


            // Create the torrent file for the fake data
            var creator = new TorrentCreator (TorrentType.V1Only);
            creator.Announces.Add (new List<string> ());
            creator.Announces[0].Add ("http://127.0.0.1:25611/announce");

            var metadata = await creator.CreateAsync (new TorrentFileSource (Path.Combine (DataDir, "file.data")));

            // Set up the seeder's memory cache
            var allBytes = File.ReadAllBytes (Path.Combine (DataDir, "file.data")).AsMemory ();
            var data = new Dictionary<BlockInfo, ReadOnlyMemory<byte>> ();
            for (int offset = 0; offset < allBytes.Length; offset += Constants.BlockSize) {
                int read = Math.Min (allBytes.Length - offset, Constants.BlockSize);
                data.Add (new BlockInfo (offset / creator.PieceLength, (offset % creator.PieceLength), read), allBytes.Slice (offset, read));
            }
            SeederDataCache = new InMemoryCache (data);

            // Start the tracker.
            var trackerListener = TrackerListenerFactory.CreateHttp (IPAddress.Parse ("127.0.0.1"), 25611);
            var tracker = new TrackerServer {
                AllowUnregisteredTorrents = true
            };
            tracker.RegisterListener (trackerListener);
            trackerListener.Start ();


            // Now create/start the seeder.
            int port = 37000;
            var seeder = new ClientEngine (
                new EngineSettingsBuilder {
                    AllowedEncryption = new List<EncryptionType> { EncryptionType.PlainText },
                    ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Any, port++) } },
                    DhtEndPoint = null,
                    AllowLocalPeerDiscovery = false,
                }.ToSettings (),
                Factories.Default.WithBlockCacheCreator ((IPieceWriter writer, long capacity, CachePolicy policy, MemoryPool buffer) => {
                    SeederDataCache.Writer = writer;
                    return SeederDataCache;
                })
            );
            ;
            await seeder.AddAsync (Torrent.Load (metadata), DataDir, new TorrentSettingsBuilder { UploadSlots = MaxDownloaders }.ToSettings ());
            await seeder.Torrents[0].HashCheckAsync (false);
            await seeder.StartAllAsync ();
            await Task.Delay (500);

            // Now create/start the leechers
            var downloaders = Enumerable.Range (port, MaxDownloaders).Select (p => {
                return new ClientEngine (
                    new EngineSettingsBuilder {
                        AllowedEncryption = new List<EncryptionType> { EncryptionType.PlainText },
                        DiskCacheBytes = DataSize,
                        ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Any, p) } },
                        DhtEndPoint = null,
                        AllowLocalPeerDiscovery = false,
                        CacheDirectory = Path.Combine (DataDir, "Downloader_" + port + "_CacheDirectory")
                    }.ToSettings (),
                    Factories.Default.WithPieceWriterCreator (maxOpenFiles => new NullWriter ())
                );
                ;
            }).ToArray ();

            List<Task> tasks = new List<Task> ();
            for (int i = 0; i < downloaders.Length; i++) {
                await downloaders[i].AddAsync (
                    Torrent.Load (metadata),
                    Path.Combine (DataDir, "Downloader" + i)
                );

                tasks.Add (RepeatDownload (downloaders[i]));
            }

            while (true) {
                long downTotal = seeder.TotalDownloadRate;
                long upTotal = seeder.TotalUploadRate;
                long totalConnections = 0;
                long dataDown = seeder.Torrents[0].Monitor.DataBytesReceived + seeder.Torrents[0].Monitor.ProtocolBytesReceived;
                long dataUp = seeder.Torrents[0].Monitor.DataBytesSent + seeder.Torrents[0].Monitor.ProtocolBytesSent;
                foreach (var engine in downloaders) {
                    downTotal += engine.TotalDownloadRate;
                    upTotal += engine.TotalUploadRate;

                    dataDown += engine.Torrents[0].Monitor.DataBytesReceived + engine.Torrents[0].Monitor.ProtocolBytesReceived;
                    dataUp += engine.Torrents[0].Monitor.DataBytesSent + engine.Torrents[0].Monitor.ProtocolBytesSent;
                    totalConnections += engine.ConnectionManager.OpenConnections;
                }
                Console.Clear ();
                Console.WriteLine ($"Speed Down:        {downTotal / 1024 / 1024}MB.");
                Console.WriteLine ($"Speed Up:          {upTotal / 1024 / 1024}MB.");
                Console.WriteLine ($"Data Down:          {dataDown / 1024 / 1024}MB.");
                Console.WriteLine ($"Data Up:            {dataUp / 1024 / 1024}MB.");

                Console.WriteLine ($"Total Connections: {totalConnections}");
                await Task.Delay (3000);
            }
        }


        async Task RepeatDownload (ClientEngine engine)
        {
            await engine.StartAllAsync ();

            var manager = engine.Torrents[0];
            while (true) {
                if (manager.State == TorrentState.Seeding) {
                    Console.WriteLine ("Download complete");
                    await manager.StopAsync ();
                    await manager.HashCheckAsync (true);
                    Console.WriteLine ("Hash check complete. Progress {0:00.0%}", manager.Progress / 100);
                } else {
                    //Console.WriteLine ("Downloading: {0} / {1:00.0%}", manager.SavePath, manager.Progress / 100);
                    await Task.Delay (2000);
                }
            }
        }
    }
}
