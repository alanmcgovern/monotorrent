using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Dht;

namespace SampleClient
{
    class MainClass
    {
        static string dhtNodeFile;
        static string basePath;
        static string downloadsPath;
        static string fastResumeFile;
        static string torrentsPath;
        static ClientEngine engine;				// The engine used for downloading
        static List<TorrentManager> torrents;	// The list where all the torrentManagers will be stored that the engine gives us
        static Top10Listener listener;			// This is a subclass of TraceListener which remembers the last 20 statements sent to it

        static void Main (string[] args)
        {
            /* Generate the paths to the folder we will save .torrent files to and where we download files to */
            basePath = Environment.CurrentDirectory;						// This is the directory we are currently in
            torrentsPath = Path.Combine (basePath, "Torrents");				// This is the directory we will save .torrents to
            downloadsPath = Path.Combine (basePath, "Downloads");			// This is the directory we will save downloads to
            fastResumeFile = Path.Combine (torrentsPath, "fastresume.data");
            dhtNodeFile = Path.Combine (basePath, "DhtNodes");
            torrents = new List<TorrentManager> ();							// This is where we will store the torrentmanagers
            listener = new Top10Listener (10);

            // We need to cleanup correctly when the user closes the window by using ctrl-c
            // or an unhandled exception happens
            Console.CancelKeyPress += delegate { Shutdown ().Wait (); };
            AppDomain.CurrentDomain.ProcessExit += delegate { Shutdown ().Wait (); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine (e.ExceptionObject); Shutdown ().Wait (); };
            Thread.GetDomain ().UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine (e.ExceptionObject); Shutdown ().Wait (); };

            StartEngine ().Wait ();
        }

        private static async Task StartEngine ()
        {
            int port;
            Torrent torrent = null;
            // Ask the user what port they want to use for incoming connections
            Console.Write ($"{Environment.NewLine}Choose a listen port: ");
            while (!Int32.TryParse (Console.ReadLine (), out port)) { }

            // Create the settings which the engine will use
            // downloadsPath - this is the path where we will save all the files to
            // port - this is the port we listen for connections on
            EngineSettings engineSettings = new EngineSettings {
                SavePath = downloadsPath,
                ListenPort = port
            };

            //engineSettings.GlobalMaxUploadSpeed = 30 * 1024;
            //engineSettings.GlobalMaxDownloadSpeed = 100 * 1024;
            //engineSettings.MaxReadRate = 1 * 1024 * 1024;

            // Create the default settings which a torrent will have.
            TorrentSettings torrentDefaults = new TorrentSettings ();

            // Create an instance of the engine.
            engine = new ClientEngine (engineSettings);

            byte[] nodes = Array.Empty<byte> ();
            try {
                if (File.Exists (dhtNodeFile))
                    nodes = File.ReadAllBytes (dhtNodeFile);
            } catch {
                Console.WriteLine ("No existing dht nodes could be loaded");
            }

            DhtEngine dht = new DhtEngine (new IPEndPoint (IPAddress.Any, port));
            await engine.RegisterDhtAsync (dht);

            // This starts the Dht engine but does not wait for the full initialization to
            // complete. This is because it can take up to 2 minutes to bootstrap, depending
            // on how many nodes time out when they are contacted.
            await engine.DhtEngine.StartAsync (nodes);

            // If the SavePath does not exist, we want to create it.
            if (!Directory.Exists (engine.Settings.SavePath))
                Directory.CreateDirectory (engine.Settings.SavePath);

            // If the torrentsPath does not exist, we want to create it
            if (!Directory.Exists (torrentsPath))
                Directory.CreateDirectory (torrentsPath);

            BEncodedDictionary fastResume = new BEncodedDictionary ();
            try {
                if (File.Exists (fastResumeFile))
                    fastResume = BEncodedValue.Decode<BEncodedDictionary> (File.ReadAllBytes (fastResumeFile));
            } catch {
            }

            // For each file in the torrents path that is a .torrent file, load it into the engine.
            foreach (string file in Directory.GetFiles (torrentsPath)) {
                if (file.EndsWith (".torrent", StringComparison.OrdinalIgnoreCase)) {
                    try {
                        // Load the .torrent from the file into a Torrent instance
                        // You can use this to do preprocessing should you need to
                        torrent = await Torrent.LoadAsync (file);
                        Console.WriteLine (torrent.InfoHash.ToString ());
                    } catch (Exception e) {
                        Console.Write ("Couldn't decode {0}: ", file);
                        Console.WriteLine (e.Message);
                        continue;
                    }
                    // When any preprocessing has been completed, you create a TorrentManager
                    // which you then register with the engine.
                    TorrentManager manager = new TorrentManager (torrent, downloadsPath, torrentDefaults);
                    if (fastResume.ContainsKey (torrent.InfoHash.ToHex ()))
                        manager.LoadFastResume (new FastResume ((BEncodedDictionary) fastResume[torrent.InfoHash.ToHex ()]));
                    await engine.Register (manager);

                    // Store the torrent manager in our list so we can access it later
                    torrents.Add (manager);
                    manager.PeersFound += Manager_PeersFound;
                }
            }

            // If we loaded no torrents, just exist. The user can put files in the torrents directory and start
            // the client again
            if (torrents.Count == 0) {
                Console.WriteLine ("No torrents found in the Torrents directory");
                Console.WriteLine ("Exiting...");
                engine.Dispose ();
                return;
            }

            // For each torrent manager we loaded and stored in our list, hook into the events
            // in the torrent manager and start the engine.
            foreach (TorrentManager manager in torrents) {
                manager.PeerConnected += (o, e) => {
                    lock (listener)
                        listener.WriteLine ($"Connection succeeded: {e.Peer.Uri}");
                };
                manager.ConnectionAttemptFailed += (o, e) => {
                    lock (listener)
                        listener.WriteLine (
                            $"Connection failed: {e.Peer.ConnectionUri} - {e.Reason} - {e.Peer.AllowedEncryption}");
                };
                // Every time a piece is hashed, this is fired.
                manager.PieceHashed += delegate (object o, PieceHashedEventArgs e) {
                    lock (listener)
                        listener.WriteLine ($"Piece Hashed: {e.PieceIndex} - {(e.HashPassed ? "Pass" : "Fail")}");
                };

                // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
                manager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e) {
                    lock (listener)
                        listener.WriteLine ($"OldState: {e.OldState} NewState: {e.NewState}");
                };

                // Every time the tracker's state changes, this is fired
                manager.TrackerManager.AnnounceComplete += (sender, e) => {
                    listener.WriteLine ($"{e.Successful}: {e.Tracker}");
                };

                // Start the torrentmanager. The file will then hash (if required) and begin downloading/seeding
                await manager.StartAsync ();
            }

