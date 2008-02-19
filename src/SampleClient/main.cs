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
        static string basePath;
        static string downloadsPath;
        static string torrentsPath;
        static ClientEngine engine;				// The engine used for downloading
        static List<TorrentManager> torrents;	// The list where all the torrentManagers will be stored that the engine gives us
        static Top10Listener listener;			// This is a subclass of TraceListener which remembers the last 20 statements sent to it
        static List<PeerId> peers;

        static void Main(string[] args)
        {
            //new SampleClient.TestManualConnection();
			/* Generate the paths to the folder we will save .torrent files to and where we download files to */
            basePath = Environment.CurrentDirectory;						// This is the directory we are currently in
            torrentsPath = Path.Combine(basePath, "Torrents");				// This is the directory we will save .torrents to
            downloadsPath = Path.Combine(basePath, "Downloads");			// This is the directory we will save downloads to
			torrents = new List<TorrentManager>();							// This is where we will store the torrentmanagers
            peers = new List<PeerId>();
			// We need to cleanup correctly when the user closes the window by using ctrl-c.
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);


			// If an unhandled exception occurs, we want to be able to clean up correctly, then allow the code to crash
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Thread.GetDomain().UnhandledException += new UnhandledExceptionEventHandler(UnhandledException);
            listener = new Top10Listener(25);
            try
            {
                StartEngine();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }
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



            // Create the default settings which a torrent will have.
            // 4 Upload slots - a good ratio is one slot per 5kB of upload speed
            // 50 open connections - should never really need to be changed
            // Unlimited download speed - valid range from 0 -> int.Max
            // Unlimited upload speed - valid range from 0 -> int.Max
            TorrentSettings torrentDefaults = new TorrentSettings(4, 150, 0, 25 * 1024);

            // Create an instance of the engine.
            engine = new ClientEngine(engineSettings);

            // If the SavePath does not exist, we want to create it.
            if (!Directory.Exists(engine.Settings.SavePath))
                Directory.CreateDirectory(engine.Settings.SavePath);

            // If the torrentsPath does not exist, we want to create it
            if (!Directory.Exists(torrentsPath))
                Directory.CreateDirectory(torrentsPath);


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
                    TorrentManager manager = new TorrentManager(torrent, downloadsPath, torrentDefaults);
                    engine.Register(manager);

                    // Store the torrent manager in our list so we can access it later
                    torrents.Add(manager);
                }
            }

            // If we loaded no torrents, just exist. The user can put files in the torrents directory and start
            // the client again
            if (torrents.Count == 0)
            {
                Console.WriteLine("No torrents found in the Torrents directory");
                Console.WriteLine("Exiting...");
                return;
            }

			// Every time a message is transferred from us to a peer, or from a peer to us, this is fired
			engine.ConnectionManager.PeerMessageTransferred += new EventHandler<PeerMessageEventArgs>(ConnectionManager_PeerMessageTransferred);
			
            // For each torrent manager we loaded and stored in our list, hook into the events
            // in the torrent manager and start the engine.
            foreach (TorrentManager manager in torrents)
            {
                // Every time a piece is hashed, this is fired.
                manager.PieceHashed += new EventHandler<PieceHashedEventArgs>(main_OnPieceHashed);

                // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
                manager.TorrentStateChanged += new EventHandler<TorrentStateChangedEventArgs>(main_OnTorrentStateChanged);

                // Every time a peer connects, this is fired
                manager.PeerConnected += new EventHandler<PeerConnectionEventArgs>(ConnectionManager_PeerConnected);

                // Every time a peer disconnects, this is fired.
                manager.PeerDisconnected += new EventHandler<PeerConnectionEventArgs>(ConnectionManager_PeerDisconnected);

                // Every time the tracker's state changes, this is fired
                foreach(TrackerTier tier in manager.TrackerManager.TrackerTiers)
                    foreach (MonoTorrent.Client.Tracker.Tracker t in tier.Trackers)
                    {
                        t.AnnounceComplete += delegate(object sender, AnnounceResponseEventArgs e) {
                            Console.WriteLine("{0}: {1}", e.Successful, e.Tracker.ToString());
                        };
                        t.StateChanged += new EventHandler<TrackerStateChangedEventArgs>(TrackerManager_OnTrackerStateChange);
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
                    running = false;
                    foreach (TorrentManager manager in torrents)
                    {
                        if (manager.State != TorrentState.Stopped)
                            running = true;
                        sb.Remove(0, sb.Length);
						AppendFormat(sb, "Torrent:            {0}", manager.Torrent.Name);
                        AppendFormat(sb, "Progress:           {0:0.00}", manager.Progress);
						AppendFormat(sb, "Download Speed:     {0:0.00} kB/s", manager.Monitor.DownloadSpeed / 1024.0);
						AppendFormat(sb, "Upload Speed:       {0:0.00} kB/s", manager.Monitor.UploadSpeed / 1024.0);
						AppendFormat(sb, "Total Downloaded:   {0:0.00} MB", manager.Monitor.DataBytesDownloaded / (1024.0 * 1024.0));
						AppendFormat(sb, "Total Uploaded:     {0:0.00} MB", manager.Monitor.DataBytesUploaded / (1024.0 * 1024.0));
						AppendFormat(sb, "Read Rate:          {0:0.00} kB/s", engine.DiskManager.ReadRate);
						AppendFormat(sb, "Write Rate:         {0:0.00} kB/s", engine.DiskManager.WriteRate);
						AppendFormat(sb, "Total Read:         {0:0.00} kB", engine.DiskManager.TotalRead);
						AppendFormat(sb, "Total Written:      {0:0.00} kB", engine.DiskManager.TotalWritten);
						AppendFormat(sb, "Torrent State:      {0}", manager.State);
						AppendFormat(sb, "Number of seeds:    {0}", manager.Peers.Seeds);
						AppendFormat(sb, "Number of leechs:   {0}", manager.Peers.Leechs);
						AppendFormat(sb, "Total available:    {0}", manager.Peers.Available);
						AppendFormat(sb, "Actively connected: {0}", manager.OpenConnections);
						AppendFormat(sb, "Tracker Status:     {0}", manager.TrackerManager.CurrentTracker.State);
						AppendFormat(sb, "Warning Message:    {0}", manager.TrackerManager.TrackerTiers[0].Trackers[0].WarningMessage);
						AppendFormat(sb, "Failure Message:    {0}", manager.TrackerManager.TrackerTiers[0].Trackers[0].FailureMessage);


                        //int areSeeders=0;
                        //int amInterested = 0;
                        //int amRequestingPieces = 0;
                        //int areChokingAndInteresting = 0;
                        //int count = 0;
                        //int isntChoking = 0;
                        //double averagePercent = 0;
                        //double maxPercent = 0;
                        //double seedernotchoking = 0;
                        //int areInterested = 0;
                        //int amUnchoking = 0;
                        //int areRequestingCount = 0;
                        //int sendQueueLength = 0;
                        //lock (peers)
                        //{
                        //    foreach (PeerId p in peers)
                        //    {
                        //        if (!p.IsValid) // If it's not valid, it means the peer has been disconnected. This reference should be dropped
                        //            continue;

                        //        count++;
                        //        if (p.IsSeeder && !p.IsChoking)
                        //            seedernotchoking++;
                        //        areSeeders += p.IsSeeder ? 1 : 0;
                        //        amInterested += p.AmInterested ? 1 : 0;
                        //        isntChoking += !p.IsChoking ? 1 : 0;
                        //        areChokingAndInteresting += (p.IsChoking && p.AmInterested) ? 1 : 0;
                        //        amRequestingPieces += p.AmRequestingPiecesCount;
                        //        averagePercent += p.Bitfield.PercentComplete;
                        //        maxPercent = !p.IsChoking ? Math.Max(maxPercent, p.Bitfield.PercentComplete) : maxPercent;
                        //        areInterested += p.IsInterested ? 1 : 0;
                        //        amUnchoking += !p.AmChoking ? 1 : 0;
                        //        areRequestingCount += p.IsRequestingPiecesCount;
                        //    }
                        //}
                        //averagePercent /= count;

                        //sb.AppendLine("Are seeders:               " + areSeeders.ToString());
                        //sb.AppendLine("Am interested:             " + amInterested.ToString());
                        //sb.AppendLine("Am requesting:             " + amRequestingPieces.ToString());
                        //sb.AppendLine("Is choking+Interesting:    " + areChokingAndInteresting.ToString());
                        //sb.AppendLine("Unchoked me:               " + isntChoking.ToString());
                        //sb.AppendLine("Unchoked me and seeder:    " + seedernotchoking.ToString());
                        //sb.AppendLine("Are interested:            " + areInterested.ToString());
                        //sb.AppendLine("Am unchoking:              " + amUnchoking.ToString());
                        //sb.AppendLine("Are requesting:            " + areRequestingCount.ToString());
                        //sb.AppendLine("Send queue length:         " + sendQueueLength.ToString());
                        //sb.AppendLine("Average send queue length: " + (sendQueueLength / (float)count).ToString());
                        //sb.AppendLine("Count:                     " + count.ToString());
                        //sb.AppendLine("Average:                   " + averagePercent.ToString());
                        //sb.AppendLine("Max:                       " + maxPercent.ToString());

                        // These are some of the other statistics which can be displayed. There are loads more available ;)

                        //sb.Append("Uploading to:     "); sb.Append(manager.UploadingTo);
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Half opens:       "); sb.Append(ClientEngine.ConnectionManager.HalfOpenConnections);
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Max open:         "); sb.Append(ClientEngine.ConnectionManager.MaxOpenConnections);
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Protocol Download:"); sb.AppendFormat("{0:0.00}", manager.Monitor.ProtocolBytesDownloaded / 1024.0);
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Protocol Upload:  "); sb.AppendFormat("{0:0.00}", manager.Monitor.ProtocolBytesUploaded / 1024.0);
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Hashfails:        "); sb.Append(manager.HashFails.ToString());
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Scrape complete:  "); sb.Append(manager.TrackerManager.TrackerTiers[0].Trackers[0].Complete);
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Scrape incomplete:"); sb.Append(manager.TrackerManager.TrackerTiers[0].Trackers[0].Incomplete);
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Scrape downloaded:"); sb.Append(manager.TrackerManager.TrackerTiers[0].Trackers[0].Downloaded);
                        //sb.Append(Environment.NewLine);
                        //sb.Append("Endgame Mode:     "); sb.Append(manager.PieceManager.InEndGameMode);
                        //sb.Append(Environment.NewLine);
                    }
                    Console.Clear();
                    Console.WriteLine(sb.ToString());
                }

                System.Threading.Thread.Sleep(500);
            }

            for (int l = 0; l < torrents.Count; l++)
                torrents[l].Stop().WaitOne();
            engine.Dispose();
        }

		private static void AppendFormat(StringBuilder sb, string str, params object[] formatting)
		{
			sb.AppendFormat(str, formatting);
			sb.AppendLine();
		}

		#region Handlers for the torrentmanager events

		static void TrackerManager_OnTrackerStateChange(object sender, TrackerStateChangedEventArgs e)
        {
            listener.WriteLine(e.NewState.ToString());
        }

        static void ConnectionManager_PeerDisconnected(object sender, PeerConnectionEventArgs e)
        {
            lock (peers)
                peers.Remove(e.PeerID);
        }

        static void ConnectionManager_PeerConnected(object sender, PeerConnectionEventArgs e)
        {
            lock (peers)
                peers.Add(e.PeerID);
        }
        static DateTime last = DateTime.Now;
        static int messageCount = 0;
        static void ConnectionManager_PeerMessageTransferred(object sender, PeerMessageEventArgs e)
        {
            //if(e.Message is HaveMessage)
            //Console.WriteLine("{2} - {0}: {1}", e.Direction, e.Message.GetType().Name, Environment.TickCount);
            /*if (DateTime.Now - last > new TimeSpan(0, 0, 3))
            {
               // messageCount = 0;
                last = DateTime.Now;
            }
            if(e.Message is RequestMessage && e.Direction == Direction.Outgoing)
            messageCount++;*/
        }

		static void main_OnTorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
		{
			listener.WriteLine("OldState: " + e.OldState.ToString() + " NewState: " + e.NewState.ToString());
		}

		private static object writeLock = new object();
        static void main_OnPieceHashed(object sender, PieceHashedEventArgs e)
        {
			lock (writeLock)
			{
				TorrentManager manager = (TorrentManager)sender;
				if (e.HashPassed)
					Console.ForegroundColor = ConsoleColor.Green;
				else
					Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Piece Hashed: {0} - {1}", e.PieceIndex, e.HashPassed ? "Pass" : "Fail");
				Console.ResetColor();
			}
        }

		#endregion Handlers for the torrentmanager events


		#region Misc methods to ensure safe shutdown of the engine

		private static void shutdown()
		{
            for (int i = 0; i < torrents.Count; i++)
                if (torrents[i].State != TorrentState.Stopped)
                    torrents[i].Stop().WaitOne();

			foreach (TraceListener lst in Debug.Listeners)
			{
				lst.Flush();
				lst.Close();
			}
		}

		static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			shutdown();
		}

		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Console.WriteLine("Unhandled exception: {0}", e.ExceptionObject);
			shutdown();
		}

		static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Console.WriteLine("Unhandled exception: {0}", e.ExceptionObject);
			shutdown();
		}

		static void CurrentDomain_ProcessExit(object sender, EventArgs e)
		{
			shutdown();
		}

		#endregion
	}
}
