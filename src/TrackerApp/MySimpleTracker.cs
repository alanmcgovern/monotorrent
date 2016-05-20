using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using MonoTorrent.Common;
using MonoTorrent.TorrentWatcher;
using MonoTorrent.Tracker;
using MonoTorrent.Tracker.Listeners;
using HttpListener = MonoTorrent.Tracker.Listeners.HttpListener;

namespace TrackerApp
{
    internal class MySimpleTracker
    {
        private const string TORRENT_DIR = "Torrents";
        private readonly Tracker tracker;
        private TorrentFolderWatcher watcher;

        /// <summary>Start the Tracker. Start Watching the TORRENT_DIR Directory for new Torrents.</summary>
        public MySimpleTracker()
        {
            var listenpoint = new IPEndPoint(IPAddress.Loopback, 10000);
            Console.WriteLine("Listening at: {0}", listenpoint);
            ListenerBase listener = new HttpListener(listenpoint);
            tracker = new Tracker();
            tracker.AllowUnregisteredTorrents = true;
            tracker.RegisterListener(listener);
            listener.Start();

            SetupTorrentWatcher();


            while (true)
            {
                Thread.Sleep(10000);
            }
        }

        private void SetupTorrentWatcher()
        {
            watcher = new TorrentFolderWatcher(Path.GetFullPath(TORRENT_DIR), "*.torrent");
            watcher.TorrentFound += delegate(object sender, TorrentWatcherEventArgs e)
            {
                try
                {
                    // This is a hack to work around the issue where a file triggers the event
                    // before it has finished copying. As the filesystem still has an exclusive lock
                    // on the file, monotorrent can't access the file and throws an exception.
                    // The best way to handle this depends on the actual application. 
                    // Generally the solution is: Wait a few hundred milliseconds
                    // then try load the file.
                    Thread.Sleep(500);

                    var t = Torrent.Load(e.TorrentPath);

                    // There is also a predefined 'InfoHashTrackable' MonoTorrent.Tracker which
                    // just stores the infohash and name of the torrent. This is all that the tracker
                    // needs to run. So if you want an ITrackable that "just works", then use InfoHashTrackable.

                    // ITrackable trackable = new InfoHashTrackable(t);
                    ITrackable trackable = new CustomITrackable(t);

                    // The lock is here because the TorrentFound event is asyncronous and I have
                    // to ensure that only 1 thread access the tracker at the same time.
                    lock (tracker)
                        tracker.Add(trackable);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error loading torrent from disk: {0}", ex.Message);
                    Debug.WriteLine("Stacktrace: {0}", ex.ToString());
                }
            };

            watcher.Start();
            watcher.ForceScan();
        }

        public void OnProcessExit(object sender, EventArgs e)
        {
            //Console.Write("shutting down the Tracker...");
            //TrackerEngine.Instance.Stop();
            //Console.WriteLine("done");
        }

        public static void Main(string[] args)
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));

            Console.WriteLine("Welcome to the MonoTorrent tracker");
            Console.WriteLine("1. Start the tracker");
            Console.WriteLine("2. Start a benchmark");

            Console.Write("Choice: ");

            var val = 0;
            while (val != 1 && val != 2)
                val = GetInt();

            if (val == 1)
                StartTracker();
            else
                Benchmark();
        }

        private static void Benchmark()
        {
            Console.Clear();
            Console.Write("How many active torrents will be simulated: ");
            var torrents = GetInt();
            Console.Write("How many active peers per torrent: ");
            var peers = GetInt();
            Console.Write("How many requests per second: ");
            var requests = GetInt();

            Console.Write("What is the tracker address: ");
            var address = Console.ReadLine();

            var test = new StressTest(torrents, peers, requests);
            test.Start(address);

            while (true)
            {
                Console.WriteLine("Measured announces/sec:  {0}", test.RequestRate);
                Console.WriteLine("Total announces: {0}", test.TotalTrackerRequests);
                Console.WriteLine(Environment.NewLine);
                Thread.Sleep(1000);
            }
        }

        private static void StartTracker()
        {
            new MySimpleTracker();
        }

        private static int GetInt()
        {
            var ret = 0;
            while (!int.TryParse(Console.ReadLine(), out ret))
            {
            }
            return ret;
        }
    }
}