            // Enable automatic port forwarding. The engine will use Mono.Nat to search for
            // uPnP or NAT-PMP compatible devices and then issue port forwarding requests to it.
            await engine.EnablePortForwardingAsync (CancellationToken.None);

            // This is how to access the list of port mappings, and to see if they were
            // successful, pending or failed. If they failed it could be because the public port
            // is already in use by another computer on your network.
            foreach (var successfulMapping in engine.PortMappings.Created) { }
            foreach (var failedMapping in engine.PortMappings.Failed) { }
            foreach (var failedMapping in engine.PortMappings.Pending) { }

            // While the torrents are still running, print out some stats to the screen.
            // Details for all the loaded torrent managers are shown.
            int i = 0;
            bool running = true;
            StringBuilder sb = new StringBuilder (1024);
            while (running) {
                if ((i++) % 10 == 0) {
                    sb.Remove (0, sb.Length);
                    running = torrents.Exists (m => m.State != TorrentState.Stopped);

                    AppendFormat (sb, "Total Download Rate: {0:0.00}kB/sec", engine.TotalDownloadSpeed / 1024.0);
                    AppendFormat (sb, "Total Upload Rate:   {0:0.00}kB/sec", engine.TotalUploadSpeed / 1024.0);
                    AppendFormat (sb, "Disk Read Rate:      {0:0.00} kB/s", engine.DiskManager.ReadRate / 1024.0);
                    AppendFormat (sb, "Disk Write Rate:     {0:0.00} kB/s", engine.DiskManager.WriteRate / 1024.0);
                    AppendFormat (sb, "Total Read:         {0:0.00} kB", engine.DiskManager.TotalRead / 1024.0);
                    AppendFormat (sb, "Total Written:      {0:0.00} kB", engine.DiskManager.TotalWritten / 1024.0);
                    AppendFormat (sb, "Open Connections:    {0}", engine.ConnectionManager.OpenConnections);

                    foreach (TorrentManager manager in torrents) {
                        AppendSeparator (sb);
                        AppendFormat (sb, "State:           {0}", manager.State);
                        AppendFormat (sb, "Name:            {0}", manager.Torrent == null ? "MetaDataMode" : manager.Torrent.Name);
                        AppendFormat (sb, "Progress:           {0:0.00}", manager.Progress);
                        AppendFormat (sb, "Download Speed:     {0:0.00} kB/s", manager.Monitor.DownloadSpeed / 1024.0);
                        AppendFormat (sb, "Upload Speed:       {0:0.00} kB/s", manager.Monitor.UploadSpeed / 1024.0);
                        AppendFormat (sb, "Total Downloaded:   {0:0.00} MB", manager.Monitor.DataBytesDownloaded / (1024.0 * 1024.0));
                        AppendFormat (sb, "Total Uploaded:     {0:0.00} MB", manager.Monitor.DataBytesUploaded / (1024.0 * 1024.0));
                        AppendFormat(sb, "Tracker Status");
                        foreach (var tier in manager.TrackerManager.Tiers)
                            AppendFormat (sb, $"\t{tier.ActiveTracker} : Announce Succeeded: {tier.LastAnnounceSucceeded}. Scrape Succeeded: {tier.LastScrapSucceeded}.");
                        if (manager.PieceManager != null)
                            AppendFormat (sb, "Current Requests:   {0}", await manager.PieceManager.CurrentRequestCountAsync ());

                        foreach (PeerId p in await manager.GetPeersAsync ())
                            AppendFormat (sb, "\t{2} - {1:0.00}/{3:0.00}kB/sec - {0}", p.Uri,
                                                                                      p.Monitor.DownloadSpeed / 1024.0,
                                                                                      p.AmRequestingPiecesCount,
                                                                                      p.Monitor.UploadSpeed / 1024.0);

                        AppendFormat (sb, "", null);
                        if (manager.Torrent != null)
                            foreach (TorrentFile file in manager.Torrent.Files)
                                AppendFormat (sb, "{1:0.00}% - {0}", file.Path, file.BitField.PercentComplete);
                    }
                    Console.Clear ();
                    Console.WriteLine (sb.ToString ());
                    listener.ExportTo (Console.Out);
                }

                Thread.Sleep (500);
            }

