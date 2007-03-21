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
        static ClientEngine engine;
        static List<TorrentManager> torrents = new List<TorrentManager>();
        static Top10Listener listener;

        static void Main(string[] args)
        {
            basePath = Environment.CurrentDirectory;
            torrentsPath = Path.Combine(basePath, "Torrents");
            downloadsPath = Path.Combine(basePath, "Downloads");

            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Thread.GetDomain().UnhandledException += new UnhandledExceptionEventHandler(UnhandledException);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            Debug.Listeners.Clear();
            listener = new Top10Listener(25);
            Debug.Listeners.Add(listener);
            Debug.Flush();

            TestEngine();
        }

        static private void TestEngine()
        {
            int port;
            Console.Write(Environment.NewLine + "Choose a listen port: ");
            while (!Int32.TryParse(Console.ReadLine(), out port)) { }

            EngineSettings engineSettings = new EngineSettings(downloadsPath, port, false);
            TorrentSettings torrentDefaults = new TorrentSettings(5, 50, 100, 30);
            engine = new ClientEngine(engineSettings, torrentDefaults);

            if (!Directory.Exists(engine.Settings.SavePath))
                Directory.CreateDirectory(engine.Settings.SavePath);

            if (!Directory.Exists(torrentsPath))
                Directory.CreateDirectory(torrentsPath);

            foreach (string file in Directory.GetFiles(torrentsPath))
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
                        sb.Append("Uploading to:     "); sb.Append(manager.UploadingTo);
                        sb.Append(Environment.NewLine);
                        sb.Append("Half opens:       "); sb.Append(ClientEngine.ConnectionManager.HalfOpenConnections);
                        sb.Append(Environment.NewLine);
                        sb.Append("Max open:         "); sb.Append(ClientEngine.ConnectionManager.MaxOpenConnections);
                        sb.Append(Environment.NewLine);
                        sb.Append("Progress:         "); sb.AppendFormat("{0:0.00}", manager.Progress);
                        sb.Append(Environment.NewLine);
                        sb.Append("Download Speed:   "); sb.AppendFormat("{0:0.00}", manager.DownloadSpeed() / 1024);
                        sb.Append(Environment.NewLine);
                        sb.Append("Upload Speed:     "); sb.AppendFormat("{0:0.00}", manager.UploadSpeed() / 1024);
                        sb.Append(Environment.NewLine);
                        sb.Append("Torrent State:    "); sb.Append(manager.State);
                        sb.Append(Environment.NewLine);
                        sb.Append("Number of seeds:  "); sb.Append(manager.Seeds());
                        sb.Append(Environment.NewLine);
                        sb.Append("Number of leechs: "); sb.Append(manager.Leechs());
                        sb.Append(Environment.NewLine);
                        sb.Append("Total available:  "); sb.Append(manager.AvailablePeers);
                        sb.Append(Environment.NewLine);
                        sb.Append("Downloaded:       "); sb.AppendFormat("{0:0.00}", manager.Monitor.DataBytesDownloaded / 1024.0);
                        sb.Append(Environment.NewLine);
                        sb.Append("Uploaded:         "); sb.AppendFormat("{0:0.00}", manager.Monitor.DataBytesUploaded / 1024.0);
                        sb.Append(Environment.NewLine);
                        sb.Append("Tracker Status:   "); sb.Append(manager.TrackerManager.CurrentTracker.State);
                        sb.Append(Environment.NewLine);
                        sb.Append("Protocol Download:"); sb.AppendFormat("{0:0.00}", manager.Monitor.ProtocolBytesDownloaded / 1024.0);
                        sb.Append(Environment.NewLine);
                        sb.Append("Protocol Upload:  "); sb.AppendFormat("{0:0.00}", manager.Monitor.ProtocolBytesUploaded / 1024.0);
                        sb.Append(Environment.NewLine);
                        sb.Append("Hashfails:        "); sb.Append(manager.HashFails.ToString());
                        sb.Append(Environment.NewLine);
                        sb.Append("Scrape complete:  "); sb.Append(manager.TrackerManager.CurrentTracker.Complete);
                        sb.Append(Environment.NewLine);
                        sb.Append("Scrape incomplete:"); sb.Append(manager.TrackerManager.CurrentTracker.Incomplete);
                        sb.Append(Environment.NewLine);
                        sb.Append("Scrape downloaded:"); sb.Append(manager.TrackerManager.CurrentTracker.Downloaded);
                        sb.Append(Environment.NewLine);
                        sb.Append("Warning Message:  "); sb.Append(manager.TrackerManager.CurrentTracker.WarningMessage);
                        sb.Append(Environment.NewLine);
                        sb.Append("Failure Message:  "); sb.Append(manager.TrackerManager.CurrentTracker.FailureMessage);
                        sb.Append(Environment.NewLine);
                        sb.Append("Endgame Mode:     "); sb.Append(manager.PieceManager.InEndGameMode);
                        sb.Append(Environment.NewLine);
                    }

                    Console.Clear();
                    Console.WriteLine(sb.ToString());
                    listener.ExportTo(Console.Out);
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
            listener.WriteLine("Disconnected: " + e.PeerID.Peer.Location + " - " + e.ConnectionDirection.ToString());
        }

        static void ConnectionManager_PeerConnected(object sender, PeerConnectionEventArgs e)
        {
            listener.WriteLine("Connected: " + e.PeerID.Peer.Location + " - " + e.ConnectionDirection.ToString());
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
            listener.WriteLine("State: " + e.NewState.ToString());
        }

        static void main_OnPieceHashed(object sender, PieceHashedEventArgs e)
        {
            TorrentManager manager = (TorrentManager)sender;
            if (!e.HashPassed)
                listener.WriteLine("Hash Passed: " + manager.Torrent.Name + " " + e.PieceIndex + "/" + manager.Torrent.Pieces.Length);
            else
                listener.WriteLine("Hash Failed: " + manager.Torrent.Name + " " + e.PieceIndex + "/" + manager.Torrent.Pieces.Length);
        }
        #endregion

    }
}
