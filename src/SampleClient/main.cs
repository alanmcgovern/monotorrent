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
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.BEncoding;

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
            TorrentSettings torrentDefaults = new TorrentSettings(4, 150, 100, 25);

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
                    catch (Exception exception)
                    {
                        Console.WriteLine("Couldn't decode the torrent");
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

            // For each torrent manager we loaded and stored in our list, hook into the events
            // in the torrent manager and start the engine.
            foreach (TorrentManager manager in torrents)
            {
                // Every time a piece is hashed, this is fired.
                manager.PieceHashed += new EventHandler<PieceHashedEventArgs>(main_OnPieceHashed);

                // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
                manager.TorrentStateChanged += new EventHandler<TorrentStateChangedEventArgs>(main_OnTorrentStateChanged);

                // Every time a message is transferred from us to a peer, or from a peer to us, this is fired
                engine.ConnectionManager.PeerMessageTransferred += new EventHandler<PeerMessageEventArgs>(ConnectionManager_PeerMessageTransferred);

                // Every time a peer connects, this is fired
                engine.ConnectionManager.PeerConnected += new EventHandler<PeerConnectionEventArgs>(ConnectionManager_PeerConnected);

                // Every time a peer disconnects, this is fired.
                engine.ConnectionManager.PeerDisconnected += new EventHandler<PeerConnectionEventArgs>(ConnectionManager_PeerDisconnected);

                // Every time the tracker's state changes, this is fired
                manager.TrackerManager.OnTrackerStateChange += new EventHandler<TrackerStateChangedEventArgs>(TrackerManager_OnTrackerStateChange);


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
                        sb.Append("Torrent:          "); sb.Append(manager.Torrent.Name);
                        sb.Append(Environment.NewLine);
                        sb.Append("Progress:         "); sb.AppendFormat("{0:0.00}", manager.Progress);
                        sb.Append(Environment.NewLine);
                        sb.Append("Download Speed:   "); sb.AppendFormat("{0:0.00}", manager.Monitor.DownloadSpeed);
                        sb.Append(" kB/s");
                        sb.Append(Environment.NewLine);
                        sb.Append("Upload Speed:     "); sb.AppendFormat("{0:0.00}", manager.Monitor.UploadSpeed);
                        sb.Append(" kB/s");
                        sb.Append(Environment.NewLine);
                        sb.Append("Torrent State:    "); sb.Append(manager.State);
                        sb.Append(Environment.NewLine);
                        sb.Append("Number of seeds:  "); sb.Append(manager.Peers.Seeds);
                        sb.Append(Environment.NewLine);
                        sb.Append("Number of leechs: "); sb.Append(manager.Peers.Leechs);
                        sb.Append(Environment.NewLine);
                        sb.Append("Total available:  "); sb.Append(manager.Peers.Available);
                        sb.Append(Environment.NewLine);
                        sb.Append("Downloaded:       "); sb.AppendFormat("{0:0.00}", manager.Monitor.DataBytesDownloaded / (1024.0 ));
                        sb.Append(" MB");
                        sb.Append(Environment.NewLine);
                        sb.Append("Uploaded:         "); sb.AppendFormat("{0:0.00}", manager.Monitor.DataBytesUploaded / (1024.0 ));
                        sb.Append(" MB");
                        sb.Append(Environment.NewLine);
                        sb.Append("Tracker Status:   "); sb.Append(manager.TrackerManager.CurrentTracker.State.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("Warning Message:  "); sb.Append(manager.TrackerManager.TrackerTiers[0].Trackers[0].WarningMessage);
                        sb.Append(Environment.NewLine);
                        sb.Append("Failure Message:  "); sb.Append(manager.TrackerManager.TrackerTiers[0].Trackers[0].FailureMessage);
                        sb.Append(Environment.NewLine);


                        int areSeeders=0;
                        int amInterested = 0;
                        int amRequestingPieces = 0;
                        int areChokingAndInteresting = 0;
                        int count = 0;
                        int isntChoking = 0;
                        double averagePercent = 0;
                        double maxPercent = 0;
                        double seedernotchoking = 0;
                        lock (peers)
                        {
                            foreach (PeerId p in peers)
                            {
                                if (!p.IsValid) // If it's not valid, it means the peer has been disconnected. This reference should be dropped
                                    continue;

                                count++;
                                if (p.IsSeeder && !p.IsChoking)
                                    seedernotchoking++;
                                areSeeders += p.IsSeeder ? 1 : 0;
                                amInterested += p.AmInterested ? 1 : 0;
                                isntChoking += !p.IsChoking ? 1 : 0;
                                areChokingAndInteresting += p.IsChoking && p.AmInterested ? 1 : 0;
                                amRequestingPieces += p.AmRequestingPiecesCount;
                                averagePercent += p.Bitfield.PercentComplete;
                                maxPercent = !p.IsChoking ? Math.Max(maxPercent, p.Bitfield.PercentComplete) : maxPercent;
                            }
                        }
                        averagePercent /= count;

                        sb.Append("AreSeeders:            " + areSeeders.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("AmInterested:          " + amInterested.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("AmRequesting:          " + amRequestingPieces.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("AmRequestingVerified:  " + messageCount.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("IsChoking+Interesting: " + areChokingAndInteresting.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("Unchoked me:           " + isntChoking.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("Unchoked me and seeder:" + seedernotchoking.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("Count:                 " + count.ToString());
                        sb.Append(Environment.NewLine);
                        
                        sb.Append("Average:               " + averagePercent.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("Max    :               " +  maxPercent.ToString());
                        sb.Append(Environment.NewLine);
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
                    //listener.ExportTo(Console.Out);
                }

                System.Threading.Thread.Sleep(100);
            }

            for (int l = 0; l < torrents.Count; l++)
                torrents[l].Stop().WaitOne();
            engine.Dispose();
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
            if (DateTime.Now - last > new TimeSpan(0, 0, 3))
            {
               // messageCount = 0;
                last = DateTime.Now;
            }
            if(e.Message is RequestMessage && e.Direction == Direction.Outgoing)
            messageCount++;
        }

		static void main_OnTorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
		{
			listener.WriteLine("OldState: " + e.OldState.ToString() + " NewState: " + e.NewState.ToString());
		}

		static void main_OnPieceHashed(object sender, PieceHashedEventArgs e)
		{
			TorrentManager manager = (TorrentManager)sender;
			if (e.HashPassed)
				listener.WriteLine("Hash Passed: " + manager.Torrent.Name + " " + e.PieceIndex + "/" + manager.Torrent.Pieces.Count);
			else
				listener.WriteLine("Hash Failed: " + manager.Torrent.Name + " " + e.PieceIndex + "/" + manager.Torrent.Pieces.Count);
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
			shutdown();
		}

		static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			shutdown();
		}

		static void CurrentDomain_ProcessExit(object sender, EventArgs e)
		{
			shutdown();
		}

		#endregion
	}
}
