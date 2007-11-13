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

namespace SampleTracker
{
    class MySimpleTracker
    {
        Tracker tracker;
        TorrentFolderWatcher watcher;
        const string TORRENT_DIR = "Torrents";
        
        ///<summary>Start the Tracker. Start Watching the TORRENT_DIR Directory for new Torrents.</summary>
        public MySimpleTracker()
        {
            ListenerBase listener = new HttpListener(System.Net.IPAddress.Loopback, 10000);
            tracker = new Tracker();
            tracker.RegisterListener(listener);
            listener.Start();

            SetupTorrentWatcher();


            while (true)
            {
                foreach (SimpleTorrentManager m in tracker)
                {
                    Console.WriteLine("Name: {0}", m.Trackable.Name);
                    Console.WriteLine("Complete: {1}   Incomplete: {2}   Downloaded: {0}", m.Downloaded, m.Complete, m.Count - m.Complete);
                    Console.WriteLine();
                    System.Threading.Thread.Sleep(10000);
                }
            }
        }

        private void SetupTorrentWatcher()
        {
            watcher = new TorrentFolderWatcher(TORRENT_DIR, "*.torrent");
            watcher.TorrentFound += delegate(object sender, TorrentWatcherEventArgs e) {
                try
                {
                    Torrent t = Torrent.Load(e.TorrentPath);
                    tracker.Add(new InfoHashTrackable(t));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error loading torrent from disk: {0}", ex.Message);
                    Debug.WriteLine("Stacktrace: {0}", ex.ToString());
                }
            };

            watcher.TorrentFound += delegate(object sender, TorrentWatcherEventArgs e) {
                try
                {
                    Torrent t = Torrent.Load(e.TorrentPath);
                    tracker.Remove(new InfoHashTrackable(t));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error loading torrent from disk: {0}", ex.Message);
                    Debug.WriteLine("Stacktrace: {0}", ex.ToString());
                }
            };


            watcher.StartWatching();
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
            Debug.WriteLine("starting FrontendEngine");
            new MySimpleTracker();
        }
    }
}
