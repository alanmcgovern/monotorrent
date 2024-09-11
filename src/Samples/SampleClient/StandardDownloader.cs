using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Logging;

namespace ClientSample
{
    class StandardDownloader
    {
        ClientEngine Engine { get; }
        Top10Listener Listener { get; }			// This is a subclass of TraceListener which remembers the last 20 statements sent to it

        public StandardDownloader (ClientEngine engine)
        {
            Engine = engine;
            Listener = new Top10Listener (10);
        }

        public async Task DownloadAsync (CancellationToken token)
        {
            // Torrents will be downloaded to this directory
            var downloadsPath = Path.Combine (Environment.CurrentDirectory, "Downloads");

            // .torrent files will be loaded from this directory (if any exist)
            var torrentsPath = Path.Combine (Environment.CurrentDirectory, "Torrents");

#if DEBUG
            LoggerFactory.Register (new TextWriterLogger (Console.Out));
#endif

            // If the torrentsPath does not exist, we want to create it
            if (!Directory.Exists (torrentsPath))
                Directory.CreateDirectory (torrentsPath);

            // For each file in the torrents path that is a .torrent file, load it into the engine.
            foreach (string file in Directory.GetFiles (torrentsPath)) {
                if (file.EndsWith (".torrent", StringComparison.OrdinalIgnoreCase)) {
                    try {
                        // EngineSettings.AutoSaveLoadFastResume is enabled, so any cached fast resume
                        // data will be implicitly loaded. If fast resume data is found, the 'hash check'
                        // phase of starting a torrent can be skipped.
                        // 
                        // TorrentSettingsBuilder can be used to modify the settings for this
                        // torrent.
                        var settingsBuilder = new TorrentSettingsBuilder {
                            MaximumConnections = 60,
                        };
                        var manager = await Engine.AddAsync (file, downloadsPath, settingsBuilder.ToSettings ());
                        Console.WriteLine (manager.InfoHashes.V1OrV2.ToHex ());
                    } catch (Exception e) {
                        Console.Write ("Couldn't decode {0}: ", file);
                        Console.WriteLine (e.Message);
                    }
                }
            }

            // If we loaded no torrents, just exist. The user can put files in the torrents directory and start
            // the client again
            if (Engine.Torrents.Count == 0) {
                Console.WriteLine ($"No torrents found in '{torrentsPath}'");
                Console.WriteLine ("Exiting...");
                return;
            }

            // For each torrent manager we loaded and stored in our list, hook into the events
            // in the torrent manager and start the engine.
            foreach (TorrentManager manager in Engine.Torrents) {
                manager.PeersFound += (o, e) => {
                    Listener.WriteLine (string.Format ($"{e.GetType ().Name}: {e.NewPeers} peers for {e.TorrentManager.Name}"));
                };
                manager.PeerConnected += (o, e) => {
                    lock (Listener)
                        Listener.WriteLine ($"Connection succeeded: {e.Peer.Uri}");
                };
                manager.ConnectionAttemptFailed += (o, e) => {
                    lock (Listener)
                        Listener.WriteLine (
                            $"Connection failed: {e.Peer.ConnectionUri} - {e.Reason}");
                };
                // Every time a piece is hashed, this is fired.
                manager.PieceHashed += delegate (object o, PieceHashedEventArgs e) {
                    lock (Listener)
                        Listener.WriteLine ($"Piece Hashed: {e.PieceIndex} - {(e.HashPassed ? "Pass" : "Fail")}");
                };

                // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
                manager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e) {
                    lock (Listener)
                        Listener.WriteLine ($"OldState: {e.OldState} NewState: {e.NewState}");
                };

                // Every time the tracker's state changes, this is fired
                manager.TrackerManager.AnnounceComplete += (sender, e) => {
                    Listener.WriteLine ($"{e.Successful}: {e.Tracker}");
                };

                // Start the torrentmanager. The file will then hash (if required) and begin downloading/seeding.
                // As EngineSettings.AutoSaveLoadDhtCache is enabled, any cached data will be loaded into the
                // Dht engine when the first torrent is started, enabling it to bootstrap more rapidly.
                await manager.StartAsync ();
            }

