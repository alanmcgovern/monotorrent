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

namespace MonoTorrent.TrackerApp
{
    class MySimpleTracker
    {
        const string TORRENT_DIR = "Torrents";
        
        ///<summary>Start the Tracker. Start Watching the TORRENT_DIR Directory for new Torrents.</summary>
        public MySimpleTracker()
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            TrackerEngine engine = TrackerEngine.Instance;
            engine.Address = "127.0.0.1";
            engine.Port = 10000;
            engine.Frontend = TrackerFrontend.InternalHttp;
            TorrentFolderWatcher watcher = new TorrentFolderWatcher(Path.Combine(Environment.CurrentDirectory, TORRENT_DIR), "*.torrent");
            watcher.TorrentFound += new EventHandler<TorrentWatcherEventArgs>(watcher_TorrentFound);
            watcher.TorrentLost += new EventHandler<TorrentWatcherEventArgs>(watcher_TorrentLost);
            engine.TorrentWatchers.Add(watcher);

            engine.TorrentWatchers.StartAll();
            engine.TorrentWatchers.ForceScanAll();
            
            engine.Start();
            Console.WriteLine("started");
        }

        void watcher_TorrentLost(object sender, TorrentWatcherEventArgs e)
        {
            try
            {
                TrackerEngine.Instance.Tracker.RemoveTorrent(e.TorrentPath);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Couldn't remove torrent: {0}", e.TorrentPath);
                Console.WriteLine("Reason: {0}", ex.Message);
            }
        }

        void watcher_TorrentFound(object sender, TorrentWatcherEventArgs e)
        {
            try
            {
                Torrent t = Torrent.Load(e.TorrentPath);
                TrackerEngine.Instance.Tracker.AddTorrent(t);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Couldn't load {0}.", e.TorrentPath);
                Console.WriteLine("Reason: {0}", ex.Message);
            }
        }
        
        public void OnProcessExit(object sender, EventArgs e)
        {
            Console.Write("shutting down the Tracker...");
            TrackerEngine.Instance.Stop();
            Console.WriteLine("done");
        }
        
        public static void Main(string[] args)
        {
            
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.WriteLine("starting FrontendEngine");
            new MySimpleTracker();
        }
    }
}
