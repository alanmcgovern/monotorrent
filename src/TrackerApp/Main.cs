//
// Main.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web;

using MonoTorrent.Tracker;
using MonoTorrent.Common;
using TrackerApp;
using MonoTorrent.TorrentWatcher;
using MonoTorrent.Tracker.Listeners;

namespace SampleTracker
{
    /// <summary>
    /// This is a sample implementation of how you could create a custom ITrackable
    /// </summary>
    public class CustomITrackable : ITrackable
    {
        // I just want to keep the TorrentFiles in memory when i'm tracking the torrent, so i store
        // a reference to them in the ITrackable. This allows me to display information about the
        // files in a GUI without having to keep the entire (really really large) Torrent instance in memory.
        private TorrentFile[] files;

        // We require the infohash and the name of the torrent so the tracker can work correctly
        private byte[] infoHash;
        private string name;

        public CustomITrackable(Torrent t)
        {
            // Note: I'm just storing the files, infohash and name. A typical Torrent instance
            // is ~100kB in memory. A typical CustomITrackable will be ~100 bytes.
            files = t.Files;
            infoHash = t.InfoHash;
            name = t.Name;
        }

        /// <summary>
        /// The files in the torrent
        /// </summary>
        public TorrentFile[] Files
        {
            get { return files; }
        }

        /// <summary>
        /// The infohash of the torrent
        /// </summary>
        public byte[] InfoHash
        {
            get { return infoHash; }
        }

        /// <summary>
        /// The name of the torrent
        /// </summary>
        public string Name
        {
            get { return name; }
        }
    }

    class MySimpleTracker
    {
        Tracker tracker;
        TorrentFolderWatcher watcher;
        const string TORRENT_DIR = "Torrents";

        ///<summary>Start the Tracker. Start Watching the TORRENT_DIR Directory for new Torrents.</summary>
        public MySimpleTracker()
        {
            System.Net.IPEndPoint listenpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 10000);
            Console.WriteLine("Listening at: {0}", listenpoint);
            ListenerBase listener = new HttpListener(listenpoint);
            tracker = new Tracker();
            tracker.RegisterListener(listener);
            listener.Start();

            SetupTorrentWatcher();


            while (true)
            {
                lock (tracker)
                    foreach (SimpleTorrentManager m in tracker)
                    {
                        Console.Write("Name: {0}   ", m.Trackable.Name);
                        Console.WriteLine("Complete: {1}   Incomplete: {2}   Downloaded: {0}", m.Downloaded, m.Complete, m.Incomplete);
                        Console.WriteLine();
                    }

                System.Threading.Thread.Sleep(10000);
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
                    System.Threading.Thread.Sleep(500);
					
                    Torrent t = Torrent.Load(e.TorrentPath);
					
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

        void watcher_TorrentLost(object sender, TorrentWatcherEventArgs e)
        {
            //try
            //{
            //    TrackerEngine.Instance.Tracker.Remove(e.TorrentPath);
            //}
            //catch(Exception ex)
            //{
            //    Console.WriteLine("Couldn't remove torrent: {0}", e.TorrentPath);
            //    Console.WriteLine("Reason: {0}", ex.Message);
            //}
        }

        void watcher_TorrentFound(object sender, TorrentWatcherEventArgs e)
        {
            //try
            //{
            //    Torrent t = Torrent.Load(e.TorrentPath);
            //    TrackerEngine.Instance.Tracker.Add(t);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("Couldn't load {0}.", e.TorrentPath);
            //    Console.WriteLine("Reason: {0}", ex.Message);
            //}
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
            int val = 0;
            while (true)
            {
                if (int.TryParse(Console.ReadLine(), out val))
                {
                    if (val == 1 || val == 2)
                        break;
                }
            }

            if (val == 1)
            {
                Debug.WriteLine("starting FrontendEngine");
                new MySimpleTracker();
            }
            else
            {
                Console.Clear();
                Console.Write("How many active torrents will be simulated: ");
                int torrents = GetInt();
                Console.Write("How many active peers per torrent: ");
                int peers = GetInt();
                Console.Write("How many requests per second: ");
                int requests = GetInt();

                StressTest test = new StressTest();
                test.Initialise(torrents, peers, requests);
                test.StartThreadedStress();

                while (true)
                {
                    Console.WriteLine("Measured announces/sec:  {0:0.00}", test.RequestRate);
                    Console.WriteLine("Total Annoucne Requests: {0:0.00}", test.TotalTrackerRequests);
                    Console.WriteLine("Actual announces/sec:    {0:0.00}", test.TotalRequests);
                    Console.WriteLine(Environment.NewLine);
                    test.TotalRequests = 0;
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }

        private static int GetInt()
        {
            int ret = 0;
            while (!int.TryParse(Console.ReadLine(), out ret))
            {
            }
            return ret;
        }
    }
}