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
        /////<summary>Add all Torrents in the TORRENT_DIR Directory.
        /////Done once at startup. 
        /////</summary>
        //public void AddTorrents()
        //{                       
        //    //MonoTorrent.Tracker.Tracker tracker = MonoTorrent.Tracker.Tracker.Instance;
        //    if (!Directory.Exists(TORRENT_DIR)) {
        //        Directory.CreateDirectory(TORRENT_DIR);
        //    }
        //    Console.WriteLine("loading torrents from " + TORRENT_DIR);
        //    foreach (string path in Directory.GetFiles(TORRENT_DIR, "*.torrent")) {
        //        AddTorrent(path);
        //    }
        //}
        
        
        ///<summary>Start the Tracker. Start Watching the TORRENT_DIR Directory for new Torrents.</summary>
        public MySimpleTracker()
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            TrackerEngine engine = TrackerEngine.Instance;
            engine.Address = "127.0.0.1";
            engine.Port = 10000;
            engine.Frontend = TrackerFrontend.InternalHttp;
            engine.TorrentWatchers.Add(new TorrentFolderWatcher(Path.Combine(Environment.CurrentDirectory, TORRENT_DIR), "*.torrent"));
            engine.Start();
            //AddTorrents();
            //StartWatching();
            Console.WriteLine("started");
        }
        
        /////<summary>Start the FileSystemWatcher on TORRENT_DIR</summary>
        //public void StartWatching()
        //{
        //    FileSystemWatcher watcher = new FileSystemWatcher(TORRENT_DIR, "*.torrent");
        //    watcher.Created += new FileSystemEventHandler(OnCreated);
        //    watcher.EnableRaisingEvents = true;
        //}
        
        /////<summary>Gets called when a File with .torrent extension was added to the TORRENT_DIR</summary>
        //public void OnCreated(object sender, FileSystemEventArgs e) 
        //{
        //    AddTorrent(e.FullPath);
        //}
        
        /////<summary>Add the Torrent to the Tracker</summary>
        /////<param name=path>Path to the Torrent which should be added</param>
        //public void AddTorrent(string path) 
        //{
        //    try {
        //        Torrent t = new Torrent();
        //        t.LoadTorrent(path);
        //        TrackerEngine.Instance.Tracker.AddTorrent(t);         
        //    } catch (TorrentException exc) {
                
        //        Console.Error.WriteLine("Failed to load Torrent " + path);
        //        Console.Error.WriteLine("Reason: " + exc.Message);
        //    }
        //}
        
        public void OnProcessExit(object sender, EventArgs e)
        {
            Console.Write("shutting down the Tracker...");
            //TrackerEngine.Instance.Stop();
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
