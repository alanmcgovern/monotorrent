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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client.PortForwarding;
using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Logging;

namespace MonoTorrent.Client
{
    /// <summary>
    /// The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IDisposable
    {
        internal static readonly MainLoop MainLoop = new MainLoop ("Client Engine Loop");
        static readonly Logger Log = Logger.Create ();

        /// <summary>
        /// An un-seeded random number generator which will not generate the same
        /// random sequence when the application is restarted.
        /// </summary>
        static readonly Random PeerIdRandomGenerator = new Random ();
        #region Global Constants

        // This is the number of 16kB requests which can be queued against one peer.
        internal static readonly int DefaultMaxPendingRequests = 256;
        public static readonly bool SupportsInitialSeed = false;
        public static readonly bool SupportsLocalPeerDiscovery = true;
        public static readonly bool SupportsWebSeed = true;
        public static readonly bool SupportsExtended = true;
        public static readonly bool SupportsFastPeer = true;
        public static readonly bool SupportsEncryption = true;
        public static readonly bool SupportsEndgameMode = true;
        public static readonly bool SupportsDht = true;
        internal const int TickLength = 500;    // A logic tick will be performed every TickLength miliseconds

        #endregion


        #region Events

        public event EventHandler<StatsUpdateEventArgs> StatsUpdate;
        public event EventHandler<CriticalExceptionEventArgs> CriticalException;

        #endregion


        #region Member Variables

        readonly ListenManager listenManager;         // Listens for incoming connections and passes them off to the correct TorrentManager
        int tickCount;
        /// <summary>
        /// The <see cref="TorrentManager"/> instances registered by the user.
        /// </summary>
        readonly List<TorrentManager> publicTorrents;

        /// <summary>
        /// The <see cref="TorrentManager"/> instances registered by the user and the instances
        /// implicitly created by <see cref="DownloadMetadataAsync(MagnetLink, CancellationToken)"/>.
        /// </summary>
        readonly List<TorrentManager> allTorrents;

        readonly RateLimiter uploadLimiter;
        readonly RateLimiterGroup uploadLimiters;
        readonly RateLimiter downloadLimiter;
        readonly RateLimiterGroup downloadLimiters;

        #endregion


        #region Properties

        public ConnectionManager ConnectionManager { get; }

        public IDhtEngine DhtEngine { get; private set; }

        IDhtListener DhtListener { get; set; }

        public DiskManager DiskManager { get; }

        public bool Disposed { get; private set; }

        internal IPeerListener Listener { get; set; }

        internal ILocalPeerDiscovery LocalPeerDiscovery { get; private set; }

        /// <summary>
        /// When <see cref="EngineSettings.AllowPortForwarding"/> is set to true, this will return a representation
        /// of the ports the engine is managing.
        /// </summary>
        public Mappings PortMappings => Settings.AllowPortForwarding ? PortForwarder.Mappings : Mappings.Empty;

        public bool IsRunning { get; private set; }

        public BEncodedString PeerId { get; }

        internal IPortForwarder PortForwarder { get; }

        public EngineSettings Settings { get; private set; }

        public IList<TorrentManager> Torrents { get; }

        public long TotalDownloadSpeed {
            get {
                long total = 0;
                for (int i = 0; i < publicTorrents.Count; i++)
                    total += publicTorrents[i].Monitor.DownloadSpeed;
                return total;
            }
        }

        public long TotalUploadSpeed {
            get {
                long total = 0;
                for (int i = 0; i < publicTorrents.Count; i++)
                    total += publicTorrents[i].Monitor.UploadSpeed;
                return total;
            }
        }

        #endregion


        #region Constructors

        public ClientEngine ()
            : this(new EngineSettings ())
        {

        }

        public ClientEngine (EngineSettings settings)
            : this (settings, new DiskWriter ())
        {

        }

        public ClientEngine (EngineSettings settings, IPieceWriter writer)
        {
            Check.Settings (settings);
            Check.Writer (writer);

            // This is just a sanity check to make sure the ReusableTasks.dll assembly is
            // loadable.
            GC.KeepAlive (ReusableTasks.ReusableTask.CompletedTask);

            PeerId = GeneratePeerId ();
            Settings = settings ?? throw new ArgumentNullException (nameof (settings));

            allTorrents = new List<TorrentManager> ();
            publicTorrents = new List<TorrentManager> ();
            Torrents = new ReadOnlyCollection<TorrentManager> (publicTorrents);

            DiskManager = new DiskManager (Settings, writer);
            ConnectionManager = new ConnectionManager (PeerId, Settings, DiskManager);
            listenManager = new ListenManager (this);
            PortForwarder = new MonoNatPortForwarder ();

            MainLoop.QueueTimeout (TimeSpan.FromMilliseconds (TickLength), delegate {
                if (IsRunning && !Disposed)
                    LogicTick ();
                return !Disposed;
            });

            downloadLimiter = new RateLimiter ();
            downloadLimiters = new RateLimiterGroup {
                new DiskWriterLimiter(DiskManager),
                downloadLimiter,
            };

            uploadLimiter = new RateLimiter ();
            uploadLimiters = new RateLimiterGroup {
                uploadLimiter
            };

            Listener = PeerListenerFactory.CreateTcp (settings.ListenPort);
            listenManager.SetListener (Listener);

            DhtListener = DhtListenerFactory.CreateUdp (settings.DhtPort);
            DhtEngine = settings.DhtPort == -1 ? new NullDhtEngine () : DhtEngineFactory.Create (DhtListener);
            DhtEngine.StateChanged += DhtEngineStateChanged;
            DhtEngine.PeersFound += DhtEnginePeersFound;

            RegisterLocalPeerDiscovery (settings.AllowLocalPeerDiscovery && settings.ListenPort > 0 ? LocalPeerDiscoveryFactory.Create (settings.ListenPort) : null);
        }

        #endregion


        #region Methods

        void CheckDisposed ()
        {
            if (Disposed)
                throw new ObjectDisposedException (GetType ().Name);
        }

        public bool Contains (InfoHash infoHash)
        {
            CheckDisposed ();
            if (infoHash == null)
                return false;

            return publicTorrents.Exists (m => m.InfoHash.Equals (infoHash));
        }

        public bool Contains (Torrent torrent)
        {
            CheckDisposed ();
            if (torrent == null)
                return false;

            return Contains (torrent.InfoHash);
        }

        public bool Contains (TorrentManager manager)
        {
            CheckDisposed ();
            if (manager == null)
                return false;

            return Contains (manager.Torrent);
        }

        public void Dispose ()
        {
            if (Disposed)
                return;

            Disposed = true;
            MainLoop.QueueWait (() => {
                Listener.Stop ();
                listenManager.SetListener (null);

                DhtListener.Stop ();
                DhtEngine.SetListener (null);
                DhtEngine.Dispose ();

                DiskManager.Dispose ();
                LocalPeerDiscovery.Stop ();
            });
        }

        /// <summary>
        /// Downloads the .torrent metadata for the provided MagnetLink.
        /// </summary>
        /// <param name="magnetLink">The MagnetLink to get the metadata for.</param>
        /// <param name="token">The cancellation token used to to abort the download. This method will
        /// only complete if the metadata successfully downloads, or the token is cancelled.</param>
        /// <returns></returns>
        public async Task<byte[]> DownloadMetadataAsync (MagnetLink magnetLink, CancellationToken token)
        {
            var manager = new TorrentManager (magnetLink);
            var metadataCompleted = new TaskCompletionSource<byte[]> ();
            using var registration = token.Register (() => metadataCompleted.TrySetResult (null));
            manager.MetadataReceived += (o, e) => metadataCompleted.TrySetResult (e);

            await Register (manager, isPublic: false);
            await manager.StartAsync (metadataOnly: true);
            var data = await metadataCompleted.Task;
            await manager.StopAsync ();
            await Unregister (manager);

            token.ThrowIfCancellationRequested ();
            return data;
        }

        async void HandleLocalPeerFound (object sender, LocalPeerFoundEventArgs args)
        {
            try {
                await MainLoop;

                TorrentManager manager = allTorrents.FirstOrDefault (t => t.InfoHash == args.InfoHash);
                // There's no TorrentManager in the engine
                if (manager == null)
                    return;

                // The torrent is marked as private, so we can't add random people
                if (manager.HasMetadata && manager.Torrent.IsPrivate) {
                    manager.RaisePeersFound (new LocalPeersAdded (manager, 0, 0));
                } else {
                    // Add new peer to matched Torrent
                    var peer = new Peer ("", args.Uri);
                    int peersAdded = manager.AddPeer (peer, fromTrackers: false, prioritise: true) ? 1 : 0;
                    manager.RaisePeersFound (new LocalPeersAdded (manager, peersAdded, 1));
                }
            } catch {
                // We don't care if the peer couldn't be added (for whatever reason)
            }
        }

        public async Task PauseAll ()
        {
            CheckDisposed ();
            await MainLoop;

            var tasks = new List<Task> ();
            foreach (TorrentManager manager in publicTorrents)
                tasks.Add (manager.PauseAsync ());
            await Task.WhenAll (tasks);
        }

        public async Task Register (TorrentManager manager)
            => await Register (manager, true);

        async Task Register (TorrentManager manager, bool isPublic)
        {
            CheckDisposed ();
            Check.Manager (manager);

            await MainLoop;
            if (manager.Engine != null)
                throw new TorrentException ("This manager has already been registered");

            if (Contains (manager.Torrent))
                throw new TorrentException ("A manager for this torrent has already been registered");

            allTorrents.Add (manager);
            if (isPublic)
                publicTorrents.Add (manager);
            ConnectionManager.Add (manager);
            listenManager.Add (manager.InfoHash);

            manager.Engine = this;
            manager.DownloadLimiters.Add (downloadLimiters);
            manager.UploadLimiters.Add (uploadLimiters);
            if (DhtEngine != null && manager.Torrent?.Nodes != null && DhtEngine.State != DhtState.Ready) {
                try {
                    DhtEngine.Add (manager.Torrent.Nodes);
                } catch {
                    // FIXME: Should log this somewhere, though it's not critical
                }
            }
        }

        async Task RegisterDht (IDhtEngine engine)
        {
            if (DhtEngine != null) {
                DhtEngine.StateChanged -= DhtEngineStateChanged;
                DhtEngine.PeersFound -= DhtEnginePeersFound;
                await DhtEngine.StopAsync ();
                DhtEngine.Dispose ();
            }
            DhtEngine = engine ?? new NullDhtEngine ();

            DhtEngine.StateChanged += DhtEngineStateChanged;
            DhtEngine.PeersFound += DhtEnginePeersFound;
            if (IsRunning)
                await DhtEngine.StartAsync ();
        }

        void RegisterLocalPeerDiscovery (ILocalPeerDiscovery localPeerDiscovery)
        {
            if (LocalPeerDiscovery != null) {
                LocalPeerDiscovery.PeerFound -= HandleLocalPeerFound;
                LocalPeerDiscovery.Stop ();
            }

            if (!SupportsLocalPeerDiscovery || localPeerDiscovery == null)
                localPeerDiscovery = new NullLocalPeerDiscovery ();

            LocalPeerDiscovery = localPeerDiscovery;

            if (LocalPeerDiscovery != null) {
                LocalPeerDiscovery.PeerFound += HandleLocalPeerFound;
                if (IsRunning)
                    LocalPeerDiscovery.Start ();
            }
        }

        async void DhtEnginePeersFound (object o, PeersFoundEventArgs e)
        {
            await MainLoop;

            TorrentManager manager = allTorrents.FirstOrDefault (t => t.InfoHash == e.InfoHash);

            if (manager == null)
                return;

            if (manager.CanUseDht) {
                int successfullyAdded = await manager.AddPeersAsync (e.Peers);
                manager.RaisePeersFound (new DhtPeersAdded (manager, successfullyAdded, e.Peers.Count));
            } else {
                // This is only used for unit testing to validate that even if the DHT engine
                // finds peers for a private torrent, we will not add them to the manager.
                manager.RaisePeersFound (new DhtPeersAdded (manager, 0, 0));
            }
        }

        async void DhtEngineStateChanged (object o, EventArgs e)
        {
            if (DhtEngine.State != DhtState.Ready)
                return;

            await MainLoop;
            foreach (TorrentManager manager in allTorrents) {
                if (!manager.CanUseDht)
                    continue;

                DhtEngine.Announce (manager.InfoHash, Settings.ListenPort);
                DhtEngine.GetPeers (manager.InfoHash);
            }
        }

        public async Task StartAllAsync ()
        {
            CheckDisposed ();

            await MainLoop;

            var tasks = new List<Task> ();
            for (int i = 0; i < publicTorrents.Count; i++)
                tasks.Add (publicTorrents[i].StartAsync ());
            await Task.WhenAll (tasks);
        }

        /// <summary>
        /// Stops all active <see cref="TorrentManager"/> instances.
        /// </summary>
        /// <returns></returns>
        public Task StopAllAsync ()
        {
            return StopAllAsync (Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Stops all active <see cref="TorrentManager"/> instances. The final announce for each <see cref="TorrentManager"/> will be limited
        /// to the maximum of either 2 seconds or <paramref name="timeout"/> seconds.
        /// </summary>
        /// <param name="timeout">The timeout for the final tracker announce.</param>
        /// <returns></returns>
        public async Task StopAllAsync (TimeSpan timeout)
        {
            CheckDisposed ();

            await MainLoop;
            var tasks = new List<Task> ();
            for (int i = 0; i < publicTorrents.Count; i++)
                tasks.Add (publicTorrents[i].StopAsync (timeout));
            await Task.WhenAll (tasks);
        }

        public async Task Unregister (TorrentManager manager)
        {
            CheckDisposed ();
            Check.Manager (manager);

            await MainLoop;
            if (manager.Engine != this)
                throw new TorrentException ("The manager has not been registered with this engine");

            if (manager.State != TorrentState.Stopped)
                throw new TorrentException ("The manager must be stopped before it can be unregistered");

            allTorrents.Remove (manager);
            publicTorrents.Remove (manager);
            ConnectionManager.Remove (manager);
            listenManager.Remove (manager.InfoHash);

            manager.Engine = null;
            manager.DownloadLimiters.Remove (downloadLimiters);
            manager.UploadLimiters.Remove (uploadLimiters);
        }

        #endregion


        #region Private/Internal methods

        void LogicTick ()
        {
            tickCount++;

            if (tickCount % 2 == 0) {
                downloadLimiter.UpdateChunks (Settings.MaximumDownloadSpeed, TotalDownloadSpeed);
                uploadLimiter.UpdateChunks (Settings.MaximumUploadSpeed, TotalUploadSpeed);
            }

            ConnectionManager.CancelPendingConnects ();
            ConnectionManager.TryConnect ();
            DiskManager.Tick ();

            for (int i = 0; i < allTorrents.Count; i++)
                allTorrents[i].Mode.Tick (tickCount);

            RaiseStatsUpdate (new StatsUpdateEventArgs ());
        }

        internal void RaiseCriticalException (CriticalExceptionEventArgs e)
        {
            CriticalException?.InvokeAsync (this, e);
        }

        internal void RaiseStatsUpdate (StatsUpdateEventArgs args)
        {
            StatsUpdate?.InvokeAsync (this, args);
        }

        internal async Task StartAsync ()
        {
            CheckDisposed ();
            if (!IsRunning) {
                IsRunning = true;

                Listener.Start ();
                LocalPeerDiscovery.Start ();
                await DhtEngine.StartAsync ();

                if (Listener is ISocketListener socketListener)
                    await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Tcp, socketListener.EndPoint.Port));
                else
                    await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Tcp, Settings.ListenPort));

                if (DhtListener.EndPoint != null)
                    await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Udp, DhtListener.EndPoint.Port));
            }
        }

        internal async Task StopAsync ()
        {
            CheckDisposed ();
            // If all the torrents are stopped, stop ticking
            IsRunning = allTorrents.Exists (m => m.State != TorrentState.Stopped);
            if (!IsRunning) {
                Listener.Stop ();
                LocalPeerDiscovery.Stop ();
                await DhtEngine.StopAsync ();
                await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Tcp, Settings.ListenPort), CancellationToken.None);
                await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Udp, Settings.DhtPort), CancellationToken.None);
            }
        }

        public async Task UpdateSettingsAsync (EngineSettings settings)
        {
            await MainLoop;

            var oldSettings = Settings;
            Settings = settings;
            await UpdateSettingsAsync (oldSettings, settings);
        }

        async Task UpdateSettingsAsync (EngineSettings oldSettings, EngineSettings newSettings)
        {
            DiskManager.Settings = newSettings;
            ConnectionManager.Settings = newSettings;

            if (oldSettings.AllowPortForwarding != newSettings.AllowPortForwarding) {
                if (newSettings.AllowPortForwarding)
                    await PortForwarder.StartAsync (CancellationToken.None);
                else
                    await PortForwarder.StopAsync (removeExistingMappings: true, CancellationToken.None);
            }

            if (oldSettings.DhtPort != newSettings.DhtPort) {
                if (DhtListener.EndPoint != null)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Udp, DhtListener.EndPoint.Port), CancellationToken.None);
                else if (oldSettings.DhtPort > 0)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Udp, oldSettings.DhtPort), CancellationToken.None);

                DhtListener = DhtListenerFactory.CreateUdp (newSettings.DhtPort);

                if (oldSettings.DhtPort == -1)
                    await RegisterDht (DhtEngineFactory.Create (DhtListener));
                else if (newSettings.DhtPort == -1)
                    await RegisterDht (new NullDhtEngine ());

                DhtEngine.SetListener (DhtListener);

                if (IsRunning) {
                    DhtListener.Start ();
                    if (Listener is ISocketListener newDhtListener)
                        await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Udp, newDhtListener.EndPoint.Port));
                    else
                        await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Udp, newSettings.DhtPort));
                }
            }

            if (oldSettings.ListenPort != newSettings.ListenPort) {
                if (Listener is ISocketListener oldListener)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Tcp, oldListener.EndPoint.Port), CancellationToken.None);
                else if (oldSettings.ListenPort > 0)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Tcp, oldSettings.ListenPort), CancellationToken.None);

                Listener.Stop ();
                Listener = PeerListenerFactory.CreateTcp (newSettings.ListenPort);
                listenManager.SetListener (Listener);

                if (IsRunning) {
                    Listener.Start ();
                    // The settings could say to listen at port 0, which means 'choose one dynamically'
                    if (Listener is ISocketListener peerListener)
                        await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Tcp, peerListener.EndPoint.Port));
                    else
                        await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Tcp, newSettings.ListenPort));
                }
            }

            // This depends on the Listener binding to it's local port.
            var localPort = newSettings.ListenPort;
            if (Listener is ISocketListener newListener)
                localPort = newListener.EndPoint.Port;

            if ((oldSettings.AllowLocalPeerDiscovery != newSettings.AllowLocalPeerDiscovery) ||
                (oldSettings.ListenPort != newSettings.ListenPort)) {
                RegisterLocalPeerDiscovery (newSettings.AllowLocalPeerDiscovery && localPort > 0 ? new LocalPeerDiscovery (localPort) : null);
            }
        }

        static BEncodedString GeneratePeerId ()
        {
            var sb = new StringBuilder (20);
            sb.Append ("-");
            sb.Append (VersionInfo.ClientVersion);
            sb.Append ("-");

            // Create and use a single Random instance which *does not* use a seed so that
            // the random sequence generated is definitely not the same between application
            // restarts.
            lock (PeerIdRandomGenerator) {
                while (sb.Length < 20)
                    sb.Append (PeerIdRandomGenerator.Next (0, 9));
            }

            return new BEncodedString (sb.ToString ());
        }

        #endregion
    }
}
