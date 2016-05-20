using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    ///     The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IDisposable
    {
        internal static MainLoop MainLoop = new MainLoop("Client Engine Loop");
        private static readonly Random random = new Random();

        #region Global Constants

        // To support this I need to ensure that the transition from
        // InitialSeeding -> Regular seeding either closes all existing
        // connections or sends HaveAll messages, or sends HaveMessages.
        public static readonly bool SupportsInitialSeed = true;
        public static readonly bool SupportsLocalPeerDiscovery = true;
        public static readonly bool SupportsWebSeed = true;
        public static readonly bool SupportsExtended = true;
        public static readonly bool SupportsFastPeer = true;
        public static readonly bool SupportsEncryption = true;
        public static readonly bool SupportsEndgameMode = true;
#if !DISABLE_DHT
        public static readonly bool SupportsDht = true;
#else
        public static readonly bool SupportsDht = false;
#endif
        internal const int TickLength = 500; // A logic tick will be performed every TickLength miliseconds

        #endregion

        #region Events

        public event EventHandler<StatsUpdateEventArgs> StatsUpdate;
        public event EventHandler<CriticalExceptionEventArgs> CriticalException;

        public event EventHandler<TorrentEventArgs> TorrentRegistered;
        public event EventHandler<TorrentEventArgs> TorrentUnregistered;

        #endregion

        #region Member Variables

        internal static readonly BufferManager BufferManager = new BufferManager();

        private readonly ListenManager listenManager;
        // Listens for incoming connections and passes them off to the correct TorrentManager

        private readonly LocalPeerManager localPeerManager;
        private readonly LocalPeerListener localPeerListener;
        private int tickCount;
        private readonly List<TorrentManager> torrents;
        private readonly ReadOnlyCollection<TorrentManager> torrentsReadonly;
        private RateLimiterGroup uploadLimiter;
        private RateLimiterGroup downloadLimiter;

        #endregion

        #region Properties

        public ConnectionManager ConnectionManager { get; }

#if !DISABLE_DHT
        public IDhtEngine DhtEngine { get; private set; }
#endif

        public DiskManager DiskManager { get; }

        public bool Disposed { get; private set; }

        public PeerListener Listener { get; }

        public bool LocalPeerSearchEnabled
        {
            get { return localPeerListener.Status != ListenerStatus.NotListening; }
            set
            {
                if (value && !LocalPeerSearchEnabled)
                    localPeerListener.Start();
                else if (!value && LocalPeerSearchEnabled)
                    localPeerListener.Stop();
            }
        }

        public bool IsRunning { get; private set; }

        public string PeerId { get; }

        public EngineSettings Settings { get; }

        public IList<TorrentManager> Torrents
        {
            get { return torrentsReadonly; }
        }

        #endregion

        #region Constructors

        public ClientEngine(EngineSettings settings)
            : this(settings, new DiskWriter())
        {
        }

        public ClientEngine(EngineSettings settings, PieceWriter writer)
            : this(settings, new SocketListener(new IPEndPoint(IPAddress.Any, 0)), writer)

        {
        }

        public ClientEngine(EngineSettings settings, PeerListener listener)
            : this(settings, listener, new DiskWriter())
        {
        }

        public ClientEngine(EngineSettings settings, PeerListener listener, PieceWriter writer)
        {
            Check.Settings(settings);
            Check.Listener(listener);
            Check.Writer(writer);

            Listener = listener;
            Settings = settings;

            ConnectionManager = new ConnectionManager(this);
            RegisterDht(new NullDhtEngine());
            DiskManager = new DiskManager(this, writer);
            listenManager = new ListenManager(this);
            MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(TickLength), delegate
            {
                if (IsRunning && !Disposed)
                    LogicTick();
                return !Disposed;
            });
            torrents = new List<TorrentManager>();
            torrentsReadonly = new ReadOnlyCollection<TorrentManager>(torrents);
            CreateRateLimiters();
            PeerId = GeneratePeerId();

            localPeerListener = new LocalPeerListener(this);
            localPeerManager = new LocalPeerManager();
            LocalPeerSearchEnabled = SupportsLocalPeerDiscovery;
            listenManager.Register(listener);
            // This means we created the listener in the constructor
            if (listener.Endpoint.Port == 0)
                listener.ChangeEndpoint(new IPEndPoint(IPAddress.Any, settings.ListenPort));
        }

        private void CreateRateLimiters()
        {
            var downloader = new RateLimiter();
            downloadLimiter = new RateLimiterGroup();
            downloadLimiter.Add(new DiskWriterLimiter(DiskManager));
            downloadLimiter.Add(downloader);

            var uploader = new RateLimiter();
            uploadLimiter = new RateLimiterGroup();
            downloadLimiter.Add(new DiskWriterLimiter(DiskManager));
            uploadLimiter.Add(uploader);

            MainLoop.QueueTimeout(TimeSpan.FromSeconds(1), delegate
            {
                downloader.UpdateChunks(Settings.GlobalMaxDownloadSpeed, TotalDownloadSpeed);
                uploader.UpdateChunks(Settings.GlobalMaxUploadSpeed, TotalUploadSpeed);
                return !Disposed;
            });
        }

        #endregion

        #region Methods

        public void ChangeListenEndpoint(IPEndPoint endpoint)
        {
            Check.Endpoint(endpoint);

            Settings.ListenPort = endpoint.Port;
            Listener.ChangeEndpoint(endpoint);
        }

        private void CheckDisposed()
        {
            if (Disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public bool Contains(InfoHash infoHash)
        {
            CheckDisposed();
            if (infoHash == null)
                return false;

            return torrents.Exists(delegate(TorrentManager m) { return m.InfoHash.Equals(infoHash); });
        }

        public bool Contains(Torrent torrent)
        {
            CheckDisposed();
            if (torrent == null)
                return false;

            return Contains(torrent.InfoHash);
        }

        public bool Contains(TorrentManager manager)
        {
            CheckDisposed();
            if (manager == null)
                return false;

            return Contains(manager.Torrent);
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;
            MainLoop.QueueWait(delegate
            {
                DhtEngine.Dispose();
                DiskManager.Dispose();
                listenManager.Dispose();
                localPeerListener.Stop();
                localPeerManager.Dispose();
            });
        }

        private static string GeneratePeerId()
        {
            var sb = new StringBuilder(20);

            sb.Append(VersionInfo.ClientVersion);
            lock (random)
                while (sb.Length < 20)
                    sb.Append(random.Next(0, 9));

            return sb.ToString();
        }

        public void PauseAll()
        {
            CheckDisposed();
            MainLoop.QueueWait(delegate
            {
                foreach (var manager in torrents)
                    manager.Pause();
            });
        }

        public void Register(TorrentManager manager)
        {
            CheckDisposed();
            Check.Manager(manager);

            MainLoop.QueueWait(delegate
            {
                if (manager.Engine != null)
                    throw new TorrentException("This manager has already been registered");

                if (Contains(manager.Torrent))
                    throw new TorrentException("A manager for this torrent has already been registered");
                torrents.Add(manager);
                manager.PieceHashed += PieceHashed;
                manager.Engine = this;
                manager.DownloadLimiter.Add(downloadLimiter);
                manager.UploadLimiter.Add(uploadLimiter);
                if (DhtEngine != null && manager.Torrent != null && manager.Torrent.Nodes != null &&
                    DhtEngine.State != DhtState.Ready)
                {
                    try
                    {
                        DhtEngine.Add(manager.Torrent.Nodes);
                    }
                    catch
                    {
                        // FIXME: Should log this somewhere, though it's not critical
                    }
                }
            });

            if (TorrentRegistered != null)
                TorrentRegistered(this, new TorrentEventArgs(manager));
        }

        public void RegisterDht(IDhtEngine engine)
        {
            MainLoop.QueueWait(delegate
            {
                if (DhtEngine != null)
                {
                    DhtEngine.StateChanged -= DhtEngineStateChanged;
                    DhtEngine.Stop();
                    DhtEngine.Dispose();
                }
                DhtEngine = engine ?? new NullDhtEngine();
            });

            DhtEngine.StateChanged += DhtEngineStateChanged;
        }

        private void DhtEngineStateChanged(object o, EventArgs e)
        {
            if (DhtEngine.State != DhtState.Ready)
                return;

            MainLoop.Queue(delegate
            {
                foreach (var manager in torrents)
                {
                    if (!manager.CanUseDht)
                        continue;

                    DhtEngine.Announce(manager.InfoHash, Listener.Endpoint.Port);
                    DhtEngine.GetPeers(manager.InfoHash);
                }
            });
        }

        public void StartAll()
        {
            CheckDisposed();
            MainLoop.QueueWait(delegate
            {
                for (var i = 0; i < torrents.Count; i++)
                    torrents[i].Start();
            });
        }

        public void StopAll()
        {
            CheckDisposed();

            MainLoop.QueueWait(delegate
            {
                for (var i = 0; i < torrents.Count; i++)
                    torrents[i].Stop();
            });
        }

        public int TotalDownloadSpeed
        {
            get
            {
                return
                    (int)
                        Toolbox.Accumulate(torrents,
                            delegate(TorrentManager m) { return m.Monitor.DownloadSpeed; });
            }
        }

        public int TotalUploadSpeed
        {
            get
            {
                return
                    (int)
                        Toolbox.Accumulate(torrents,
                            delegate(TorrentManager m) { return m.Monitor.UploadSpeed; });
            }
        }

        public void Unregister(TorrentManager manager)
        {
            CheckDisposed();
            Check.Manager(manager);

            MainLoop.QueueWait(delegate
            {
                if (manager.Engine != this)
                    throw new TorrentException("The manager has not been registered with this engine");

                if (manager.State != TorrentState.Stopped)
                    throw new TorrentException("The manager must be stopped before it can be unregistered");

                torrents.Remove(manager);

                manager.PieceHashed -= PieceHashed;
                manager.Engine = null;
                manager.DownloadLimiter.Remove(downloadLimiter);
                manager.UploadLimiter.Remove(uploadLimiter);
            });

            if (TorrentUnregistered != null)
                TorrentUnregistered(this, new TorrentEventArgs(manager));
        }

        #endregion

        #region Private/Internal methods

        internal void Broadcast(TorrentManager manager)
        {
            if (LocalPeerSearchEnabled)
                localPeerManager.Broadcast(manager);
        }

        private void LogicTick()
        {
            tickCount++;

            if (tickCount%(1000/TickLength) == 0)
            {
                DiskManager.writeLimiter.UpdateChunks(Settings.MaxWriteRate, DiskManager.WriteRate);
                DiskManager.readLimiter.UpdateChunks(Settings.MaxReadRate, DiskManager.ReadRate);
            }

            ConnectionManager.TryConnect();
            for (var i = 0; i < torrents.Count; i++)
                torrents[i].Mode.Tick(tickCount);

            RaiseStatsUpdate(new StatsUpdateEventArgs());
        }

        internal void RaiseCriticalException(CriticalExceptionEventArgs e)
        {
            Toolbox.RaiseAsyncEvent(CriticalException, this, e);
        }

        private void PieceHashed(object sender, PieceHashedEventArgs e)
        {
            if (e.TorrentManager.State != TorrentState.Hashing)
                DiskManager.QueueFlush(e.TorrentManager, e.PieceIndex);
        }

        internal void RaiseStatsUpdate(StatsUpdateEventArgs args)
        {
            Toolbox.RaiseAsyncEvent(StatsUpdate, this, args);
        }


        internal void Start()
        {
            CheckDisposed();
            IsRunning = true;
            if (Listener.Status == ListenerStatus.NotListening)
                Listener.Start();
        }


        internal void Stop()
        {
            CheckDisposed();
            // If all the torrents are stopped, stop ticking
            IsRunning = torrents.Exists(delegate(TorrentManager m) { return m.State != TorrentState.Stopped; });
            if (!IsRunning)
                Listener.Stop();
        }

        #endregion
    }
}