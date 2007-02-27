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

namespace TestClient
{
    class main
    {
        static string basePath;
        static ClientEngine engine;
        static List<TorrentManager> torrents = new List<TorrentManager>();

        static void GenerateBencodes()
        {
        	UTF8Encoding enc = new UTF8Encoding();
            TextWriter s = null;/// new FileStream(@"d:\bencodedstrings.txt", FileMode.OpenOrCreate);
        	IBEncodedValue val = (BEncodedString)"this is a bencoded string";
        	s.WriteLine(enc.GetString(val.Encode()));
        	
        	val = (BEncodedNumber)12345;
        	s.WriteLine();
        	s.WriteLine(enc.GetString(val.Encode()));
        	
        	BEncodedList list = new BEncodedList();
        	list.Add((BEncodedString)"mystring");
        	list.Add(new BEncodedNumber(12321));
        	list.Add(new BEncodedList());
        	
        	s.WriteLine();
        	s.WriteLine(enc.GetString(val.Encode()));
        	
        	BEncodedDictionary dict = new BEncodedDictionary();
        	dict.Add("entry 1", new BEncodedNumber(6532));
        	dict.Add("entry 2: a string", new BEncodedString("hola"));
        	dict.Add("This is a dict", new BEncodedDictionary());
        	
        	s.WriteLine();
        	s.WriteLine(enc.GetString(val.Encode()));
        }
        static void Main(string[] args)
        {
            basePath = Environment.CurrentDirectory;

            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Thread.GetDomain().UnhandledException += new UnhandledExceptionEventHandler(UnhandledException);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            Debug.Listeners.Clear();
            Debug.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Out));