            // While the torrents are still running, print out some stats to the screen.
            // Details for all the loaded torrent managers are shown.
            StringBuilder sb = new StringBuilder (1024);
            while (Engine.IsRunning) {
                sb.Remove (0, sb.Length);

                AppendFormat (sb, $"Transfer Rate:      {Engine.TotalDownloadRate / 1024.0:0.00}kB/sec ↓ / {Engine.TotalUploadRate / 1024.0:0.00}kB/sec ↑");
                AppendFormat (sb, $"Memory Cache:       {Engine.DiskManager.CacheBytesUsed / 1024.0:0.00}/{Engine.Settings.DiskCacheBytes / 1024.0:0.00} kB");
                AppendFormat (sb, $"Disk IO Rate:       {Engine.DiskManager.ReadRate / 1024.0:0.00} kB/s read / {Engine.DiskManager.WriteRate / 1024.0:0.00} kB/s write");
                AppendFormat (sb, $"Disk IO Total:      {Engine.DiskManager.TotalBytesRead / 1024.0:0.00} kB read / {Engine.DiskManager.TotalBytesWritten / 1024.0:0.00} kB written");
                AppendFormat (sb, $"Open Files:         {Engine.DiskManager.OpenFiles} / {Engine.DiskManager.MaximumOpenFiles}");
                AppendFormat (sb, $"Open Connections:   {Engine.ConnectionManager.OpenConnections}");
                AppendFormat (sb, $"DHT State:          {Engine.Dht.State}");

                // Print out the port mappings
                foreach (var mapping in Engine.PortMappings.Created)
                    AppendFormat (sb, $"Successful Mapping    {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
                foreach (var mapping in Engine.PortMappings.Failed)
                    AppendFormat (sb, $"Failed mapping:       {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
                foreach (var mapping in Engine.PortMappings.Pending)
                    AppendFormat (sb, $"Pending mapping:      {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");

                foreach (TorrentManager manager in Engine.Torrents) {
                    AppendSeparator (sb);
                    AppendFormat (sb, $"State:              {manager.State}");
                    AppendFormat (sb, $"Name:               {(manager.Torrent == null ? "MetaDataMode" : manager.Torrent.Name)}");
                    AppendFormat (sb, $"Progress:           {manager.Progress:0.00}");
                    AppendFormat (sb, $"Transferred:        {manager.Monitor.DataBytesReceived / 1024.0 / 1024.0:0.00} MB ↓ / {manager.Monitor.DataBytesSent / 1024.0 / 1024.0:0.00} MB ↑");
                    AppendFormat (sb, $"Tracker Status");
                    foreach (var tier in manager.TrackerManager.Tiers)
                        AppendFormat (sb, $"\t{tier.ActiveTracker} : Announce Succeeded: {tier.LastAnnounceSucceeded}. Scrape Succeeded: {tier.LastScrapeSucceeded}.");

                    if (manager.PieceManager != null)
                        AppendFormat (sb, "Current Requests:   {0}", await manager.PieceManager.CurrentRequestCountAsync ());

                    var peers = await manager.GetPeersAsync ();
                    AppendFormat (sb, "Outgoing:");
                    foreach (PeerId p in peers.Where (t => t.ConnectionDirection == Direction.Outgoing)) {
                        AppendFormat (sb, $"\t{p.AmRequestingPiecesCount} - {(p.Monitor.DownloadRate / 1024.0):0.00}/{(p.Monitor.UploadRate / 1024.0):0.00}kB/sec - {p.Uri} - {p.EncryptionType}");
                    }
                    AppendFormat (sb, "");
                    AppendFormat (sb, "Incoming:");
                    foreach (PeerId p in peers.Where (t => t.ConnectionDirection == Direction.Incoming)) {
                        AppendFormat (sb, $"\t{p.AmRequestingPiecesCount} - {(p.Monitor.DownloadRate / 1024.0):0.00}/{(p.Monitor.UploadRate / 1024.0):0.00}kB/sec - {p.Uri} - {p.EncryptionType}");
                    }

                    AppendFormat (sb, "", null);
                    if (manager.Torrent != null)
                        foreach (var file in manager.Files)
                            AppendFormat (sb, "{1:0.00}% - {0}", file.Path, file.BitField.PercentComplete);
                }
                Console.Clear ();
                Console.WriteLine (sb.ToString ());
                Listener.ExportTo (Console.Out);

                await Task.Delay (5000, token);
            }
        }

        void Manager_PeersFound (object sender, PeersAddedEventArgs e)
        {
            lock (Listener)
                Listener.WriteLine ($"Found {e.NewPeers} new peers and {e.ExistingPeers} existing peers");//throw new Exception("The method or operation is not implemented.");
        }

        void AppendSeparator (StringBuilder sb)
        {
            AppendFormat (sb, "");
            AppendFormat (sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");
            AppendFormat (sb, "");
        }

        void AppendFormat (StringBuilder sb, string str, params object[] formatting)
        {
            if (formatting != null && formatting.Length > 0)
                sb.AppendFormat (str, formatting);
            else
                sb.Append (str);
            sb.AppendLine ();
        }
    }
}
