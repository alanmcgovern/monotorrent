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
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Dht;
using MonoTorrent.Logging;
using MonoTorrent.PieceWriter;
using MonoTorrent.PortForwarding;

using ReusableTasks;

namespace MonoTorrent.Client
{
    /// <summary>
    /// The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IDisposable
    {
        static ClientEngine ()
        {
            ReusableTaskMethodBuilder.MaximumCacheSize = 2048;
        }

        internal static readonly MainLoop MainLoop = new MainLoop ("Client Engine Loop");
        static readonly Logger Log = Logger.Create (nameof (ClientEngine));

        public static Task<ClientEngine> RestoreStateAsync (string pathToStateFile)
            => RestoreStateAsync (pathToStateFile, Factories.Default);

        public static async Task<ClientEngine> RestoreStateAsync (string pathToStateFile, Factories factories)
        {
            await MainLoop.SwitchThread ();
            return await RestoreStateAsync (File.ReadAllBytes (pathToStateFile), factories);
        }

        public static Task<ClientEngine> RestoreStateAsync (ReadOnlyMemory<byte> buffer)
            => RestoreStateAsync (buffer, Factories.Default);

        public static async Task<ClientEngine> RestoreStateAsync (ReadOnlyMemory<byte> buffer, Factories factories)
        {
            var state = BEncodedValue.Decode<BEncodedDictionary> (buffer.Span);
            var engineSettings = Serializer.DeserializeEngineSettings ((BEncodedDictionary) state["Settings"]);

            var clientEngine = new ClientEngine (engineSettings, factories);
            TorrentManager manager;
            foreach (BEncodedDictionary torrent in (BEncodedList) state[nameof (clientEngine.Torrents)]) {
                var saveDirectory = ((BEncodedString) torrent[nameof (manager.SavePath)]).Text;
                var streaming = bool.Parse (((BEncodedString) torrent["Streaming"]).Text);
                var torrentSettings = Serializer.DeserializeTorrentSettings ((BEncodedDictionary) torrent[nameof (manager.Settings)]);

                if (torrent.ContainsKey (nameof (manager.MetadataPath))) {
                    var metadataPath = (BEncodedString) torrent[nameof (manager.MetadataPath)];
                    if (streaming)
                        manager = await clientEngine.AddStreamingAsync (metadataPath.Text, saveDirectory, torrentSettings);
                    else
                        manager = await clientEngine.AddAsync (metadataPath.Text, saveDirectory, torrentSettings);

                    foreach (BEncodedDictionary file in (BEncodedList) torrent[nameof (manager.Files)]) {
                        TorrentFileInfo torrentFile;
                        torrentFile = (TorrentFileInfo) manager.Files.Single (t => t.Path == ((BEncodedString) file[nameof (torrentFile.Path)]).Text);
                        torrentFile.Priority = (Priority) Enum.Parse (typeof (Priority), file[nameof (torrentFile.Priority)].ToString ()!);
                        torrentFile.UpdatePaths ((
                            newPath: ((BEncodedString) file[nameof (torrentFile.FullPath)]).Text,
                            downloadCompletePath: ((BEncodedString) file[nameof (torrentFile.DownloadCompleteFullPath)]).Text,
                            downloadIncompletePath: ((BEncodedString) file[nameof (torrentFile.DownloadIncompleteFullPath)]).Text
                        ));
                    }
                } else {
                    var magnetLink = MagnetLink.Parse (torrent[nameof (manager.MagnetLink)].ToString ()!);
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
                            { nameof(file.DownloadCompleteFullPath), (BEncodedString) file.DownloadCompleteFullPath },
                            { nameof(file.DownloadIncompleteFullPath), (BEncodedString) file.DownloadIncompleteFullPath },
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

        public event EventHandler<StatsUpdateEventArgs>? StatsUpdate;
        public event EventHandler<CriticalExceptionEventArgs>? CriticalException;

        #endregion


        #region Member Variables

        readonly SemaphoreSlim dhtNodeLocker;

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

        public IDht Dht { get; private set; }

        internal IDhtEngine DhtEngine { get; private set; }

        IDhtListener DhtListener { get; set; }

        public DiskManager DiskManager { get; }

        public bool Disposed { get; private set; }

        internal Factories Factories { get; }


        /// <summary>
        /// A readonly list of the listeners which the engine is using to receive incoming connections from other peers.
        /// This are created by passing <see cref="EngineSettings.ListenEndPoints"/> to the <see cref="Factories.CreatePeerConnectionListener(IPEndPoint)"/> factory method.
        /// </summary>
        public IList<IPeerConnectionListener> PeerListeners { get; private set; } = Array.Empty<IPeerConnectionListener> ();

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

        public long TotalDownloadRate {
            get {
                long total = 0;
                for (int i = 0; i < publicTorrents.Count; i++)
                    total += publicTorrents[i].Monitor.DownloadRate;
                return total;
            }
        }

        public long TotalUploadRate {
            get {
                long total = 0;
                for (int i = 0; i < publicTorrents.Count; i++)
                    total += publicTorrents[i].Monitor.UploadRate;
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
            dhtNodeLocker = new SemaphoreSlim (1, 1);
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

            PeerListeners = Array.AsReadOnly (settings.ListenEndPoints.Values.Select (t => Factories.CreatePeerConnectionListener (t)).ToArray ());
            listenManager.SetListeners (PeerListeners);

            DhtListener = (settings.DhtEndPoint == null ? null : Factories.CreateDhtListener (settings.DhtEndPoint)) ?? new NullDhtListener ();
            DhtEngine = (settings.DhtEndPoint == null ? null : Factories.CreateDht ()) ?? new NullDhtEngine ();
            Dht = new DhtEngineWrapper (DhtEngine);
            DhtEngine.SetListenerAsync (DhtListener).GetAwaiter ().GetResult ();

            DhtEngine.StateChanged += DhtEngineStateChanged;
            DhtEngine.PeersFound += DhtEnginePeersFound;
            LocalPeerDiscovery = new NullLocalPeerDiscovery ();

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

            var metadataCachePath = Settings.GetMetadataPath (torrent.InfoHashes);
            if (metadataPath != metadataCachePath) {
                Directory.CreateDirectory (Path.GetDirectoryName (metadataCachePath)!);
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
                { "info", BEncodedValue.Decode (torrent.InfoMetadata.Span) }
            });
            editor.SetCustom ("name", (BEncodedString) torrent.Name);

            if (torrent.AnnounceUrls.Count > 0) {
                if (torrent.AnnounceUrls.Count == 1 && torrent.AnnounceUrls[0].Count == 1) {
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

            var metadataCachePath = Settings.GetMetadataPath (torrent.InfoHashes);
            Directory.CreateDirectory (Path.GetDirectoryName (metadataCachePath)!);
            File.WriteAllBytes (metadataCachePath, editor.ToDictionary ().Encode ());

            return await AddAsync (null, torrent, saveDirectory, settings);
        }

        async Task<TorrentManager> AddAsync (MagnetLink? magnetLink, Torrent? torrent, string saveDirectory, TorrentSettings settings)
        {
            await MainLoop;

            saveDirectory = string.IsNullOrEmpty (saveDirectory) ? Environment.CurrentDirectory : Path.GetFullPath (saveDirectory);
            TorrentManager manager;
            if (magnetLink != null) {
                var metadataSaveFilePath = Settings.GetMetadataPath (magnetLink.InfoHashes);
                manager = new TorrentManager (this, magnetLink, saveDirectory, settings);
                if (Settings.AutoSaveLoadMagnetLinkMetadata && Torrent.TryLoad (metadataSaveFilePath, out torrent) && torrent.InfoHashes == magnetLink.InfoHashes)
                    manager.SetMetadata (torrent);
            } else if (torrent != null) {
                manager = new TorrentManager (this, torrent, saveDirectory, settings);
            } else {
                throw new InvalidOperationException ($"You must pass a non-null {nameof (magnetLink)} or {nameof (torrent)}");
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
            return RemoveAsync (magnetLink.InfoHashes, mode);
        }

        public Task<bool> RemoveAsync (Torrent torrent)
            => RemoveAsync (torrent, RemoveMode.CacheDataOnly);

        public Task<bool> RemoveAsync (Torrent torrent, RemoveMode mode)
        {
            torrent = torrent ?? throw new ArgumentNullException (nameof (torrent));
            return RemoveAsync (torrent.InfoHashes, mode);
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
            listenManager.Remove (manager.InfoHashes);

            manager.DownloadLimiters.Remove (downloadLimiters);
            manager.UploadLimiters.Remove (uploadLimiters);
            manager.Dispose ();

            if (mode.HasFlag (RemoveMode.CacheDataOnly)) {
                foreach (var path in new[] { Settings.GetFastResumePath (manager.InfoHashes), Settings.GetMetadataPath (manager.InfoHashes) })
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

        async Task<bool> RemoveAsync (InfoHashes infoHashes, RemoveMode mode)
        {
            await MainLoop;
            var manager = allTorrents.FirstOrDefault (t => t.InfoHashes == infoHashes);
            return manager != null && await RemoveAsync (manager, mode);
        }

        async Task ChangePieceWriterAsync (IPieceWriter writer)
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

        public bool Contains (InfoHashes infoHashes)
        {
            CheckDisposed ();
            if (infoHashes == null)
                return false;

            return allTorrents.Exists (m => m.InfoHashes == infoHashes);
        }

        public bool Contains (Torrent torrent)
        {
            CheckDisposed ();
            if (torrent == null)
                return false;

            return Contains (torrent.InfoHashes);
        }

        public bool Contains (TorrentManager manager)
        {
            CheckDisposed ();
            if (manager == null)
                return false;

            return Contains (manager.InfoHashes);
        }

        public void Dispose ()
        {
            if (Disposed)
                return;

            Disposed = true;
            MainLoop.QueueWait (() => {
                foreach (var listener in PeerListeners)
                    listener.Stop ();
                listenManager.SetListeners (Array.Empty<IPeerConnectionListener> ());

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
        public async Task<ReadOnlyMemory<byte>> DownloadMetadataAsync (MagnetLink magnetLink, CancellationToken token)
        {
            await MainLoop;

            var manager = new TorrentManager (this, magnetLink, "", new TorrentSettings ());
            var metadataCompleted = new TaskCompletionSource<ReadOnlyMemory<byte>> ();
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

        async void HandleLocalPeerFound (object? sender, LocalPeerFoundEventArgs args)
        {
            try {
                await MainLoop;

                TorrentManager? manager = allTorrents.FirstOrDefault (t => t.InfoHashes.Contains (args.InfoHash));
                // There's no TorrentManager in the engine
                if (manager == null)
                    return;

                // The torrent is marked as private, so we can't add random people
                if (manager.HasMetadata && manager.Torrent!.IsPrivate) {
                    manager.RaisePeersFound (new LocalPeersAdded (manager, 0, 0));
                } else {
                    // Add new peer to matched Torrent
                    var peer = new PeerInfo (args.Uri);
                    int peersAdded = manager.AddPeers (new[] { peer }, prioritise: false, fromTracker: false);
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

            if (Contains (manager.InfoHashes))
                throw new TorrentException ("A manager for this torrent has already been registered");

            allTorrents.Add (manager);
            if (isPublic)
                publicTorrents.Add (manager);
            ConnectionManager.Add (manager);
            listenManager.Add (manager.InfoHashes);

            manager.DownloadLimiters.Add (downloadLimiters);
            manager.UploadLimiters.Add (uploadLimiters);
            if (DhtEngine != null && manager.Torrent?.Nodes != null && DhtEngine.State != DhtState.Ready) {
                try {
                    DhtEngine.Add (manager.Torrent.Nodes.OfType<BEncodedString> ().Select (t => t.AsMemory ()));
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
            Dht = new DhtEngineWrapper (DhtEngine);

            DhtEngine.StateChanged += DhtEngineStateChanged;
            DhtEngine.PeersFound += DhtEnginePeersFound;
            if (IsRunning)
                await DhtEngine.StartAsync (await MaybeLoadDhtNodes ());
        }

        void RegisterLocalPeerDiscovery (ILocalPeerDiscovery? localPeerDiscovery)
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

        async void DhtEnginePeersFound (object? o, PeersFoundEventArgs e)
        {
            await MainLoop;

            TorrentManager? manager = allTorrents.FirstOrDefault (t => t.InfoHashes.Contains (e.InfoHash));
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

        async void DhtEngineStateChanged (object? o, EventArgs e)
        {
            if (DhtEngine.State != DhtState.Ready)
                return;

            await MainLoop;
            foreach (TorrentManager manager in allTorrents) {
                if (!manager.CanUseDht)
                    continue;

                // IPV6: Also report to an ipv6 DHT node
                if (manager.InfoHashes.V1 != null) {
                    DhtEngine.Announce (manager.InfoHashes.V1, GetOverrideOrActualListenPort ("ipv4") ?? -1);
                    DhtEngine.GetPeers (manager.InfoHashes.V1);
                }
                if (manager.InfoHashes.V2 != null) {
                    DhtEngine.Announce (manager.InfoHashes.V2.Truncate (), GetOverrideOrActualListenPort ("ipv4") ?? -1);
                    DhtEngine.GetPeers (manager.InfoHashes.V2.Truncate ());
                }
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
                downloadLimiter.UpdateChunks (Settings.MaximumDownloadRate);
                uploadLimiter.UpdateChunks (Settings.MaximumUploadRate);
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

                if (Settings.AllowPortForwarding)
                    await PortForwarder.StartAsync (CancellationToken.None);

                LocalPeerDiscovery.Start ();
                await DhtEngine.StartAsync (await MaybeLoadDhtNodes ());

                await StartAndPortMapPeerListeners ();

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
                await UnmapAndStopPeerListeners ();

                if (DhtListener.LocalEndPoint != null)
                    await PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Udp, DhtListener.LocalEndPoint.Port), CancellationToken.None);

                LocalPeerDiscovery.Stop ();

                if (Settings.AllowPortForwarding)
                    await PortForwarder.StopAsync (CancellationToken.None);

                await MaybeSaveDhtNodes ();
                await DhtEngine.StopAsync ();
            }
        }

        async ReusableTask StartAndPortMapPeerListeners ()
        {
            foreach (var v in PeerListeners)
                v.Start ();

            // The settings could say to listen at port 0, which means 'choose one dynamically'
            var maps = PeerListeners
                .Select (t => t.LocalEndPoint!)
                .Where (t => t != null)
                .Select (endpoint => PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Tcp, endpoint.Port)))
                .ToArray ();
            await Task.WhenAll (maps);
        }

        async ReusableTask UnmapAndStopPeerListeners()
        {
            var unmaps = PeerListeners
                    .Select (t => t.LocalEndPoint!)
                    .Where (t => t != null)
                    .Select (endpoint => PortForwarder.UnregisterMappingAsync (new Mapping (Protocol.Tcp, endpoint.Port), CancellationToken.None))
                    .ToArray ();
            await Task.WhenAll (unmaps);

            foreach (var listener in PeerListeners)
                listener.Stop ();
        }

        async ReusableTask<ReadOnlyMemory<byte>> MaybeLoadDhtNodes ()
        {
            if (!Settings.AutoSaveLoadDhtCache)
                return ReadOnlyMemory<byte>.Empty;

            var savePath = Settings.GetDhtNodeCacheFilePath ();
            return await Task.Run (() => File.Exists (savePath) ? File.ReadAllBytes (savePath) : ReadOnlyMemory<byte>.Empty);
        }

        async ReusableTask MaybeSaveDhtNodes ()
        {
            if (!Settings.AutoSaveLoadDhtCache)
                return;

            var nodes = await DhtEngine.SaveNodesAsync ();
            if (nodes.Length == 0)
                return;

            // Perform this action on a threadpool thread.
            await MainLoop.SwitchThread ();

            // Ensure only 1 thread at a time tries to save DhtNodes.
            // Users can call StartAsync/StopAsync many times on
            // TorrentManagers and the file write could happen
            // concurrently.
            using (await dhtNodeLocker.EnterAsync ().ConfigureAwait (false)) {
                var savePath = Settings.GetDhtNodeCacheFilePath ();
                var parentDir = Path.GetDirectoryName (savePath);
                if (!(parentDir is null))
                    Directory.CreateDirectory (parentDir);
                File.WriteAllBytes (savePath, nodes.ToArray ());
            }
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
                    DhtListener = (Settings.DhtEndPoint is null ? null : Factories.CreateDhtListener (Settings.DhtEndPoint)) ?? new NullDhtListener ();
                    if (IsRunning)
                        DhtListener.Start ();

                    if (oldSettings.DhtEndPoint == null) {
                        var dht = Factories.CreateDht ();
                        await dht.SetListenerAsync (DhtListener);
                        await RegisterDht (dht);

                    } else {
                        await DhtEngine.SetListenerAsync (DhtListener);
                    }
                }

                if (DhtListener.LocalEndPoint != null)
                    await PortForwarder.RegisterMappingAsync (new Mapping (Protocol.Udp, DhtListener.LocalEndPoint.Port));
            }

            if (!oldSettings.ListenEndPoints.SequenceEqual (newSettings.ListenEndPoints)) {
                await UnmapAndStopPeerListeners ();

                PeerListeners = Array.AsReadOnly (newSettings.ListenEndPoints.Values.Select (t => Factories.CreatePeerConnectionListener (t)).ToArray ());
                listenManager.SetListeners (PeerListeners);

                if (IsRunning)
                    await StartAndPortMapPeerListeners ();
            }

            if (oldSettings.AllowLocalPeerDiscovery != newSettings.AllowLocalPeerDiscovery) {
                RegisterLocalPeerDiscovery (!newSettings.AllowLocalPeerDiscovery ? null : Factories.CreateLocalPeerDiscovery ());
            }
        }

        static BEncodedString GeneratePeerId ()
        {
            var sb = new StringBuilder (20);
            sb.Append ("-");
            sb.Append (GitInfoHelper.ClientVersion);
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

        internal int? GetOverrideOrActualListenPort (string scheme)
        {
            // If the override is set to non-zero, use it. Otherwise use the actual port.
            if (Settings.ReportedListenEndPoints.TryGetValue (scheme, out var reportedEndPoint) && reportedEndPoint.Port != 0)
                return reportedEndPoint.Port;

            // Try to get the actual port first.
            foreach (var endPoint in PeerListeners.Select (t => t.LocalEndPoint!).Where (t => t != null)) {
                if (scheme == "ipv4" && endPoint.AddressFamily == AddressFamily.InterNetwork)
                    return endPoint.Port;
                if (scheme == "ipv6" && endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                    return endPoint.Port;
            }

            // If there was a listener but it hadn't successfully bound to a port, return the preferred port port... if it's non-zero.
            foreach (var endPoint in PeerListeners.Select (t => t.PreferredLocalEndPoint!).Where (t => t != null)) {
                if (scheme == "ipv4" && endPoint.Port != 0 && endPoint.AddressFamily == AddressFamily.InterNetwork)
                    return endPoint.Port;
                if (scheme == "ipv6" && endPoint.Port != 0 && endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                    return endPoint.Port;
            }

            // If we get here there are either no listeners, or none were bound to a local port, or the preferred port is set to '0' (which means
            // we don't know it's port yet because the listener isn't running. This *should* never happen as we should only be running announces
            // while the engine is active, which means the listener should still be running.)
            return null;
        }
    }
    #endregion
}
