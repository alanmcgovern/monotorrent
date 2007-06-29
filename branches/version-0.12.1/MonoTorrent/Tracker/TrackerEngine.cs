//
// TrackerEngine.cs
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
using System.Threading;
using MonoTorrent.Common;
using System.Net;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Tracker
{   
    public class TrackerEngineException : Exception
    {
        public TrackerEngineException(string message) : base(message)
        {
            
        }
    }
    
    
    ///<summary>This class Handles the starting of the Web Frontend used by the Tracker.
    ///Currently there are two Frontends supported: Asp.Net and Internal Frontend. The Internal
    ///Frontend handles HTTP Request through the HttpListener class.
    ///</summary>
    public class TrackerEngine
    {       
        private bool running;
        private InternalHttpServer internal_http;
                
        private TrackerEngine()
        {           
            running = false;            
        }
        
        ///<summary>Gets an instance of the TrackerEngine.
        ///</summary>
        public static TrackerEngine Instance
        {
            get {            
                if (tracker_engine == null) {
                    tracker_engine = new TrackerEngine();               
                }
                return tracker_engine;
            }
        }
        private static TrackerEngine tracker_engine = null;
        
        
        ///<summary>The tracker Instance</summary>
        public Tracker Tracker
        {
            get {
                if( !running)
                    throw new TrackerEngineException("TrackeEngine not started");
                return Tracker.Instance;
            }
        }        
        
        ///<summary>Get or set Frontend. Use before start() is called.</summary>
        public TrackerFrontend Frontend
        {
            get {
                return frontend;
            }
            set {
                if (running)
                    throw new TrackerEngineException("Tracker already started. Set Properties before calling Start");
                frontend = value;
            }
        }
        private TrackerFrontend frontend = TrackerFrontend.InternalHttp;
        
        ///<summary>Get and set port of the Frontend. Used by Internal Frontend.</summary>
        public ushort Port
        {
            get {
                return port;
            }
            set {
                if (running)
                    throw new TrackerEngineException("Tracker already started. Set Properties before calling Start");
                port = value;
            }
        }
        private ushort port = 6969;
        
        ///<summary>Checks if the Tracker is running.
        ///</summary>
        public bool IsRunnning()
        {
            return running;
        }
        
        ///<summary>The Address the Frontend should use. Used by Internal Frontend.</summary>
        public string Address
        {
            get {
                return address;
            }
            set {
                if (running)
                    throw new TrackerEngineException("Tracker already started. Set Properties before calling Start");
                IPAddress.Parse(value);
                address = value;
            }
        }
        private string address = "0.0.0.0";
        
        ///<summary>Start the Frontend. Used by Internal Frontend.</summary>
        public void Start()
        {
            if (!running) {            
                running = true;
                
                if (frontend == TrackerFrontend.InternalHttp) {
                    internal_http = new InternalHttpServer(Address, Port);
                    Thread serverThread = new Thread(new ThreadStart(internal_http.Start));
                    serverThread.Start();
                    //internal_http.Start();
                }
                Tracker.ClearTorrents();            
                
                if (torrentWatchers != null)
                {
                    TorrentWatchers.StartWatching();
                    TorrentWatchers.ForceScan();
                }
            }
        }
        
        ///<summary>Stops the Frontend and Reset is called. 
        ///</summary>
        public void Stop()
        {           
            if (running) {
                try{
                    if (Frontend == TrackerFrontend.InternalHttp) {
                        internal_http.Stop();
                    }
                    Reset();
                    running = false;                
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                } finally {
                    
                }
            }            
        }
        
        ///<summary>Reset the Tracker. All Torrents will be removed and the internal
        ///Datastructures are cleard.
        ///</summary>
        public void Reset()
        {
            if (running) {
                Tracker.ClearTorrents();
            }
        }
        
        
        private TorrentWatchers torrentWatchers;

        ///<summary></summary>
        public TorrentWatchers TorrentWatchers
        {
            get
            {
                if (this.torrentWatchers == null)
                {
                    this.torrentWatchers = new TorrentWatchers();
                    this.torrentWatchers.OnTorrentFound += new EventHandler<TorrentWatcherEventArgs>(OnTorrentCreated);
                    this.torrentWatchers.OnTorrentLost += new EventHandler<TorrentWatcherEventArgs>(OnTorrentRemoved);
                }

                return this.torrentWatchers;
            }
        }

        void OnTorrentCreated(object sender, TorrentWatcherEventArgs e)
        {
            try
            {
                Torrent t = Torrent.Load(e.TorrentPath);
                this.Tracker.AddTorrent(t);
            }
            catch (BEncodingException ex)
            {
                Console.Error.WriteLine("Reason: " + ex.ToString());
            }
            catch (TorrentException exc)
            {
                //Console.Error.WriteLine("Failed to load Torrent " + e.Torrent.TorrentPath);
                Console.Error.WriteLine("Reason: " + exc.Message);
            }
        }

        void OnTorrentRemoved(object sender, TorrentWatcherEventArgs e)
        {
            try
            {
                this.Tracker.RemoveTorrent(e.TorrentPath);
            }
            catch (TorrentException exc)
            {
                //Console.Error.WriteLine("Failed to remove Torrent " + e.Torrent.TorrentPath);
                Console.Error.WriteLine("Reason: " + exc.Message);
            }
        }
    }
    
}

