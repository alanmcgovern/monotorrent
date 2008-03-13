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
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Common;
using MonoTorrent.Client.Managers;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client.PieceWriters;

namespace MonoTorrent.Client
{
    /// <summary>
    /// The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IDisposable
    {
        private static Random random = new Random();
        #region Global Constants

        public static readonly bool SupportsExtended = false;
        public static readonly bool SupportsFastPeer = true;
        public static readonly bool SupportsEncryption = true;
        public static readonly bool SupportsEndgameMode = false;
        public static readonly bool SupportsDht = false;
        internal const int TickLength = 500;    // A logic tick will be performed every TickLength miliseconds
       
        #endregion


        #region Events

        public event EventHandler<StatsUpdateEventArgs> StatsUpdate;

        #endregion


        #region Member Variables

        internal object asyncCompletionLock;     // The lock used to avoid nasty race conditions when async methods are returned
        internal static readonly BufferManager BufferManager = new BufferManager();
        private ConnectionManager connectionManager;
        private DiskManager diskManager;
        private ListenManager listenManager;         // Listens for incoming connections and passes them off to the correct TorrentManager
        private readonly string peerId;
        private EngineSettings settings;
        private System.Timers.Timer timer;      // The timer used to call the logic methods for the torrent managers
        private int tickCount;
        private MonoTorrentCollection<TorrentManager> torrents;
        internal ReaderWriterLock torrentsLock;

        #endregion


        #region Properties

        /// <summary>
        /// The connection manager which manages all the connections for the library
        /// </summary>
        public ConnectionManager ConnectionManager
        {
            get { return this.connectionManager; }
        }


        /// <summary>
        /// 
        /// </summary>
        public DiskManager DiskManager
        {
            get { return diskManager; }
        }

        public ListenManager ListenManager
        {
            get { return listenManager; }
        }

        /// <summary>
        /// True if the engine has been started
        /// </summary>
        public bool IsRunning
        {
            get { return this.timer.Enabled; }
        }


        /// <summary>
        /// Returns the engines PeerID
        /// </summary>
        public string PeerId
        {
            get { return peerId; }
        }


        /// <summary>
        /// The engines settings
        /// </summary>
        public EngineSettings Settings
        {
            get { return this.settings; }
        }


        /// <summary>
        /// The TorrentManager's loaded into the engine
        /// </summary>
        internal MonoTorrentCollection<TorrentManager> Torrents
        {
            get { return this.torrents; }
            set { this.torrents = value; }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new ClientEngine
        /// </summary>
        /// <param name="engineSettings">The engine settings to use</param>
        /// <param name="defaultTorrentSettings">The default settings for new torrents</param>
        public ClientEngine(EngineSettings engineSettings)
        {
            Initialise(engineSettings, null, null);
            this.peerId = GeneratePeerId();
        }

        public ClientEngine(EngineSettings settings, PieceWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");

            Initialise(settings, null, writer);
            this.peerId = GeneratePeerId();
        }

        /// <summary>
        /// Creates a new ClientEngine
        /// </summary>
        /// <param name="engineSettings">The engine settings to use</param>
        /// <param name="defaultTorrentSettings">The default settings for new torrents</param>
        public ClientEngine(EngineSettings engineSettings, ConnectionListenerBase listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            Initialise(engineSettings, listener, null);
            this.peerId = GeneratePeerId();
        }

        public ClientEngine(EngineSettings engineSettings, ConnectionListenerBase listener, PieceWriter writer)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");
            if (writer == null)
                throw new ArgumentNullException("writer");
            
            Initialise(engineSettings, listener, writer);
            this.peerId = GeneratePeerId();
        }

        private void Initialise(EngineSettings engineSettings, ConnectionListenerBase listener, PieceWriter writer)
        {
            if (engineSettings == null)
                throw new ArgumentNullException("engineSettings");

            this.settings = engineSettings;

            // Wrap a memory buffer around the disk writer
            if (writer == null)
            {
                writer = new DiskWriter(engineSettings.MaxOpenFiles);
                //writer = new MemoryWriter(writer);
            }


            this.asyncCompletionLock = new object();
            this.connectionManager = new ConnectionManager(this);
            this.diskManager = new DiskManager(this, writer);
            this.listenManager = new ListenManager(this);
            this.timer = new System.Timers.Timer(TickLength);
            this.torrents = new MonoTorrentCollection<TorrentManager>();
            this.torrentsLock = new ReaderWriterLock();
            this.timer.Elapsed += new ElapsedEventHandler(LogicTick);

            if (listener == null)
            {
                listener = new SocketListener(new IPEndPoint(IPAddress.Any, engineSettings.ListenPort));
                listenManager.Register(listener);
                listener.Start();
            }
            else
            {
                listenManager.Register(listener);
            }
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

            using (new ReaderLock(this.torrentsLock))
                return torrents.Exists(delegate(TorrentManager m) { return Toolbox.ByteMatch(m.Torrent.infoHash, torrent.infoHash); });
        }


        /// <summary>
        /// Returns true if the TorrentManager has been registered with this engine
        /// </summary>
        /// <param name="manager"></param>
        /// <returns></returns>
        public bool Contains(TorrentManager manager)
        {
            return manager == null ? false : Contains(manager.Torrent);
        }


        /// <summary>
        /// Disposes the Engine as well as all TorrentManagers still registered with the engine
        /// </summary>
        public void Dispose()
        {
            using (new WriterLock(this.torrentsLock))
            {
                while (this.torrents.Count > 0)
                {
                    TorrentManager t = torrents[0];
                    if (t.State != TorrentState.Stopped)
                        t.Stop().WaitOne();

                    Unregister(t);
                    t.Dispose();
                }
                this.diskManager.Dispose();
            }

            lock (asyncCompletionLock)
            {
                this.listenManager.Dispose();
                this.timer.Dispose();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static string GeneratePeerId()
        {
            StringBuilder sb = new StringBuilder(20);

            sb.Append(Common.VersionInfo.ClientVersion);
            lock (random)
                for (int i = 0; i < 12; i++)
                    sb.Append(random.Next(0, 9));

            return sb.ToString();
        }


        /// <summary>
        /// Pauses all TorrentManagers which are registered to this engine
        /// </summary>
        public void PauseAll()
        {
            lock (asyncCompletionLock)
                using (new ReaderLock(this.torrentsLock))
                    torrents.ForEach(delegate(TorrentManager m) { m.Pause(); });
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
            {
                if (Contains(manager.Torrent))
                    throw new TorrentException("A manager for this torrent has already been registered");
                this.torrents.Add(manager);
            }
            manager.PieceHashed += PieceHashed;
            manager.Engine = this;
/*
            manager.PieceHashed += delegate (object sender, PieceHashedEventArgs e) {
                diskManager.Writer.RemovePressure(e.TorrentManager, e.PieceIndex);
            };

            manager.PieceManager.BlockRequested += delegate(object sender, BlockEventArgs e) {
                diskManager.Writer.AddPressure(e.TorrentManager, e.Piece.Index, e.Block.StartOffset / Piece.BlockSize);
            };
*/
        }


        /// <summary>
        /// Starts all stopped or paused TorrentManagers which are registered to this engine
        /// </summary>
        public void StartAll()
        {
            lock (asyncCompletionLock)
                using (new ReaderLock(this.torrentsLock))
                    torrents.ForEach(delegate(TorrentManager m) { m.Start(); });
        }


        /// <summary>
        /// Stops all TorrentManagers which are registered to this engine
        /// </summary>
        public WaitHandle[] StopAll()
        {
            List<WaitHandle> handles = new List<WaitHandle>();
            lock (asyncCompletionLock)
                using (new ReaderLock(this.torrentsLock))
                    torrents.ForEach(delegate(TorrentManager m) { handles.Add(m.Stop()); });

            return handles.ToArray();
        }


        /// <summary>
        /// Returns the combined download speed of all active torrents in bytes/sec
        /// </summary>
        public int TotalDownloadSpeed
        {
            get
            {
                using (new ReaderLock(torrentsLock))
                    return Toolbox.Accumulate<TorrentManager>(torrents, delegate(TorrentManager m) { return m.Monitor.DownloadSpeed; });
            }
        }


        /// <summary>
        /// Returns the combined upload speed of all active torrents in bytes/sec
        /// </summary>
        public int TotalUploadSpeed
        {
            get
            {
                using (new ReaderLock(torrentsLock))
                    return Toolbox.Accumulate<TorrentManager>(torrents, delegate(TorrentManager m) { return m.Monitor.UploadSpeed; });
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

            manager.PieceHashed -= PieceHashed;
            manager.Engine = null;
        }

        #endregion


        #region Private/Internal methods

        private void LogicTick(object sender, ElapsedEventArgs e)
        {
            tickCount++;

            if (tickCount % (1000 / TickLength) == 0)
            {
                diskManager.TickMonitors();
                diskManager.rateLimiter.UpdateDownloadChunks((int)(settings.MaxWriteRate * 1024),
                                                             (int)(settings.MaxReadRate * 1024),
                                                             (int)(diskManager.WriteRate * 1024),
                                                             (int)(diskManager.ReadRate * 1024));
            }
            using(new ReaderLock(this.torrentsLock))
            for (int i = 0; i < this.torrents.Count; i++)
            {
                this.torrents[i].PreLogicTick(tickCount);
                switch (this.torrents[i].State)
                {
                    case (TorrentState.Downloading):
                        this.torrents[i].DownloadLogic(tickCount);
                        break;

                    case (TorrentState.Seeding):
                        this.torrents[i].SeedingLogic(tickCount);
                        break;

                    default:
                        break;  // Do nothing.
                }
                this.torrents[i].PostLogicTick(tickCount);
            }

            RaiseStatsUpdate(new StatsUpdateEventArgs());
        }

        private void PieceHashed(object sender, PieceHashedEventArgs e)
        {
            diskManager.QueueFlush(e.TorrentManager, e.PieceIndex);
        }

        internal void RaiseStatsUpdate(StatsUpdateEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<StatsUpdateEventArgs>(StatsUpdate, this, args);
        }


        internal void Start()
        {
            lock (asyncCompletionLock)
            {
                if (!timer.Enabled)
                    timer.Enabled = true;       // Start logic ticking
            }
        }


        internal void Stop()
        {
            lock (asyncCompletionLock)
            {
                // If all the torrents are stopped, stop ticking
                using (new ReaderLock(this.torrentsLock))
                    timer.Enabled = torrents.Exists(delegate(TorrentManager m) { return m.State != TorrentState.Stopped; });
            }
        }

        #endregion
    }
}