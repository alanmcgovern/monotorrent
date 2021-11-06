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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Logging;
using MonoTorrent.PieceWriter;
using MonoTorrent.PortForwarding;

namespace MonoTorrent.Client
{
    /// <summary>
    /// The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IDisposable
    {
        internal static readonly MainLoop MainLoop = new MainLoop ("Client Engine Loop");
        static readonly Logger Log = Logger.Create (nameof (ClientEngine));

        public static async Task<ClientEngine> RestoreStateAsync (string pathToStateFile)
        {
            await MainLoop.SwitchThread ();
            return await RestoreStateAsync (File.ReadAllBytes (pathToStateFile));
        }

        public static async Task<ClientEngine> RestoreStateAsync (byte[] buffer)
        {
            var state = BEncodedValue.Decode<BEncodedDictionary> (buffer);
            var engineSettings = Serializer.DeserializeEngineSettings ((BEncodedDictionary) state["Settings"]);

            var clientEngine = new ClientEngine (engineSettings);
            TorrentManager manager;
            foreach (BEncodedDictionary torrent in (BEncodedList) state[nameof(clientEngine.Torrents)]) {
                var saveDirectory = ((BEncodedString) torrent[nameof(manager.SavePath)]).Text;
                var streaming = bool.Parse (((BEncodedString) torrent["Streaming"]).Text);
                var torrentSettings = Serializer.DeserializeTorrentSettings ((BEncodedDictionary) torrent[nameof(manager.Settings)]);

                if (torrent.ContainsKey (nameof (manager.MetadataPath))) {
                    var metadataPath = (BEncodedString) torrent[nameof (manager.MetadataPath)];
                    if (streaming)
                        manager = await clientEngine.AddStreamingAsync (metadataPath.Text, saveDirectory, torrentSettings);
                    else
                        manager = await clientEngine.AddAsync (metadataPath.Text, saveDirectory, torrentSettings);

                    foreach (BEncodedDictionary file in (BEncodedList) torrent[nameof(manager.Files)]) {
                        TorrentFileInfo torrentFile;
                        torrentFile = (TorrentFileInfo) manager.Files.Single (t => t.Path == ((BEncodedString) file[nameof(torrentFile.Path)]).Text);
                        torrentFile.Priority = (Priority) Enum.Parse (typeof (Priority), file[nameof(torrentFile.Priority)].ToString ());
                        torrentFile.FullPath = ((BEncodedString) file[nameof(torrentFile.FullPath)]).Text;
                    }
                } else {
                    var magnetLink = MagnetLink.Parse (torrent[nameof (manager.MagnetLink)].ToString ());
                    if (streaming)
                        await clientEngine.AddStreamingAsync (magnetLink, saveDirectory, torrentSettings);
                    else
                        await clientEngine.AddAsync (magnetLink, saveDirectory, torrentSettings);
                }
            }
            return clientEngine;
        }

        public async Task<byte[]> SaveStateAsync ()
        {
            await MainLoop;
            var state = new BEncodedDictionary {
                {nameof (Settings), Serializer.Serialize (Settings) },
            };

            state[nameof (Torrents)] = new BEncodedList (Torrents.Select (t => {
                var dict = new BEncodedDictionary {
                    { nameof (t.MagnetLink),  (BEncodedString) t.MagnetLink.ToV1String () },
                    { nameof(t.SavePath), (BEncodedString) t.SavePath },
                    { nameof(t.Settings), Serializer.Serialize (t.Settings) },
                    { "Streaming", (BEncodedString) (t.StreamProvider != null).ToString ()},
                };

                if (t.HasMetadata) {
                    dict[nameof (t.Files)] = new BEncodedList (t.Files.Select (file =>
                       new BEncodedDictionary {
                            { nameof(file.FullPath), (BEncodedString) file.FullPath },
                            { nameof(file.Path), (BEncodedString) file.Path },
                            { nameof(file.Priority), (BEncodedString) file.Priority.ToString () },
                       }
                    ));
                    dict[nameof (t.MetadataPath)] = (BEncodedString) t.MetadataPath;
                } else {
                }

                return dict;
            }));

            foreach (var v in Torrents)
                await v.MaybeWriteFastResumeAsync ();

            return state.Encode ();
        }

        public async Task SaveStateAsync (string pathToStateFile)
        {
            var state = await SaveStateAsync ();
            await MainLoop.SwitchThread ();
            File.WriteAllBytes (pathToStateFile, state);
        }

        /// <summary>
        /// An un-seeded random number generator which will not generate the same
        /// random sequence when the application is restarted.
        /// </summary>
        static readonly Random PeerIdRandomGenerator = new Random ();

        #region Global Constants

        public static readonly bool SupportsInitialSeed = false;
        public static readonly bool SupportsLocalPeerDiscovery = true;
        public static readonly bool SupportsWebSeed = true;
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

        internal Factories Factories { get; }

        internal IPeerConnectionListener PeerListener { get; set; }

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
            : this (new EngineSettings ())
        {

        }

        public ClientEngine (EngineSettings settings)
            : this (settings, Factories.Default)
        {

        }

        public ClientEngine (EngineSettings settings, Factories factories)
        {
            settings = settings ?? throw new ArgumentNullException (nameof (settings));
            Factories = factories ?? throw new ArgumentNullException (nameof (factories));

            // This is just a sanity check to make sure the ReusableTasks.dll assembly is
            // loadable.
            GC.KeepAlive (ReusableTasks.ReusableTask.CompletedTask);

            PeerId = GeneratePeerId ();
            Settings = settings ?? throw new ArgumentNullException (nameof (settings));
            CheckSettingsAreValid (Settings);

            allTorrents = new List<TorrentManager> ();
            publicTorrents = new List<TorrentManager> ();
            Torrents = new ReadOnlyCollection<TorrentManager> (publicTorrents);

            DiskManager = new DiskManager (Settings, Factories);

            ConnectionManager = new ConnectionManager (PeerId, Settings, Factories, DiskManager);
            listenManager = new ListenManager (this);
            PortForwarder = Factories.CreatePortForwarder ();

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

            PeerListener = (settings.ListenPort == -1 ? null : Factories.CreatePeerConnectionListener (new IPEndPoint (IPAddress.Any, settings.ListenPort))) ?? new NullPeerListener ();
            listenManager.SetListener (PeerListener);

            DhtListener = (settings.DhtEndPoint == null ? null : Factories.CreateDhtListener (settings.DhtEndPoint)) ?? new NullDhtListener ();
            DhtEngine = settings.DhtEndPoint == null ? new NullDhtEngine () : DhtEngineFactory.Create (Factories);
            DhtEngine.SetListenerAsync (DhtListener).GetAwaiter ().GetResult ();

            DhtEngine.StateChanged += DhtEngineStateChanged;
            DhtEngine.PeersFound += DhtEnginePeersFound;

            RegisterLocalPeerDiscovery (settings.AllowLocalPeerDiscovery ? Factories.CreateLocalPeerDiscovery () : null);
        }

        #endregion


        #region Methods

        public Task<TorrentManager> AddAsync (MagnetLink magnetLink, string saveDirectory)
            => AddAsync (magnetLink, saveDirectory, new TorrentSettings ());

        public Task<TorrentManager> AddAsync (MagnetLink magnetLink, string saveDirectory, TorrentSettings settings)
            => AddAsync (magnetLink, null, saveDirectory, settings);

        public Task<TorrentManager> AddAsync (string metadataPath, string saveDirectory)
            => AddAsync (metadataPath, saveDirectory, new TorrentSettings ());

        public async Task<TorrentManager> AddAsync (string metadataPath, string saveDirectory, TorrentSettings settings)
        {
            var torrent = await Torrent.LoadAsync (metadataPath).ConfigureAwait (false);

            var metadataCachePath = Settings.GetMetadataPath (torrent.InfoHash);
            if (metadataPath != metadataCachePath) {
                Directory.CreateDirectory (Path.GetDirectoryName (metadataCachePath));
                File.Copy (metadataPath, metadataCachePath, true);
            }
            return await AddAsync (null, torrent, saveDirectory, settings);
        }

        public Task<TorrentManager> AddAsync (Torrent torrent, string saveDirectory)
            => AddAsync (torrent, saveDirectory, new TorrentSettings ());

        public async Task<TorrentManager> AddAsync (Torrent torrent, string saveDirectory, TorrentSettings settings)
        {
            await MainLoop.SwitchThread ();

            var editor = new TorrentEditor (new BEncodedDictionary {
                { "info", BEncodedValue.Decode (torrent.InfoMetadata) }
            });
            editor.SetCustom ("name", (BEncodedString) torrent.Name);

            if (torrent.AnnounceUrls.Count > 0) {
                if (torrent.AnnounceUrls.Count == 1 && torrent.AnnounceUrls [0].Count == 1) {
                    editor.Announce = torrent.AnnounceUrls.Single ().Single ();
                } else {
                    foreach (var tier in torrent.AnnounceUrls) {
                        var list = new List<string> ();
                        foreach (var tracker in tier)
                            list.Add (tracker);
                        editor.Announces.Add (list);
                    }
                }
            }

            var metadataCachePath = Settings.GetMetadataPath (torrent.InfoHash);
            Directory.CreateDirectory (Path.GetDirectoryName (metadataCachePath));
            File.WriteAllBytes (metadataCachePath, editor.ToDictionary ().Encode ());

            return await AddAsync (null, torrent, saveDirectory, settings);
        }

        async Task<TorrentManager> AddAsync (MagnetLink magnetLink, Torrent torrent, string saveDirectory, TorrentSettings settings)
        {
            await MainLoop;

            saveDirectory = string.IsNullOrEmpty (saveDirectory) ? Environment.CurrentDirectory : Path.GetFullPath (saveDirectory);
            TorrentManager manager;
            if (magnetLink != null) {
                var metadataSaveFilePath = Settings.GetMetadataPath (magnetLink.InfoHash);
                manager = new TorrentManager (this, magnetLink, saveDirectory, settings);
                if (Settings.AutoSaveLoadMagnetLinkMetadata && Torrent.TryLoad (metadataSaveFilePath, out torrent) && torrent.InfoHash == magnetLink.InfoHash)
                    manager.SetMetadata (torrent);
            } else {
                manager = new TorrentManager (this, torrent, saveDirectory, settings);
            }

            await Register (manager, true);
            await manager.MaybeLoadFastResumeAsync ();

            return manager;
        }

        public async Task<TorrentManager> AddStreamingAsync (MagnetLink magnetLink, string saveDirectory)
            => await MakeStreamingAsync (await AddAsync (magnetLink, saveDirectory));

        public async Task<TorrentManager> AddStreamingAsync (MagnetLink magnetLink, string saveDirectory, TorrentSettings settings)
            => await MakeStreamingAsync (await AddAsync (magnetLink, saveDirectory, settings));

        public async Task<TorrentManager> AddStreamingAsync (string metadataPath, string saveDirectory)
            => await MakeStreamingAsync (await AddAsync (metadataPath, saveDirectory));

        public async Task<TorrentManager> AddStreamingAsync (string metadataPath, string saveDirectory, TorrentSettings settings)
            => await MakeStreamingAsync (await AddAsync (metadataPath, saveDirectory, settings));

        public async Task<TorrentManager> AddStreamingAsync (Torrent torrent, string saveDirectory)
            => await MakeStreamingAsync (await AddAsync (torrent, saveDirectory));

        public async Task<TorrentManager> AddStreamingAsync (Torrent torrent, string saveDirectory, TorrentSettings settings)
            => await MakeStreamingAsync (await AddAsync (torrent, saveDirectory, settings));

        async Task<TorrentManager> MakeStreamingAsync (TorrentManager manager)
        {
            await manager.ChangePickerAsync (Factories.CreateStreamingPieceRequester ());
            return manager;
        }

        public Task<bool> RemoveAsync (MagnetLink magnetLink)
            => RemoveAsync (magnetLink, RemoveMode.CacheDataOnly);

        public Task<bool> RemoveAsync (MagnetLink magnetLink, RemoveMode mode)
        {
            magnetLink = magnetLink ?? throw new ArgumentNullException (nameof (magnetLink));
            return RemoveAsync (magnetLink.InfoHash, mode);
        }

        public Task<bool> RemoveAsync (Torrent torrent)
            => RemoveAsync (torrent, RemoveMode.CacheDataOnly);

        public Task<bool> RemoveAsync (Torrent torrent, RemoveMode mode)
        {
            torrent = torrent ?? throw new ArgumentNullException (nameof (torrent));
            return RemoveAsync (torrent.InfoHash, mode);
        }

        public Task<bool> RemoveAsync (TorrentManager manager)
            => RemoveAsync (manager, RemoveMode.CacheDataOnly);

        public async Task<bool> RemoveAsync (TorrentManager manager, RemoveMode mode)
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

            manager.DownloadLimiters.Remove (downloadLimiters);
            manager.UploadLimiters.Remove (uploadLimiters);
            manager.Dispose ();

            if (mode.HasFlag (RemoveMode.CacheDataOnly)) {
                foreach (var path in new [] { Settings.GetFastResumePath (manager.InfoHash), Settings.GetMetadataPath (manager.InfoHash) })
                    if (File.Exists (path))
                        File.Delete (path);
            }
            if (mode.HasFlag (RemoveMode.DownloadedDataOnly)) {
                foreach (var path in manager.Files.Select (f => f.FullPath))
                    if (File.Exists (path))
                        File.Delete (path);
                // FIXME: Clear the empty directories.
            }
            return true;
        }

