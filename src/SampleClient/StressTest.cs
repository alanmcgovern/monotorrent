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

namespace SampleClient
{
    class NullWriter : IPieceWriter
    {
        public int MaximumOpenFiles => 0;

        public ReusableTask CloseAsync (ITorrentFileInfo file)
        {
            return ReusableTask.CompletedTask;
        }

        public void Dispose ()
        {
        }

        public ReusableTask<bool> ExistsAsync (ITorrentFileInfo file)
        {
            return ReusableTask.FromResult (false);
        }

        public ReusableTask FlushAsync (ITorrentFileInfo file)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask MoveAsync (ITorrentFileInfo file, string fullPath, bool overwrite)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<int> ReadAsync (ITorrentFileInfo file, long offset, Memory<byte> buffer)
        {
            return ReusableTask.FromResult (0);
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask WriteAsync (ITorrentFileInfo file, long offset, ReadOnlyMemory<byte> buffer)
        {
            return ReusableTask.CompletedTask;
        }
    }

    class StressTest
    {
        const int DataSize = 100 * 1024 * 1024 - 1024;
        static string DataDir = Path.GetFullPath ("data_dir");

        public async Task RunAsync ()
        {
            //LoggerFactory.Creator = className => new TextLogger (Console.Out, className);

            int port = 37827;
            var seeder = new ClientEngine (
                new EngineSettingsBuilder {
                    AllowedEncryption = new[] { EncryptionType.PlainText },
                    DiskCacheBytes = DataSize,
                    ListenEndPoint = new IPEndPoint (IPAddress.Any, port++)
                }.ToSettings ()
            );
            await seeder.ChangePieceWriterAsync (new NullWriter ());

            var downloaders = Enumerable.Range (port, 16).Select (p => {
                return new ClientEngine (
                    new EngineSettingsBuilder {
                        AllowedEncryption = new[] { EncryptionType.PlainText },
                        DiskCacheBytes = DataSize,
                        ListenEndPoint = new IPEndPoint (IPAddress.Any, p),
                    }.ToSettings ()
                );
            }).ToArray ();
            foreach (var engine in downloaders)
                await engine.ChangePieceWriterAsync (new NullWriter ());

            Directory.CreateDirectory (DataDir);
            // Generate some fake data on-disk
            var buffer = Enumerable.Range (0, 16 * 1024).Select (s => (byte) s).ToArray ();
            using (var fileStream = File.OpenWrite (Path.Combine (DataDir, "file.data"))) {
                for (int i = 0; i < DataSize / buffer.Length; i++)
                    fileStream.Write (buffer, 0, buffer.Length);
                fileStream.SetLength (DataSize);
            }

            var trackerListener = TrackerListenerFactory.CreateHttp (IPAddress.Parse ("127.0.0.1"), 25611);
            var tracker = new TrackerServer {
                AllowUnregisteredTorrents = true
            };
            tracker.RegisterListener (trackerListener);
            trackerListener.Start ();

            // Create the torrent file for the fake data
            var creator = new TorrentCreator ();
            creator.Announces.Add (new List<string> ());
            creator.Announces[0].Add ("http://127.0.0.1:25611/announce");

            var metadata = await creator.CreateAsync (new TorrentFileSource (DataDir));

            // Set up the seeder
            await seeder.AddAsync (Torrent.Load (metadata), DataDir, new TorrentSettingsBuilder { UploadSlots = 20 }.ToSettings ());
            using (var fileStream = File.OpenRead (Path.Combine (DataDir, "file.data"))) {
                while (fileStream.Position < fileStream.Length) {
                    var dataRead = new byte[16 * 1024];
                    int offset = (int) fileStream.Position;
                    int read = fileStream.Read (dataRead, 0, dataRead.Length);
                    // FIXME: Implement a custom IPieceWriter to handle this.
                    // The internal MemoryWriter is limited and isn't a general purpose read/write API
                    // await seederWriter.WriteAsync (seeder.Torrents[0].Files[0], offset, dataRead, 0, read, false);
                }
            }

            await seeder.StartAllAsync ();

            List<Task> tasks = new List<Task> ();
            for (int i = 0; i < downloaders.Length; i++) {
                await downloaders[i].AddAsync (
                    Torrent.Load (metadata),
                    Path.Combine (DataDir, "Downloader" + i)
                );

                tasks.Add (RepeatDownload (downloaders[i]));
            }

            while (true) {
                long downTotal = seeder.TotalDownloadSpeed;
                long upTotal = seeder.TotalUploadSpeed;
                long totalConnections = 0;
                long dataDown = seeder.Torrents[0].Monitor.DataBytesDownloaded + seeder.Torrents[0].Monitor.ProtocolBytesDownloaded;
                long dataUp = seeder.Torrents[0].Monitor.DataBytesUploaded + seeder.Torrents[0].Monitor.ProtocolBytesUploaded;
                foreach (var engine in downloaders) {
                    downTotal += engine.TotalDownloadSpeed;
                    upTotal += engine.TotalUploadSpeed;

                    dataDown += engine.Torrents[0].Monitor.DataBytesDownloaded + engine.Torrents[0].Monitor.ProtocolBytesDownloaded;
                    dataUp += engine.Torrents[0].Monitor.DataBytesUploaded + engine.Torrents[0].Monitor.ProtocolBytesUploaded;
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
