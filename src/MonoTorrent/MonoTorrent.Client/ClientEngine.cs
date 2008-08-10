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
        internal static MainLoop MainLoop = new MainLoop("Client Engine Loop");
        private static Random random = new Random();
        #region Global Constants

        public static readonly bool SupportsInitialSeed = false;
        public static readonly bool SupportsWebSeed = false;
        public static readonly bool SupportsExtended = false;
        public static readonly bool SupportsFastPeer = true;
        public static readonly bool SupportsEncryption = true;
        public static readonly bool SupportsEndgameMode = false;
        public static readonly bool SupportsDht = false;
        internal const int TickLength = 500;    // A logic tick will be performed every TickLength miliseconds
       
        #endregion


        #region Events

        public event EventHandler<StatsUpdateEventArgs> StatsUpdate;
        public event EventHandler<CriticalExceptionEventArgs> CriticalException;

        public event EventHandler<TorrentEventArgs> TorrentRegistered;
        public event EventHandler<TorrentEventArgs> TorrentUnregistered;

        #endregion


        #region Member Variables

        internal static readonly BufferManager BufferManager = new BufferManager();
        private ConnectionManager connectionManager;
        private DiskManager diskManager;
        private bool isRunning;
        private ConnectionListenerBase listener;
        private ListenManager listenManager;         // Listens for incoming connections and passes them off to the correct TorrentManager
        private readonly string peerId;
        private EngineSettings settings;
        private int tickCount;
        private MonoTorrentCollection<TorrentManager> torrents;
        internal RateLimiter uploadLimiter;
        internal RateLimiter downloadLimiter;

        #endregion


        #region Properties

        public ConnectionManager ConnectionManager
        {
            get { return this.connectionManager; }
        }

        public DiskManager DiskManager
        {
            get { return diskManager; }
        }

        public ConnectionListenerBase Listener
        {
            get { return this.listener; }
        }

        public bool IsRunning
        {
            get { return this.isRunning; }
        }

        public string PeerId
        {
            get { return peerId; }
        }

        public EngineSettings Settings
        {
            get { return this.settings; }
        }

        internal MonoTorrentCollection<TorrentManager> Torrents
        {
            get { return this.torrents; }
            set { this.torrents = value; }
        }

        #endregion


        #region Constructors

        public ClientEngine(EngineSettings settings)
            : this (settings, new DiskWriter())
        {

        }

        public ClientEngine(EngineSettings settings, PieceWriter writer)
            : this(settings, new SocketListener(new IPEndPoint(IPAddress.Any, 0)), writer)

        {

        }

        public ClientEngine(EngineSettings settings, ConnectionListenerBase listener)
            : this (settings, listener, new DiskWriter())
        {

        }

        public ClientEngine(EngineSettings settings, ConnectionListenerBase listener, PieceWriter writer)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (listener == null)
                throw new ArgumentNullException("listener");
            if (writer == null)
                throw new ArgumentNullException("writer");

            this.listener = listener;
            this.settings = settings;

            this.connectionManager = new ConnectionManager(this);
            this.diskManager = new DiskManager(this, writer);
            this.listenManager = new ListenManager(this);
            MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(TickLength), delegate {
                if (IsRunning)
                    MainLoop.QueueWait((MainLoopTask)LogicTick);
                return true;
            });
            this.torrents = new MonoTorrentCollection<TorrentManager>();
            this.downloadLimiter = new RateLimiter();
            this.uploadLimiter = new RateLimiter();
            this.peerId = GeneratePeerId();

            listenManager.Register(listener);

            // This means we created the listener in the constructor
            if (listener.ListenPort == 0)
                listener.ChangePort(settings.ListenPort);

            listener.Start();
        }

        #endregion


        #region Methods

        public bool Contains(Torrent torrent)
        {
            if (torrent == null)
                return false;

            return torrents.Exists(delegate(TorrentManager m) { return Toolbox.ByteMatch(m.Torrent.infoHash, torrent.infoHash); });
        }

        public bool Contains(TorrentManager manager)
        {
            return manager == null ? false : Contains(manager.Torrent);
        }

        public void Dispose()
        {
            MainLoop.QueueWait(delegate {
                this.diskManager.Dispose();
                this.listenManager.Dispose();
            });
        }

        private static string GeneratePeerId()
        {
            StringBuilder sb = new StringBuilder(20);

            sb.Append(Common.VersionInfo.ClientVersion);
            lock (random)
                for (int i = 0; i < 12; i++)
                    sb.Append(random.Next(0, 9));

            return sb.ToString();
        }

        public void PauseAll()
        {
            MainLoop.QueueWait(delegate {
                foreach (TorrentManager manager in torrents)
                    manager.Pause();
            });
        }

        public void Register(TorrentManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("torrent");

            MainLoop.QueueWait(delegate {
                if (manager.Engine != null)
                    throw new TorrentException("This manager has already been registered");

                if (Contains(manager.Torrent))
                    throw new TorrentException("A manager for this torrent has already been registered");
                this.torrents.Add(manager);
                manager.PieceHashed += PieceHashed;
                manager.Engine = this;
            });

            if (TorrentRegistered != null)
                TorrentRegistered(this, new TorrentEventArgs(manager));
        }

        public void StartAll()
        {
            MainLoop.QueueWait(delegate {
                for (int i = 0; i < torrents.Count; i++)
                    torrents[i].Start();
            });
        }

        public WaitHandle[] StopAll()
        {
            List<WaitHandle> handles = new List<WaitHandle>();

            MainLoop.QueueWait(delegate {
                for (int i = 0; i < torrents.Count; i++)
                    handles.Add(torrents[i].Stop());
            });

            return handles.ToArray();
        }

        public int TotalDownloadSpeed
        {
            get
            {
                return Toolbox.Accumulate<TorrentManager>(torrents, delegate(TorrentManager m) { return m.Monitor.DownloadSpeed; });
            }
        }

        public int TotalUploadSpeed
        {
            get
            {
                return Toolbox.Accumulate<TorrentManager>(torrents, delegate(TorrentManager m) { return m.Monitor.UploadSpeed; });
            }
        }

        public void Unregister(TorrentManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");

            MainLoop.QueueWait(delegate {
                if (manager.Engine != this)
                    throw new TorrentException("The manager has not been registered with this engine");

                if (manager.State != TorrentState.Stopped)
                    throw new TorrentException("The manager must be stopped before it can be unregistered");

                this.torrents.Remove(manager);

                manager.PieceHashed -= PieceHashed;
                manager.Engine = null;
            });

            if (TorrentUnregistered != null)
                TorrentUnregistered(this, new TorrentEventArgs(manager));
        }

        #endregion


        #region Private/Internal methods

        private void LogicTick()
        {
            tickCount++;

            if (tickCount % (1000 / TickLength) == 0)
            {
                diskManager.TickMonitors();
                diskManager.writeLimiter.UpdateChunks(settings.MaxWriteRate, diskManager.WriteRate);
                diskManager.readLimiter.UpdateChunks(settings.MaxReadRate, diskManager.ReadRate);
                downloadLimiter.UpdateChunks(settings.GlobalMaxDownloadSpeed, TotalDownloadSpeed);
                uploadLimiter.UpdateChunks(settings.GlobalMaxUploadSpeed, TotalUploadSpeed);
            }

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

        internal void RaiseCriticalException(CriticalExceptionEventArgs e)
        {
            Toolbox.RaiseAsyncEvent<CriticalExceptionEventArgs>(CriticalException, this, e); 
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
            isRunning = true;
        }


        internal void Stop()
        {
            // If all the torrents are stopped, stop ticking
            isRunning = torrents.Exists(delegate(TorrentManager m) { return m.State != TorrentState.Stopped; });
        }

        #endregion
    }
}