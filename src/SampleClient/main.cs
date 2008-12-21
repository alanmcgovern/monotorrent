using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Client;
using System.Net;
using System.Diagnostics;
using System.Threading;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent
{
    class main
    {
        static string dhtNodeFile;
        static string basePath;
        static string downloadsPath;
        static string fastResumeFile;
        static string torrentsPath;
        static ClientEngine engine;				// The engine used for downloading
        static List<TorrentManager> torrents;	// The list where all the torrentManagers will be stored that the engine gives us
        static Top10Listener listener;			// This is a subclass of TraceListener which remembers the last 20 statements sent to it

        static void Main(string[] args)
        {
            /* Generate the paths to the folder we will save .torrent files to and where we download files to */
            basePath = Environment.CurrentDirectory;						// This is the directory we are currently in
            torrentsPath = Path.Combine(basePath, "Torrents");				// This is the directory we will save .torrents to
            downloadsPath = Path.Combine(basePath, "Downloads");			// This is the directory we will save downloads to
            fastResumeFile = Path.Combine(torrentsPath, "fastresume.data");
            dhtNodeFile = Path.Combine(basePath, "DhtNodes");
            torrents = new List<TorrentManager>();							// This is where we will store the torrentmanagers
            listener = new Top10Listener(10);

            // We need to cleanup correctly when the user closes the window by using ctrl-c
            // or an unhandled exception happens
            Console.CancelKeyPress += delegate { shutdown(); };
            AppDomain.CurrentDomain.ProcessExit += delegate { shutdown(); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); shutdown(); };
            Thread.GetDomain().UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); shutdown(); };

            StartEngine();
        }

        private static void StartEngine()
        {
            int port;
            Torrent torrent = null;
            // Ask the user what port they want to use for incoming connections
            Console.Write(Environment.NewLine + "Choose a listen port: ");
            while (!Int32.TryParse(Console.ReadLine(), out port)) { }



            // Create the settings which the engine will use
            // downloadsPath - this is the path where we will save all the files to
            // port - this is the port we listen for connections on
            EngineSettings engineSettings = new EngineSettings(downloadsPath, port);
            engineSettings.PreferEncryption = false;
            engineSettings.AllowedEncryption = EncryptionTypes.All;

            //engineSettings.GlobalMaxUploadSpeed = 30 * 1024;
            //engineSettings.GlobalMaxDownloadSpeed = 100 * 1024;
            //engineSettings.MaxReadRate = 1 * 1024 * 1024;


            // Create the default settings which a torrent will have.
            // 4 Upload slots - a good ratio is one slot per 5kB of upload speed
            // 50 open connections - should never really need to be changed
            // Unlimited download speed - valid range from 0 -> int.Max
            // Unlimited upload speed - valid range from 0 -> int.Max
            TorrentSettings torrentDefaults = new TorrentSettings(4, 150, 0, 0);

            // Create an instance of the engine.
            engine = new ClientEngine(engineSettings);
            engine.ChangeListenEndpoint(new IPEndPoint(IPAddress.Loopback, port));
            byte[] nodes = null;
            try
            {
                nodes = File.ReadAllBytes(dhtNodeFile);
            }
            catch
            {
                Console.WriteLine("No existing dht nodes could be loaded");
            }

            engine.DhtEngine.Start(nodes);

            // If the SavePath does not exist, we want to create it.
            if (!Directory.Exists(engine.Settings.SavePath))
                Directory.CreateDirectory(engine.Settings.SavePath);

            // If the torrentsPath does not exist, we want to create it
            if (!Directory.Exists(torrentsPath))
                Directory.CreateDirectory(torrentsPath);

            BEncodedDictionary fastResume;
            try
            {
                fastResume = BEncodedValue.Decode<BEncodedDictionary>(File.ReadAllBytes(fastResumeFile));
            }
            catch
            {
                fastResume = new BEncodedDictionary();
            }

            // For each file in the torrents path that is a .torrent file, load it into the engine.
            foreach (string file in Directory.GetFiles(torrentsPath))
            {
                if (file.EndsWith(".torrent"))
                {
                    try
                    {
                        // Load the .torrent from the file into a Torrent instance
                        // You can use this to do preprocessing should you need to
                        torrent = Torrent.Load(file);
                    }
                    catch (Exception e)
                    {
                        Console.Write("Couldn't decode {0}: ", file);
                        Console.WriteLine(e.Message);
                        continue;
                    }
                    // When any preprocessing has been completed, you create a TorrentManager
                    // which you then register with the engine.
                    TorrentManager manager;
                    if (fastResume.ContainsKey(torrent.InfoHash))
                        manager = new TorrentManager(torrent, downloadsPath, torrentDefaults, new FastResume((BEncodedDictionary)fastResume[torrent.InfoHash]));
                    else
                        manager = new TorrentManager(torrent, downloadsPath, torrentDefaults);
                    engine.Register(manager);

                    // Store the torrent manager in our list so we can access it later
                    torrents.Add(manager);
                    manager.PeersFound += new EventHandler<PeersAddedEventArgs>(manager_PeersFound);
                }
            }

            // If we loaded no torrents, just exist. The user can put files in the torrents directory and start
            // the client again
            if (torrents.Count == 0)
            {
                Console.WriteLine("No torrents found in the Torrents directory");
                Console.WriteLine("Exiting...");
                engine.Dispose();
                return;
            }

            // For each torrent manager we loaded and stored in our list, hook into the events
            // in the torrent manager and start the engine.
            foreach (TorrentManager manager in torrents)
            {
                // Every time a piece is hashed, this is fired.
                manager.PieceHashed += delegate(object o, PieceHashedEventArgs e) {
                    lock (listener)
                        listener.WriteLine(string.Format("Piece Hashed: {0} - {1}", e.PieceIndex, e.HashPassed ? "Pass" : "Fail"));
                };

                // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
                manager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e) {
                    lock (listener)
                        listener.WriteLine("OldState: " + e.OldState.ToString() + " NewState: " + e.NewState.ToString());
                };

                // Every time the tracker's state changes, this is fired
                foreach (TrackerTier tier in manager.TrackerManager.TrackerTiers)
                {
                    foreach (MonoTorrent.Client.Tracker.Tracker t in tier.Trackers)
                    {
                        t.AnnounceComplete += delegate(object sender, AnnounceResponseEventArgs e) {
                            listener.WriteLine(string.Format("{0}: {1}", e.Successful, e.Tracker.ToString()));
                        };
                    }
                }
                // Start the torrentmanager. The file will then hash (if required) and begin downloading/seeding
                manager.Start();
            }

            // While the torrents are still running, print out some stats to the screen.
            // Details for all the loaded torrent managers are shown.
            int i = 0;
            bool running = true;
            StringBuilder sb = new StringBuilder(1024);
            while (running)
            {
                if ((i++) % 10 == 0)
                {
                    sb.Remove(0, sb.Length);
                    running = torrents.Exists(delegate(TorrentManager m) { return m.State != TorrentState.Stopped; });

                    AppendFormat(sb, "Total Download Rate: {0:0.00}kB/sec", engine.TotalDownloadSpeed / 1024.0);
                    AppendFormat(sb, "Total Upload Rate:   {0:0.00}kB/sec", engine.TotalUploadSpeed / 1024.0);
                    AppendFormat(sb, "Disk Read Rate:      {0:0.00} kB/s", engine.DiskManager.ReadRate / 1024.0);
                    AppendFormat(sb, "Disk Write Rate:     {0:0.00} kB/s", engine.DiskManager.WriteRate / 1024.0);
                    AppendFormat(sb, "Total Read:         {0:0.00} kB", engine.DiskManager.TotalRead / 1024.0);
                    AppendFormat(sb, "Total Written:      {0:0.00} kB", engine.DiskManager.TotalWritten / 1024.0);
                    AppendFormat(sb, "Open Connections:    {0}", engine.ConnectionManager.OpenConnections);
                    
                    foreach (TorrentManager manager in torrents)
                    {
                        AppendSeperator(sb);

                        AppendFormat(sb, "Name:            {0}", manager.Torrent.Name);
                        AppendFormat(sb, "Progress:           {0:0.00}", manager.Progress);
                        AppendFormat(sb, "Download Speed:     {0:0.00} kB/s", manager.Monitor.DownloadSpeed / 1024.0);
                        AppendFormat(sb, "Upload Speed:       {0:0.00} kB/s", manager.Monitor.UploadSpeed / 1024.0);
                        AppendFormat(sb, "Total Downloaded:   {0:0.00} MB", manager.Monitor.DataBytesDownloaded / (1024.0 * 1024.0));
                        AppendFormat(sb, "Total Uploaded:     {0:0.00} MB", manager.Monitor.DataBytesUploaded / (1024.0 * 1024.0));
                        AppendFormat(sb, "Tracker Status:     {0}", manager.TrackerManager.CurrentTracker.State);
                        AppendFormat(sb, "Warning Message:    {0}", manager.TrackerManager.CurrentTracker.WarningMessage);
                        AppendFormat(sb, "Failure Message:    {0}", manager.TrackerManager.CurrentTracker.FailureMessage);
                    }
                    Console.Clear();
                    Console.WriteLine(sb.ToString());
                    listener.ExportTo(Console.Out);
                }

                System.Threading.Thread.Sleep(500);
            }
        }

        static void manager_PeersFound(object sender, PeersAddedEventArgs e)
        {
            lock (listener)
                listener.WriteLine(string.Format("Found {0} new peers and {1} existing peers", e.NewPeers, e.ExistingPeers ));//throw new Exception("The method or operation is not implemented.");
        }

        private static void AppendSeperator(StringBuilder sb)
        {
            AppendFormat(sb, "", null);
            AppendFormat(sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -", null);
            AppendFormat(sb, "", null);
        }
		private static void AppendFormat(StringBuilder sb, string str, params object[] formatting)
		{
            if (formatting != null)
                sb.AppendFormat(str, formatting);
            else
                sb.Append(str);
			sb.AppendLine();
		}

		private static void shutdown()
		{
            BEncodedDictionary fastResume = new BEncodedDictionary();
            for (int i = 0; i < torrents.Count; i++)
            {
                WaitHandle handle = torrents[i].Stop(); ;
                while (!handle.WaitOne(10, true))
                    Console.WriteLine(handle.ToString());

                Console.WriteLine(handle.ToString());
                fastResume.Add(torrents[i].Torrent.InfoHash, torrents[i].SaveFastResume().Encode());
            }

            File.WriteAllBytes(dhtNodeFile, engine.DhtEngine.SaveNodes());
            File.WriteAllBytes(fastResumeFile, fastResume.Encode());
            engine.Dispose();

			foreach (TraceListener lst in Debug.Listeners)
			{
				lst.Flush();
				lst.Close();
			}

            System.Threading.Thread.Sleep(2000);
		}
	}
}