        async Task<bool> RemoveAsync (InfoHash infohash, RemoveMode mode)
        {
            await MainLoop;
            var manager = allTorrents.FirstOrDefault (t => t.InfoHash == infohash);
            return manager != null && await RemoveAsync (manager, mode);
        }

        public async Task ChangePieceWriterAsync (IPieceWriter writer)
        {
            writer = writer ?? throw new ArgumentNullException (nameof (writer));

            await MainLoop;
            if (IsRunning)
                throw new InvalidOperationException ("You must stop all active downloads before changing the piece writer used to write data to disk.");
            await DiskManager.SetWriterAsync (writer);
        }

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

            return allTorrents.Exists (m => m.InfoHash.Equals (infoHash));
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

            return Contains (manager.InfoHash);
        }

        public void Dispose ()
        {
            if (Disposed)
                return;

            Disposed = true;
            MainLoop.QueueWait (() => {
                PeerListener.Stop ();
                listenManager.SetListener (null);

                DhtListener.Stop ();
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
            await MainLoop;

            var manager = new TorrentManager (this, magnetLink, "", new TorrentSettings ());
            var metadataCompleted = new TaskCompletionSource<byte[]> ();
            using var registration = token.Register (() => metadataCompleted.TrySetResult (null));
            manager.MetadataReceived += (o, e) => metadataCompleted.TrySetResult (e);

            await Register (manager, isPublic: false);
            await manager.StartAsync (metadataOnly: true);
            var data = await metadataCompleted.Task;
            await manager.StopAsync ();
            await RemoveAsync (manager);

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

        async Task Register (TorrentManager manager, bool isPublic)
        {
            CheckDisposed ();
            Check.Manager (manager);

            await MainLoop;

            if (Contains (manager.InfoHash))
                throw new TorrentException ("A manager for this torrent has already been registered");

            allTorrents.Add (manager);
            if (isPublic)
                publicTorrents.Add (manager);
            ConnectionManager.Add (manager);
            listenManager.Add (manager.InfoHash);

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
                await DhtEngine.StartAsync (await MaybeLoadDhtNodes ());
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

                PeerListener.Start ();
                LocalPeerDiscovery.Start ();
                await DhtEngine.StartAsync (await MaybeLoadDhtNodes ());
                if (Settings.AllowPortForwarding)
                    await PortForwarder.StartAsync (CancellationToken.None);

                if (PeerListener.LocalEndPoint != null)
                    await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Tcp, PeerListener.LocalEndPoint.Port));

                if (DhtListener.LocalEndPoint != null)
                    await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Udp, DhtListener.LocalEndPoint.Port));
            }
        }

        internal async Task StopAsync ()
        {
            CheckDisposed ();
            // If all the torrents are stopped, stop ticking
            IsRunning = allTorrents.Exists (m => m.State != TorrentState.Stopped);
            if (!IsRunning) {
                if (PeerListener.LocalEndPoint != null)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Tcp, PeerListener.LocalEndPoint.Port), CancellationToken.None);

                if (DhtListener.LocalEndPoint != null)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Udp, DhtListener.LocalEndPoint.Port), CancellationToken.None);

                PeerListener.Stop ();
                LocalPeerDiscovery.Stop ();

                if (Settings.AllowPortForwarding)
                    await PortForwarder.StopAsync (CancellationToken.None);

                await MaybeSaveDhtNodes ();
                await DhtEngine.StopAsync ();
            }
        }

        async ReusableTasks.ReusableTask<byte[]> MaybeLoadDhtNodes ()
        {
            if (!Settings.AutoSaveLoadDhtCache)
                return null;

            var savePath = Settings.GetDhtNodeCacheFilePath ();
            return await Task.Run (() => File.Exists (savePath) ? File.ReadAllBytes (savePath) : null);
        }

        async ReusableTasks.ReusableTask MaybeSaveDhtNodes ()
        {
            if (!Settings.AutoSaveLoadDhtCache)
                return;

            var nodes = await DhtEngine.SaveNodesAsync ();
            if (nodes.Length == 0)
                return;

            await Task.Run (() => {
                var savePath = Settings.GetDhtNodeCacheFilePath ();
                var parentDir = Path.GetDirectoryName (savePath);
                Directory.CreateDirectory (parentDir);
                File.WriteAllBytes (savePath, nodes);
            });
        }

        public async Task UpdateSettingsAsync (EngineSettings settings)
        {
            await MainLoop.SwitchThread ();
            CheckSettingsAreValid (settings);

            await MainLoop;

            var oldSettings = Settings;
            Settings = settings;
            await UpdateSettingsAsync (oldSettings, settings);
        }

        static void CheckSettingsAreValid (EngineSettings settings)
        {
            if (string.IsNullOrEmpty (settings.CacheDirectory))
                throw new ArgumentException ("EngineSettings.CacheDirectory cannot be null or empty.", nameof (settings));

            if (File.Exists (settings.CacheDirectory))
                throw new ArgumentException ("EngineSettings.CacheDirectory should be a directory, but a file exists at that path instead. Please delete the file or choose another path", nameof (settings));

            foreach (var directory in new[] { settings.CacheDirectory, settings.MetadataCacheDirectory, settings.FastResumeCacheDirectory }) {
                try {
                    Directory.CreateDirectory (directory);
                } catch (Exception e) {
                    throw new ArgumentException ($"Could not create a directory at the path {directory}. Please check this path has read/write permissions for this user.", nameof (settings), e);
                }
            }
        }

        async Task UpdateSettingsAsync (EngineSettings oldSettings, EngineSettings newSettings)
        {
            await DiskManager.UpdateSettingsAsync (newSettings);
            if (newSettings.DiskCacheBytes != oldSettings.DiskCacheBytes)
                await Task.WhenAll (Torrents.Select (t => DiskManager.FlushAsync (t)));

            ConnectionManager.Settings = newSettings;

            if (oldSettings.UsePartialFiles != newSettings.UsePartialFiles) {
                foreach (var manager in Torrents)
                    await manager.UpdateUsePartialFiles (newSettings.UsePartialFiles);
            }
            if (oldSettings.AllowPortForwarding != newSettings.AllowPortForwarding) {
                if (newSettings.AllowPortForwarding)
                    await PortForwarder.StartAsync (CancellationToken.None);
                else
                    await PortForwarder.StopAsync (removeExistingMappings: true, CancellationToken.None);
            }

            if (oldSettings.DhtEndPoint != newSettings.DhtEndPoint) {
                if (DhtListener.LocalEndPoint != null)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Udp, DhtListener.LocalEndPoint.Port), CancellationToken.None);
                DhtListener.Stop ();

                if (newSettings.DhtEndPoint == null) {
                    DhtListener = new NullDhtListener ();
                    await RegisterDht (new NullDhtEngine ());
                } else {
                    DhtListener = Factories.CreateDhtListener (Settings.DhtEndPoint) ?? new NullDhtListener ();
                    if (IsRunning)
                        DhtListener.Start ();

                    if (oldSettings.DhtEndPoint == null) {
                        var dht = DhtEngineFactory.Create (Factories);
                        await dht.SetListenerAsync (DhtListener);
                        await RegisterDht (dht);

                    } else {
                        await DhtEngine.SetListenerAsync (DhtListener);
                    }
                }

                if (DhtListener.LocalEndPoint != null)
                    await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Udp, DhtListener.LocalEndPoint.Port));
            }

            if (oldSettings.ListenPort != newSettings.ListenPort) {
                if (PeerListener.LocalEndPoint != null)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Tcp, PeerListener.LocalEndPoint.Port), CancellationToken.None);

                PeerListener.Stop ();
                PeerListener = (newSettings.ListenPort == -1 ? null : Factories.CreatePeerConnectionListener (new IPEndPoint (IPAddress.Any, newSettings.ListenPort))) ?? new NullPeerListener ();
                listenManager.SetListener (PeerListener);

                if (IsRunning) {
                    PeerListener.Start ();
                    // The settings could say to listen at port 0, which means 'choose one dynamically'
                    if (PeerListener.LocalEndPoint != null)
                        await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Tcp, PeerListener.LocalEndPoint.Port));
                }
            }

            if (oldSettings.AllowLocalPeerDiscovery != newSettings.AllowLocalPeerDiscovery) {
                RegisterLocalPeerDiscovery (!newSettings.AllowLocalPeerDiscovery ? null : Factories.CreateLocalPeerDiscovery());
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