            Debug.Flush();
            TestEngine();
        }

        static private void TestEngine()
        {
            int port;
            Console.Write(Environment.NewLine + "Choose a listen port: ");
            while (!Int32.TryParse(Console.ReadLine(), out port)) { }

            EngineSettings settings = EngineSettings.DefaultSettings();
                        settings.SavePath = Path.Combine(basePath, "Downloads");

            settings.ListenPort = port;
            engine = new ClientEngine(settings, TorrentSettings.DefaultSettings());


            if (!Directory.Exists(engine.Settings.SavePath))
                Directory.CreateDirectory(engine.Settings.SavePath);

            if (!Directory.Exists(Path.Combine(basePath, "Torrents")))
                Directory.CreateDirectory(Path.Combine(basePath, "Torrents"));

            foreach (string file in Directory.GetFiles(Path.Combine(basePath, "Torrents")))
            {
                if (file.EndsWith(".torrent"))
                    torrents.Add(engine.LoadTorrent(file));
            }

            if (torrents.Count == 0)
            {
                Console.WriteLine("No torrents found in the Torrents directory");
                Console.WriteLine("Exiting...");
                return;
            }
            Debug.WriteLine("Torrent State:    " + ((TorrentManager)torrents[0]).State.ToString());
            foreach (TorrentManager manager in torrents)
            {
                manager.PieceHashed += new EventHandler<PieceHashedEventArgs>(main_OnPieceHashed);
                manager.TorrentStateChanged += new EventHandler<TorrentStateChangedEventArgs>(main_OnTorrentStateChanged);
                ClientEngine.ConnectionManager.PeerMessageTransferred += new EventHandler<PeerMessageEventArgs>(ConnectionManager_PeerMessageTransferred);
                ClientEngine.ConnectionManager.PeerConnected += new EventHandler<PeerConnectionEventArgs>(ConnectionManager_PeerConnected);
                ClientEngine.ConnectionManager.PeerDisconnected += new EventHandler<PeerConnectionEventArgs>(ConnectionManager_PeerDisconnected);
                
                engine.Start(manager);
            }


            int i = 0;
            bool running = true;
            while (running)
            {
                if ((i++) % 5 == 0)
                {
                    running = false;
                    Console.Clear();
                    foreach (TorrentManager manager in torrents)
                    {
                        if (manager.State != TorrentState.Stopped)
                            running = true;

                        Console.WriteLine("Torrent:          " + manager.Torrent.Name);
                        Console.WriteLine("Uploading to:     " + manager.UploadingTo.ToString());
                        Console.WriteLine("Half opens:       " + ClientEngine.ConnectionManager.HalfOpenConnections);
                        Console.WriteLine("Max open:         " + ClientEngine.ConnectionManager.MaxOpenConnections);
                        Console.WriteLine("Progress:         " + string.Format(manager.Progress.ToString(), ("{0:0.00}")));
                        Console.WriteLine("Download Speed:   " + string.Format("{0:0.00}", manager.DownloadSpeed() / 1024));
                        Console.WriteLine("Upload Speed:     " + string.Format("{0:0.00}", manager.UploadSpeed() / 1024));
                        Console.WriteLine("Torrent State:    " + manager.State.ToString());
                        Console.WriteLine("Number of seeds:  " + manager.Seeds());
                        Console.WriteLine("Number of leechs: " + manager.Leechs());
                        Console.WriteLine("Total available:  " + manager.AvailablePeers);
                        Console.WriteLine("Downloaded:       " + manager.Monitor.DataBytesDownloaded / 1024.0);
                        Console.WriteLine("Uploaded:         " + manager.Monitor.DataBytesUploaded / 1024.0);
                        Console.WriteLine("Tracker Status:   " + manager.TrackerManager.CurrentTracker.State.ToString());
                        Console.WriteLine("Protocol Download:" + manager.Monitor.ProtocolBytesDownloaded / 1024.0);
                        Console.WriteLine("Protocol Upload:  " + manager.Monitor.ProtocolBytesUploaded / 1024.0);
                        Console.WriteLine("Hashfails:        " + manager.HashFails.ToString());
                        Console.WriteLine("Tracker Status:   " + manager.TrackerManager.CurrentTracker.State.ToString());
                        Console.WriteLine("Scrape complete:  " + manager.TrackerManager.CurrentTracker.Complete);
                        Console.WriteLine("Scrape incomplete:" + manager.TrackerManager.CurrentTracker.Incomplete);
                        Console.WriteLine("Scrape downloaded:" + manager.TrackerManager.CurrentTracker.Downloaded);
                        Console.WriteLine("Warning Message:  " + manager.TrackerManager.CurrentTracker.WarningMessage);
                        Console.WriteLine("Failure Message:  " + manager.TrackerManager.CurrentTracker.FailureMessage);
                        Console.WriteLine("Endgame Mode:     " + manager.PieceManager.InEndGameMode.ToString());
                        Console.WriteLine("\n");
                    }
                }
                System.Threading.Thread.Sleep(100);
            }

            WaitHandle[] a = engine.Stop();
            for (int j = 0; j < a.Length; j++)
                if (a[j] != null)
                    a[j].WaitOne();
            engine.Dispose();
        }

        static void ConnectionManager_PeerDisconnected(object sender, PeerConnectionEventArgs e)
        {
            Console.WriteLine("Disconnected: " + e.PeerID.Peer.Location + " - " + e.ConnectionDirection.ToString());
        }

        static void ConnectionManager_PeerConnected(object sender, PeerConnectionEventArgs e)
        {
            Console.WriteLine("Connected: " + e.PeerID.Peer.Location + " - " + e.ConnectionDirection.ToString());
        }

        static void ConnectionManager_PeerMessageTransferred(object sender, PeerMessageEventArgs e)
        {
            //Console.WriteLine(e.Direction.ToString() + ":\t" + e.Message.GetType());
        }

        #region Shutdown methods
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

        private static void shutdown()
        {
            Logger.FlushToDisk();
            WaitHandle[] handles = engine.Stop();
            for (int i = 0; i < handles.Length; i++)
                if (handles[i] != null)
                    handles[i].WaitOne();

            foreach (TraceListener lst in Debug.Listeners)
            {
                lst.Flush();
                lst.Close();
            }
        }
        #endregion


        #region events i've hooked into
        static void main_OnTorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
        {
            Debug.WriteLine("State: " + e.NewState.ToString());
        }

        static void main_OnPieceHashed(object sender, PieceHashedEventArgs e)
        {
            TorrentManager manager = (TorrentManager)sender;
            if (!e.HashPassed)
                //Debug.WriteLine("Hash Passed: " + manager.Torrent.Name + " " + e.PieceIndex + "/" + manager.Torrent.Pieces.Length);
           // else
                Console.WriteLine("Hash Failed: " + manager.Torrent.Name + " " + e.PieceIndex + "/" + manager.Torrent.Pieces.Length);
        }
        #endregion


        /*
        public void Main()
        {
            // An TorrentManager is passed out of the engine when you load a torrent. This is
            // used for controlling the torrent.
            TorrentManager torrentManager;

            // These are the default settings for the engine for this session
            EngineSettings engineSettings = EngineSettings.DefaultSettings;

            // The default location to download files to on your HardDrive, like a downloads folder
            // All files will be downloaded using this as the base directory. Single file torrents will
            // go directly into this directory, multifile torrents will create a directory within this
            // and download there.
            engineSettings.DefaultSavePath = @"D:\Downloads\Torrents";

            // Maximum upload speed of 30 kB/sec. At upload speeds of less than 5kB/sec, there will
            // be automatic download speed limiting to 5x the selected upload.
            engineSettings.GlobalMaxUploadSpeed = 30;

            // Every torrent loaded into the engine for this session will start off with these default settings
            // unless other settings are specified.
            TorrentSettings torrentSettings = TorrentSettings.DefaultSettings;

            // Each torrent will be allowed a max of 10kB/sec upload speed
            torrentSettings.MaxUploadSpeed = 10;

            // Each torrent will have 4 upload slots to allow 2.5kB/sec per slot.
            torrentSettings.UploadSlots = 4;

            // Instantiate a new engine with the engineSettings and Default Torrent settings.
            ClientEngine engine = new ClientEngine(engineSettings, torrentSettings);

            // A torrent can be downloaded from the specified url, saved to the specified file and
            // then loaded into the engine.
            // torrentManager =engine.LoadTorrent(new Uri("http://example.com/example.torrent"), @"D:\Downloads\example.torrent");

            // Alternatively a .torrent can just be loaded from the disk. This torrent will save
            // to the DefaultSaveLocation as specified in the EngineSettings and will inherit the
            // default settings that are set in the Engine.
            //torrentManager = engine.LoadTorrent(@"D:\Downloads\Torrents\MyTorrentFile.torrent");

            // This torrent would use the supplied settings instead of using the ones that were
            // supplied when instantiating the engine
            torrentManager = engine.LoadTorrent(@"D:\Downloads\Torrents\MyTorrentFile.torrent", TorrentSettings.DefaultSettings);

            // If you have loaded multiple torrents into the engine, you can start them all at once with this:
            // engine.Start();

            // Or you can start one specific torrent by passing in that torrents TorrentManager
            engine.Start(torrentManager);

            // You can hook into various events in order to display information on screen:
            // Fired every time a peer is added through DHT, PEX or Tracker Updates
            torrentManager.OnPeersAdded+=new EventHandler<PeersAddedEventArgs>(PeersAdded);

            // Fired every time a piece is hashed
            torrentManager.OnPieceHashed+=new EventHandler<PieceHashedEventArgs>(PieceHashed);

            // Fired every time the torrent State changes (i.e. paused/hashing/downloading)
            torrentManager.OnTorrentStateChanged+= new EventHandler<TorrentStateChangedEventArgs>(torrentStateChanged);
            
            // Fired every time a piece changes. i.e. block sent/received/written to disk
            torrentManager.PieceManager.OnPieceChanged+=new EventHandler<PieceEventArgs>(pieceStateChanged);
            
            // Fired every time a connection is either created or destroyed
            ClientEngine.connectionManager.OnPeerConnectionChanged+=new EventHandler<PeerConnectionEventArgs>(peerConnectionChanged);
           
            // Fired every time a peer message is sent
            ClientEngine.connectionManager.OnPeerMessages+= new EventHandler<PeerMessageEventArgs>(peerMessageSentOrRecieved);

            // Keep running while the torrent isn't stopped or paused.
            while (torrentManager.State != TorrentState.Stopped || torrentManager.State != TorrentState.Paused)
            {
                Console.WriteLine(torrentManager.Progress());
                System.Threading.Thread.Sleep(1000);

                if (torrentManager.Progress() == 100.0)
                {
                    // If we want to stop a torrent, or the engine for whatever reason, we call engine.Stop()
                    // A tracker update *must* be performed before the engine is shut down, so you must
                    // wait for the waithandle to become signaled before continuing with the complete
                    // shutdown of your client. Otherwise stats will not get reported correctly.
                    WaitHandle[] handles = engine.Stop();
                    WaitHandle.WaitAll(handles);
                    return;
                }
            }
        }

        */
    }
}