            // Stop searching for uPnP or NAT-PMP compatible devices and delete
            // all mapppings which had been created.
            await engine.DisablePortForwardingAsync (CancellationToken.None);
        }

        static void Manager_PeersFound (object sender, PeersAddedEventArgs e)
        {
            lock (listener)
                listener.WriteLine ($"Found {e.NewPeers} new peers and {e.ExistingPeers} existing peers");//throw new Exception("The method or operation is not implemented.");
        }

        private static void AppendSeparator (StringBuilder sb)
        {
            AppendFormat (sb, "", null);
            AppendFormat (sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -", null);
            AppendFormat (sb, "", null);
        }

        private static void AppendFormat (StringBuilder sb, string str, params object[] formatting)
        {
            if (formatting != null)
                sb.AppendFormat (str, formatting);
            else
                sb.Append (str);
            sb.AppendLine ();
        }

        private static async Task Shutdown ()
        {
            BEncodedDictionary fastResume = new BEncodedDictionary ();
            for (int i = 0; i < torrents.Count; i++) {
                var stoppingTask = torrents[i].StopAsync ();
                while (torrents[i].State != TorrentState.Stopped) {
                    Console.WriteLine ("{0} is {1}", torrents[i].Torrent.Name, torrents[i].State);
                    Thread.Sleep (250);
                }
                await stoppingTask;

                if (torrents[i].HashChecked)
                    fastResume.Add (torrents[i].Torrent.InfoHash.ToHex (), torrents[i].SaveFastResume ().Encode ());
            }

            var nodes = await engine.DhtEngine.SaveNodesAsync ();
            File.WriteAllBytes (dhtNodeFile, nodes);
            File.WriteAllBytes (fastResumeFile, fastResume.Encode ());
            engine.Dispose ();

            Thread.Sleep (2000);
        }
    }
}
