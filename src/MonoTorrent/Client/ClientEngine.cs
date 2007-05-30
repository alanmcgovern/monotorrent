//
// ClientEngine.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Timers;
using System.Net;
using MonoTorrent.Client.PeerMessages;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IDisposable
    {
        #region Global Supports
        public static readonly bool SupportsFastPeer = true;
        public static readonly bool SupportsEncryption = true;
        public static readonly bool SupportsEndgameMode = false;
        public static readonly bool SupportsDht = false;
        #endregion


        #region Events

        public event EventHandler<StateUpdateEventArgs> StatsUpdate;

        #endregion


        #region Member Variables
        /// <summary>
        /// This manager is used to control Send/Receive buffer allocations for all running torrents
        /// </summary>
        internal static readonly BufferManager BufferManager = new BufferManager();

        /// <summary>
        /// The connection manager which manages all the connections for the library
        /// </summary>
        public ConnectionManager ConnectionManager
        {
            get { return this.connectionManager; }
        }

        private ConnectionManager connectionManager;

        /// <summary>
        /// True if the engine has been started
        /// </summary>
        public bool IsRunning
        {
            get { return this.timer.Enabled; }
        }


        /// <summary>
        /// Listens for incoming connections and passes them off to the correct TorrentManager
        /// </summary>
        private ConnectionListener listener;


        /// <summary>
        /// Returns the engines PeerID
        /// </summary>
        public string PeerId
        {
            get { return peerId; }
        }
        private readonly string peerId = GeneratePeerId();


        /// <summary>
        /// The engines settings
        /// </summary>
        public EngineSettings Settings
        {
            get { return this.settings; }
            set { this.settings = value; }
        }
        private EngineSettings settings;


        /// <summary>
        /// The timer used to call the logic methods for the torrent managers
        /// </summary>
        private System.Timers.Timer timer;
        private int tickCount;


        /// <summary>
        /// A logic tick will be performed every TickLength miliseconds
        /// </summary>
        internal const int TickLength = 500;

        /// <summary>
        /// The TorrentManager's loaded into the engine
        /// </summary>
        public TorrentManagerCollection Torrents
        {
            get { return this.torrents; }
            set { this.torrents = value; }
        }
        private TorrentManagerCollection torrents;
       
        internal ReaderWriterLock torrentsLock = new ReaderWriterLock();

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new ClientEngine
        /// </summary>
        /// <param name="engineSettings">The engine settings to use</param>
        /// <param name="defaultTorrentSettings">The default settings for new torrents</param>
        public ClientEngine(EngineSettings engineSettings)
        {
            if (engineSettings == null)
                throw new ArgumentNullException("engineSettings");

            this.settings = engineSettings;

            this.connectionManager = new ConnectionManager(this);
            this.listener = new ConnectionListener(this);
            this.timer = new System.Timers.Timer(TickLength);
            this.timer.Elapsed += new ElapsedEventHandler(LogicTick);
            this.torrents = new TorrentManagerCollection();
        }

        #endregion


        #region Methods

        /// <summary>
        /// Returns true if a TorrentManager containing this Torrent has been registered with this engine
        /// </summary>
        /// <param name="torrent"></param>
        /// <returns></returns>
        public bool Contains(Torrent torrent)
        {
            if (torrent == null)
                return false;

            for (int i = 0; i < this.torrents.Count; i++)
                if (Toolbox.ByteMatch(this.torrents[i].Torrent.infoHash, torrent.infoHash))
                    return true;

            return false;
        }


        /// <summary>
        /// Returns true if the TorrentManager has been registered with this engine
        /// </summary>
        /// <param name="manager"></param>
        /// <returns></returns>
        public bool Contains(TorrentManager manager)
        {
            if (manager == null)
                return false;

            return Contains(manager.Torrent);
        }


        /// <summary>
        /// Disposes the Engine as well as all TorrentManagers still registered with the engine
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < this.torrents.Count; i++)
            {
                if (torrents[i].State != TorrentState.Stopped)
                    torrents[i].Stop().WaitOne();

                Unregister(torrents[i]);
            }

            if (!this.listener.Disposed)
                this.listener.Dispose();

            this.timer.Dispose();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static string GeneratePeerId()
        {
            StringBuilder sb = new StringBuilder(20);
            Random rand = new Random((int)DateTime.Now.TimeOfDay.TotalMilliseconds);

            sb.Append(Common.VersionInfo.ClientVersion);
            for (int i = 0; i < 12; i++)
                sb.Append(rand.Next(0, 9));

            return sb.ToString();
        }


        /// <summary>
        /// Pauses all TorrentManagers which are registered to this engine
        /// </summary>
        public void PauseAll()
        {
            using (new ReaderLock(this.torrentsLock))
                for (int i = 0; i < torrents.Count; i++)
                    if (torrents[i].State == TorrentState.Downloading ||
                        torrents[i].State == TorrentState.Seeding ||
                        torrents[i].State == TorrentState.SuperSeeding)
                        torrents[i].Pause();
        }


        /// <summary>
        /// Registers the TorrentManager with this engine
        /// </summary>
        /// <param name="manager"></param>
        public void Register(TorrentManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("torrent");

            if (manager.Engine != null)
                throw new TorrentException("This manager has already been registered");

            using (new WriterLock(torrentsLock))
                this.torrents.Add(manager);

            manager.Engine = this;
        }


        /// <summary>
        /// Starts all stopped or paused TorrentManagers which are registered to this engine
        /// </summary>
        public void StartAll()
        {
            using (new ReaderLock(this.torrentsLock))
                for (int i = 0; i < torrents.Count; i++)
                    if (torrents[i].State == TorrentState.Stopped || torrents[i].State == TorrentState.Paused)
                        torrents[i].Start();
        }


        /// <summary>
        /// Stops all TorrentManagers which are registered to this engine
        /// </summary>
        public void StopAll()
        {
            using (new ReaderLock(this.torrentsLock))
                for (int i = 0; i < torrents.Count; i++)
                    if (torrents[i].State != TorrentState.Stopped)
                        torrents[i].Stop();
        }


        /// <summary>
        /// Returns the combined download speed of all active torrents
        /// </summary>
        public double TotalDownloadSpeed
        {
            get
            {
                double speed = 0;
                using (new ReaderLock(torrentsLock))
                    for (int i = 0; i < torrents.Count; i++)
                        speed += torrents[i].Monitor.DownloadSpeed;

                return speed;
            }
        }


        /// <summary>
        /// Returns the combined upload speed of all active torrents
        /// </summary>
        public double TotalUploadSpeed
        {
            get
            {
                double speed = 0;
                using (new ReaderLock(torrentsLock))
                    for (int i = 0; i < torrents.Count; i++)
                        speed += torrents[i].Monitor.UploadSpeed;

                return speed;
            }
        }


        /// <summary>
        /// Unregisters the TorrentManager from this engine
        /// </summary>
        /// <param name="manager"></param>
        public void Unregister(TorrentManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");

            if (manager.Engine != this)
                throw new TorrentException("The manager has not been registered with this engine");

            if (manager.State != TorrentState.Stopped)
                throw new TorrentException("The manager must be stopped before it can be unregistered");

            using (new WriterLock(torrentsLock))
                this.torrents.Remove(manager);

            manager.Engine = null;
        }

        #endregion


        #region Private/Internal methods


        private void AsyncStatsUpdate(object args)
        {
            if (StatsUpdate != null)
                StatsUpdate(this, (StateUpdateEventArgs)args);
        }


        private void LogicTick(object sender, ElapsedEventArgs e)
        {
            tickCount++;

            using(new ReaderLock(this.torrentsLock))
            for (int i = 0; i < this.torrents.Count; i++)
            {
                switch (this.torrents[i].State)
                {
                    case (TorrentState.Downloading):
                        this.torrents[i].DownloadLogic(tickCount);
                        break;

                    case (TorrentState.Seeding):
                        this.torrents[i].SeedingLogic(tickCount);
                        break;

                    case (TorrentState.SuperSeeding):
                        this.torrents[i].SuperSeedingLogic(tickCount);
                        break;

                    default:
                        break;  // Do nothing.
                }
            }

            RaiseStatsUpdate(new StateUpdateEventArgs());
        }


        internal void RaiseStatsUpdate(StateUpdateEventArgs args)
        {
            if (StatsUpdate != null)
                ThreadPool.QueueUserWorkItem(new WaitCallback(AsyncStatsUpdate), args);
        }


        internal void Start()
        {
            if (!this.listener.IsListening)
                this.listener.Start();      // Start Listening for connections

            if (!timer.Enabled)
                timer.Enabled = true;       // Start logic ticking
        }


        internal void Stop()
        {
            using (new ReaderLock(this.torrentsLock))
                for (int i = 0; i < this.torrents.Count; i++)
                    if (this.torrents[i].State != TorrentState.Stopped)
                        return;

            timer.Enabled = false;              // All the torrents are stopped, so stop ticking

            if (this.listener.IsListening)      // Also stop listening for incoming connections
                this.listener.Stop();
        }

        #endregion





    }
}