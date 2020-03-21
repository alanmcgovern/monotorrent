//
// TorrentManager.cs
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Modes;
using MonoTorrent.Client.PiecePicking;
using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client
{
    public class TorrentManager : IDisposable, IEquatable<TorrentManager>
    {
        #region Events

        internal event EventHandler<(Torrent torrent, byte[] dict)> MetadataReceived;

        /// <summary>
        /// This asynchronous event is raised whenever a new incoming, or outgoing, connection
        /// has successfully completed the handshake process and has been fully established.
        /// </summary>
        public event EventHandler<PeerConnectedEventArgs> PeerConnected;

        /// <summary>
        /// This asynchronous event is raised whenever an established connection has been
        /// closed.
        /// </summary>
        public event EventHandler<PeerDisconnectedEventArgs> PeerDisconnected;

        /// <summary>
        /// This asynchronous event is raised when an outgoing connection to a peer
        /// could not be established.
        /// </summary>
        public event EventHandler<ConnectionAttemptFailedEventArgs> ConnectionAttemptFailed;

        /// <summary>
        /// This event is raised synchronously and is only used supposed to be used by tests.
        /// </summary>
        internal event Action<Mode, Mode> ModeChanged;

        /// <summary>
        /// Raised whenever new peers are discovered and added. The object will be of type
        /// <see cref="TrackerPeersAdded"/>, <see cref="PeerExchangePeersAdded"/>, <see cref="LocalPeersAdded"/>
        /// or <see cref="DhtPeersAdded"/> depending on the source of the new peers.
        /// </summary>
        public event EventHandler<PeersAddedEventArgs> PeersFound;

        /// <summary>
        /// This asynchronous event is raised whenever a piece is hashed, either as part of
        /// regular downloading, or as part of a <see cref="HashCheckAsync(bool)"/>.
        /// </summary>
        public event EventHandler<PieceHashedEventArgs> PieceHashed;

        /// <summary>
        /// This asynchronous event is raised whenever the TorrentManager changes state.
        /// </summary>
        public event EventHandler<TorrentStateChangedEventArgs> TorrentStateChanged;

        internal event EventHandler<PeerAddedEventArgs> OnPeerFound;

        #endregion


        #region Member Variables

        bool disposed;
        internal Queue<HaveMessage> finishedPieces;     // The list of pieces which we should send "have" messages for
        internal bool isInEndGame;       // Set true when the torrent enters end game processing
        Mode mode;
        readonly string torrentSave;             // The path where the .torrent data will be saved when in metadata mode
        internal IUnchoker chokeUnchoker; // Used to choke and unchoke peers
        internal DateTime lastCalledInactivePeerManager = DateTime.Now;
        #endregion Member Variables


        #region Properties

        public BitField Bitfield { get; internal set; }

        public bool CanUseDht => Settings.AllowDht && (Torrent == null || !Torrent.IsPrivate);

        public bool CanUseLocalPeerDiscovery => ClientEngine.SupportsLocalPeerDiscovery && (Torrent == null || !Torrent.IsPrivate);

        /// <summary>
        /// Returns true only when all files have been fully downloaded. If some files are marked as 'DoNotDownload' then the
        /// torrent will not be considered to be Complete until they are downloaded.
        /// </summary>
        public bool Complete => Bitfield.AllTrue;

        RateLimiter DownloadLimiter { get; set; }

        internal RateLimiterGroup DownloadLimiters { get; private set; }

        public ClientEngine Engine { get; internal set; }

        public Error Error { get; private set; }

        internal Mode Mode {
            get => mode;
            set {
                Mode oldMode = mode;
                mode = value;
                ModeChanged?.Invoke (oldMode, mode);
                if (oldMode != null)
                    RaiseTorrentStateChanged (new TorrentStateChangedEventArgs (this, oldMode.State, mode.State));
                oldMode?.Dispose ();
                mode.Tick (0);
            }
        }

        internal void RaiseMetadataReceived (Torrent torrent, BEncodedDictionary dict)
        {
            MetadataReceived?.Invoke (this, (torrent, dict.Encode ()));
        }

        /// <summary>
        /// If <see cref="TorrentFile.Priority"/> is set to <see cref="Priority.DoNotDownload"/> then the pieces
        /// associated with that <see cref="TorrentFile"/> will not be hash checked. An IgnoringPicker is used
        /// to ensure pieces which have not been hash checked are never downloaded.
        /// </summary>
        internal BitField UnhashedPieces { get; set; }

        public bool HashChecked { get; internal set; }

        public int HashFails { get; internal set; }

        public bool HasMetadata => Torrent != null;

        /// <summary>
        /// True if this torrent has activated special processing for the final few pieces
        /// </summary>
        public bool IsInEndGame => State == TorrentState.Downloading && isInEndGame;

        public ConnectionMonitor Monitor { get; private set; }

        /// <summary>
        /// The number of peers that this torrent instance is connected to
        /// </summary>
        public int OpenConnections => Peers.ConnectedPeers.Count;

        /// <summary>
        /// The time the last announce to the DHT occurred
        /// </summary>
        public DateTime LastDhtAnnounce { get; private set; }

        /// <summary>
        /// Internal timer used to trigger Dht announces every <see cref="MonoTorrent.Dht.DhtEngine.AnnounceInternal"/> seconds.
        /// </summary>
        internal ValueStopwatch LastDhtAnnounceTimer;

        /// <summary>
        /// The time the last announce using Local Peer Discovery occurred
        /// </summary>
        public DateTime LastLocalPeerAnnounce { get; private set; }

        /// <summary>
        /// Internal timer used to trigger Local PeerDiscovery announces every <see cref="LocalPeerDiscovery.AnnounceInternal"/> seconds.
        /// </summary>
        internal ValueStopwatch LastLocalPeerAnnounceTimer;

        internal BitField PartialProgressSelector { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public PeerManager Peers { get; private set; }


        /// <summary>
        /// The piecemanager for this TorrentManager
        /// </summary>
        public PieceManager PieceManager { get; private set; }


        /// <summary>
        /// The inactive peer manager for this TorrentManager
        /// </summary>
        internal InactivePeerManager InactivePeerManager { get; private set; }

        /// <summary>
        /// The download progress in percent (0 -> 100.0) for the files whose priority
        /// is not set to <see cref="Priority.DoNotDownload"/>. If every file is marked
        /// as <see cref="Priority.DoNotDownload"/> then this returns 0. If no file is
        /// marked as 'DoNotDownload' then this returns the same value as <see cref="Progress"/>.
        /// </summary>
        public double PartialProgress {
            get {
                if (!HasMetadata)
                    return Progress;

                if (PartialProgressSelector.TrueCount == 0)
                    return 0;

                // This is an optimisation so we can fastpath the Bitfield operations when
                // all files are marked as downloadable.
                if (PartialProgressSelector.TrueCount == Bitfield.Length)
                    return Progress;

                int totalTrue = Bitfield.CountTrue (PartialProgressSelector);
                return (totalTrue * 100.0) / PartialProgressSelector.TrueCount;
            }
        }

        /// <summary>
        /// The download progress in percent (0 -> 100.0). This includes all files, even
        /// if they are marked as <see cref="Priority.DoNotDownload"/>. This will return
        /// '100.0' when all files in the torrent have been downloaded.
        /// </summary>
        public double Progress => Bitfield.PercentComplete;

        /// <summary>
        /// The directory to download the files to
        /// </summary>
        public string SavePath { get; internal set; }

        /// <summary>
        /// The settings for with this TorrentManager
        /// </summary>
        public TorrentSettings Settings { get; }

        /// <summary>
        /// The current state of the TorrentManager
        /// </summary>
        public TorrentState State => mode.State;

        /// <summary>
        /// The time the torrent manager was started at
        /// </summary>
        public DateTime StartTime { get; private set; }


        /// <summary>
        /// The tracker connection associated with this TorrentManager
        /// </summary>
        public ITrackerManager TrackerManager { get; private set; }

        /// <summary>
        /// The Torrent contained within this TorrentManager
        /// </summary>
        public Torrent Torrent { get; internal set; }

        /// <summary>
        /// The number of peers that we are currently uploading to
        /// </summary>
        public int UploadingTo { get; internal set; }

        RateLimiter UploadLimiter { get; set; }

        internal RateLimiterGroup UploadLimiters { get; private set; }

        public bool IsInitialSeeding => Mode is InitialSeedingMode;

        public InfoHash InfoHash { get; }

        #endregion

        #region Constructors
        internal TorrentManager (MagnetLink magnetLink)
            : this (magnetLink, "", new TorrentSettings (), "")
        {

        }

        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        public TorrentManager (Torrent torrent, string savePath)
            : this (torrent, savePath, new TorrentSettings ())
        {

        }

        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        /// <param name="settings">The settings to use for controlling connections</param>
        public TorrentManager (Torrent torrent, string savePath, TorrentSettings settings)
            : this (torrent, savePath, settings, torrent.Files.Length == 1 ? "" : torrent.Name)
        {

        }

        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        /// <param name="settings">The settings to use for controlling connections</param>
        /// <param name="baseDirectory">In the case of a multi-file torrent, the name of the base directory containing the files. Defaults to Torrent.Name</param>
        public TorrentManager (Torrent torrent, string savePath, TorrentSettings settings, string baseDirectory)
        {
            Check.Torrent (torrent);
            Check.SavePath (savePath);
            Check.Settings (settings);
            Check.BaseDirectory (baseDirectory);

            Torrent = torrent;
            InfoHash = torrent.InfoHash;
            Settings = settings;

            Initialise (savePath, baseDirectory, torrent.AnnounceUrls);
        }


        public TorrentManager (InfoHash infoHash, string savePath, TorrentSettings settings, string torrentSave, IList<RawTrackerTier> announces)
        {
            Check.InfoHash (infoHash);
            Check.SavePath (savePath);
            Check.Settings (settings);
            Check.TorrentSave (torrentSave);
            Check.Announces (announces);

            InfoHash = infoHash;
            Settings = settings;
            this.torrentSave = torrentSave;

            Initialise (savePath, "", announces);
        }

        public TorrentManager (MagnetLink magnetLink, string savePath, TorrentSettings settings, string torrentSave)
        {
            Check.MagnetLink (magnetLink);
            Check.InfoHash (magnetLink.InfoHash);
            Check.SavePath (savePath);
            Check.Settings (settings);
            Check.TorrentSave (torrentSave);

            InfoHash = magnetLink.InfoHash;
            Settings = settings;
            this.torrentSave = torrentSave;
            IList<RawTrackerTier> announces = new RawTrackerTiers ();
            if (magnetLink.AnnounceUrls != null)
                announces.Add (magnetLink.AnnounceUrls);

            if (Torrent.TryLoad (torrentSave, out Torrent torrent) && torrent.InfoHash == magnetLink.InfoHash)
                Torrent = torrent;

            Initialise (savePath, "", announces);
        }

        void Initialise (string savePath, string baseDirectory, IList<RawTrackerTier> announces)
        {
            Bitfield = new BitField (HasMetadata ? Torrent.Pieces.Count : 1);
            PartialProgressSelector = new BitField (HasMetadata ? Torrent.Pieces.Count : 1);
            UnhashedPieces = new BitField (HasMetadata ? Torrent.Pieces.Count : 1).SetAll (true);
            SavePath = Path.Combine (savePath, baseDirectory);
            finishedPieces = new Queue<HaveMessage> ();
            Monitor = new ConnectionMonitor ();
            InactivePeerManager = new InactivePeerManager (this);
            Peers = new PeerManager ();
            PieceManager = new PieceManager (this);
            SetTrackerManager (new TrackerManager (new TrackerRequestFactory (this), announces));

            Mode = new StoppedMode (this, null, null, null);
            CreateRateLimiters ();

            ChangePicker (CreateStandardPicker ());

            if (HasMetadata) {
                foreach (TorrentFile file in Torrent.Files)
                    file.FullPath = Path.Combine (SavePath, file.Path);
            }
        }

        void CreateRateLimiters ()
        {
            DownloadLimiter = new RateLimiter ();
            DownloadLimiters = new RateLimiterGroup {
                new PauseLimiter(this),
                DownloadLimiter
            };

            UploadLimiter = new RateLimiter ();
            UploadLimiters = new RateLimiterGroup {
                new PauseLimiter(this),
                UploadLimiter
            };
        }

        #endregion


        #region Public Methods

        internal void ChangePicker (PiecePicker picker)
        {
            Check.Picker (picker);
            IEnumerable<Piece> pieces = PieceManager.Picker?.ExportActiveRequests () ?? new List<Piece> ();
            PieceManager.ChangePicker (new IgnoringPicker (UnhashedPieces, picker), Bitfield);
            if (Torrent != null)
                PieceManager.Picker.Initialise (Bitfield, Torrent, pieces);
        }

        /// <summary>
        /// Changes the active piece picker. This can be called when the manager is running, or when it is stopped.
        /// </summary>
        /// <param name="picker">The new picker to use.</param>
        /// <returns></returns>
        public async Task ChangePickerAsync (PiecePicker picker)
        {
            await ClientEngine.MainLoop;
            ChangePicker (picker);
        }

        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
        }


        /// <summary>
        /// Overrridden. Returns the name of the torrent.
        /// </summary>
        /// <returns></returns>
        public override string ToString ()
        {
            return Torrent == null ? "<Metadata Mode>" : Torrent.Name;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals (object obj)
        {
            return (!(obj is TorrentManager m)) ? false : Equals (m);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals (TorrentManager other)
        {
            return (other == null) ? false : InfoHash == other.InfoHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode ()
        {
            return InfoHash.GetHashCode ();
        }

        public async Task<List<PeerId>> GetPeersAsync ()
        {
            await ClientEngine.MainLoop;
            return new List<PeerId> (Peers.ConnectedPeers);
        }

        /// <summary>
        /// Performs a full hash check, ignoring any previously loaded Fast Resume data or previous hash checks.
        /// </summary>
        /// <param name="autoStart">True if a the TorrentManager should be started as soon as the hashcheck completes.</param>
        public Task HashCheckAsync (bool autoStart)
        {
            return HashCheckAsync (autoStart, true);
        }

        internal async Task HashCheckAsync (bool autoStart, bool setStoppedModeWhenDone)
        {
            if (!HasMetadata)
                throw new TorrentException ("A hashcheck cannot be performed if the TorrentManager was created with a Magnet link and the metadata has not been downloaded.");

            await ClientEngine.MainLoop;
            if (!Mode.CanHashCheck)
                throw new TorrentException (
                    $"A hashcheck can only be performed when the manager is stopped. State is: {State}");

            CheckRegisteredAndDisposed ();
            StartTime = DateTime.Now;

            // An IgnoringPicker is created to ensure pieces which *have not* been hash checked
            // are not requested from other peers. The intention is that files marked as DoNotDownload
            // will not be hashed, or downloaded.
            UnhashedPieces.SetAll (true);

            var hashingMode = new HashingMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
            Mode = hashingMode;

            try {
                await hashingMode.WaitForHashingToComplete ();
                hashingMode.Token.ThrowIfCancellationRequested ();
            } catch (OperationCanceledException) {
                return;
            } catch (Exception ex) {
                TrySetError (Reason.ReadFailure, ex);
                return;
            }

            HashChecked = true;
            if (autoStart) {
                await StartAsync ();
            } else if (setStoppedModeWhenDone) {
                Mode = new StoppedMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
            }
        }

        public async Task MoveFileAsync (TorrentFile file, string path)
        {
            Check.File (file);
            Check.PathNotEmpty (path);
            CheckRegisteredAndDisposed ();
            CheckMetadata ();

            if (State != TorrentState.Stopped)
                throw new TorrentException ("Cannot move files when the torrent is active");

            try {
                await Engine.DiskManager.MoveFileAsync (file, path);
            } catch (Exception ex) {
                TrySetError (Reason.WriteFailure, ex);
            }
        }

        public async Task MoveFilesAsync (string newRoot, bool overWriteExisting)
        {
            CheckRegisteredAndDisposed ();
            CheckMetadata ();

            if (State != TorrentState.Stopped)
                throw new TorrentException ("Cannot move files when the torrent is active");

            try {
                await Engine.DiskManager.MoveFilesAsync (Torrent, newRoot, overWriteExisting);
                SavePath = newRoot;
            } catch (Exception ex) {
                TrySetError (Reason.WriteFailure, ex);
            }
        }

        /// <summary>
        /// Pauses the TorrentManager
        /// </summary>
        public async Task PauseAsync ()
        {
            await ClientEngine.MainLoop;
            CheckRegisteredAndDisposed ();

            if (Mode is HashingMode hashing) {
                hashing.Pause ();
            } else if (Mode is DownloadMode) {
                Mode = new PausedMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
            }
        }

        /// <summary>
        /// Starts the TorrentManager
        /// </summary>
        public async Task StartAsync ()
            => await StartAsync (false);

        internal async Task StartAsync (bool metadataOnly)
        {
            await ClientEngine.MainLoop;

            if (Mode is StoppingMode)
                throw new TorrentException ("The manager cannot be restarted while it is in the Stopping state.");
            if (Mode is StartingMode)
                throw new TorrentException ("The manager cannot be started a second time while it is already in the Starting state.");

            CheckRegisteredAndDisposed ();

            await Engine.StartAsync ();
            // If the torrent was "paused", then just update the state to Downloading and forcefully
            // make sure the peers begin sending/receiving again
            if (State == TorrentState.Paused) {
                Mode = new DownloadMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
            } else if (Mode is HashingMode hashing && !HashChecked) {
                if (State == TorrentState.HashingPaused)
                    hashing.Resume ();
            } else if (!HasMetadata) {
                StartTime = DateTime.Now;
                Mode = new MetadataMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings, torrentSave, metadataOnly);
            } else {
                StartTime = DateTime.Now;
                var startingMode = new StartingMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
                Mode = startingMode;
                _ = startingMode.WaitForStartingToComplete ();
            }
        }

        public async Task LocalPeerAnnounceAsync ()
        {
            await ClientEngine.MainLoop;

            if (CanUseLocalPeerDiscovery && (!LastLocalPeerAnnounceTimer.IsRunning || LastLocalPeerAnnounceTimer.Elapsed > LocalPeerDiscovery.MinimumAnnounceInternal)) {
                LastLocalPeerAnnounce = DateTime.Now;
                LastLocalPeerAnnounceTimer.Restart ();
                await Engine?.LocalPeerDiscovery.Announce (InfoHash);
            }
        }

        /// <summary>
        /// Perform an announce using the <see cref="ClientEngine.DhtEngine"/> to retrieve more peers. The
        /// returned task completes as soon as the Dht announce begins.
        /// </summary>
        /// <returns></returns>
        public async Task DhtAnnounceAsync ()
        {
            await ClientEngine.MainLoop;
            DhtAnnounce ();
        }

        internal void DhtAnnounce ()
        {
            if (CanUseDht && (!LastDhtAnnounceTimer.IsRunning || LastDhtAnnounceTimer.Elapsed > Dht.DhtEngine.MinimumAnnounceInterval)) {
                LastDhtAnnounce = DateTime.UtcNow;
                LastDhtAnnounceTimer.Restart ();
                Engine?.DhtEngine.GetPeers (InfoHash);
            }
        }

        /// <summary>
        /// Stops the TorrentManager. The returned task completes as soon as the manager has fully stopped.
        /// </summary>
        public Task StopAsync ()
        {
            return StopAsync (Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Stops the TorrentManager. The returned task completes as soon as the manager has fully stopped. The final
        /// tracker announce will be limited to the maximum of either 2 seconds or <paramref name="timeout"/> seconds.
        /// </summary>
        public async Task StopAsync (TimeSpan timeout)
        {
            await ClientEngine.MainLoop;

            if (Mode is StoppingMode)
                throw new TorrentException ("The manager cannot be stopped while it is already in the Stopping state.");

            if (State == TorrentState.Error) {
                Error = null;
                Mode = new StoppedMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
                await Engine.StopAsync ();
            } else if (State != TorrentState.Stopped) {
                var stoppingMode = new StoppingMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
                Mode = stoppingMode;
                await stoppingMode.WaitForStoppingToComplete (timeout);

                stoppingMode.Token.ThrowIfCancellationRequested ();
                Mode = new StoppedMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
                await Engine.StopAsync ();
            }
        }

        #endregion


        #region Internal Methods

        public async Task<bool> AddPeerAsync (Peer peer)
        {
            await ClientEngine.MainLoop;
            return AddPeer (peer, false, false);
        }

        internal bool AddPeer (Peer peer, bool fromTrackers, bool prioritise)
        {
            Check.Peer (peer);
            if (HasMetadata && Torrent.IsPrivate && !fromTrackers)
                throw new InvalidOperationException ("You cannot add external peers to a private torrent");

            if (Peers.Contains (peer))
                return false;

            // Ignore peers in the inactive list
            if (InactivePeerManager.InactivePeerList.Contains (peer.ConnectionUri))
                return false;

            if (Peers.TotalPeers < Settings.MaximumPeerDetails) {
                if (prioritise)
                    Peers.AvailablePeers.Insert (0, peer);
                else
                    Peers.AvailablePeers.Add (peer);
            } else {
                bool successful = false;
                for (int i = 0; i < Peers.AvailablePeers.Count; i++) {
                    if (Peers.AvailablePeers[i].MaybeStale) {
                        Peers.AvailablePeers[i] = peer;
                        successful = true;
                        break;
                    }
                }
                if (!successful)
                    return false;
            }
            OnPeerFound?.Invoke (this, new PeerAddedEventArgs (this, peer));
            // When we successfully add a peer we try to connect to the next available peer
            return true;
        }

        public async Task<int> AddPeersAsync (IEnumerable<Peer> peers)
        {
            await ClientEngine.MainLoop;
            return AddPeers (peers, false);
        }

        int AddPeers (IEnumerable<Peer> peers, bool fromTrackers)
        {
            Check.Peers (peers);
            if (HasMetadata && Torrent.IsPrivate && !fromTrackers)
                throw new InvalidOperationException ("You cannot add external peers to a private torrent");

            int count = 0;
            foreach (Peer p in peers)
                count += AddPeer (p, fromTrackers, prioritise: false) ? 1 : 0;
            return count;
        }

        internal void RaisePeerConnected (PeerConnectedEventArgs args)
        {
            PeerConnected?.InvokeAsync (this, args);
        }

        internal void RaisePeerDisconnected (PeerDisconnectedEventArgs args)
        {
            Mode.HandlePeerDisconnected (args.Peer);
            PeerDisconnected?.InvokeAsync (this, args);
        }

        internal void RaisePeersFound (PeersAddedEventArgs args)
        {
            PeersFound?.InvokeAsync (this, args);
        }

        internal void OnPieceHashed (int index, bool hashPassed)
            => OnPieceHashed (index, hashPassed, 1, 1);

        internal void OnPieceHashed (int index, bool hashPassed, int piecesHashed, int totalToHash)
        {
            Bitfield[index] = hashPassed;
            // The PiecePickers will no longer ignore this piece as it has now been hash checked.
            UnhashedPieces[index] = false;

            // This gives us the index of *one* of the files we need to update. We need to search up
            // and down the array for other matching files as multiple files can share a piece.
            TorrentFile[] files = Torrent.Files;
            var fileIndex = files.BinarySearch (PieceIndexComparer, index);
            for (int i = fileIndex; i < files.Length && files[i].StartPieceIndex <= index; i++)
                files[i].BitField[index - files[i].StartPieceIndex] = hashPassed;

            for (int i = fileIndex - 1; i >= 0 && files[i].EndPieceIndex >= index; i--)
                files[i].BitField[index - files[i].StartPieceIndex] = hashPassed;

            if (hashPassed) {
                List<PeerId> connected = Peers.ConnectedPeers;
                for (int i = 0; i < connected.Count; i++)
                    connected[i].IsAllowedFastPieces.Remove (index);
            }

            PieceHashed?.InvokeAsync (this, new PieceHashedEventArgs (this, index, hashPassed, piecesHashed, totalToHash));
        }

        static readonly Func<TorrentFile, int, int> PieceIndexComparer = (TorrentFile file, int pieceIndex) => {
            if (pieceIndex >= file.StartPieceIndex && pieceIndex <= file.EndPieceIndex)
                return 0;
            if (pieceIndex > file.EndPieceIndex)
                return -1;
            else
                return 1;
        };

        internal void RaiseTorrentStateChanged (TorrentStateChangedEventArgs e)
        {
            TorrentStateChanged?.InvokeAsync (this, e);
        }

        /// <summary>
        /// Raise the connection attempt failed event
        /// </summary>
        /// <param name="args"></param>
        internal void RaiseConnectionAttemptFailed (ConnectionAttemptFailedEventArgs args)
        {
            ConnectionAttemptFailed?.InvokeAsync (this, args);
        }

        internal void UpdateLimiters ()
        {
            DownloadLimiter.UpdateChunks (Settings.MaximumDownloadSpeed, Monitor.DownloadSpeed);
            UploadLimiter.UpdateChunks (Settings.MaximumUploadSpeed, Monitor.UploadSpeed);
        }
        #endregion Internal Methods


        #region Private Methods

        void CheckMetadata ()
        {
            if (!HasMetadata)
                throw new InvalidOperationException ("This action cannot be performed until metadata has been retrieved");
        }

        void CheckRegisteredAndDisposed ()
        {
            if (Engine == null)
                throw new TorrentException ("This manager has not been registed with an Engine");
            if (Engine.Disposed)
                throw new InvalidOperationException ("The registered engine has been disposed");
        }

        internal PiecePicker CreateStandardPicker ()
        {
            PiecePicker picker = new StandardPicker ();
            picker = new RandomisedPicker (picker);
            picker = new RarestFirstPicker (picker);

            if (ClientEngine.SupportsEndgameMode)
                picker = new EndGameSwitcher (picker, new EndGamePicker (), this);

            picker = new PriorityPicker (picker);
            return picker;
        }

        public void LoadFastResume (FastResume data)
        {
            Check.Data (data);
            CheckMetadata ();
            if (State != TorrentState.Stopped)
                throw new InvalidOperationException ("Can only load FastResume when the torrent is stopped");
            if (InfoHash != data.Infohash || Torrent.Pieces.Count != data.Bitfield.Length)
                throw new ArgumentException ("The fast resume data does not match this torrent", "fastResumeData");

            for (int i = 0; i < Torrent.Pieces.Count; i++)
                OnPieceHashed (i, data.Bitfield[i], i, Torrent.Pieces.Count);
            UnhashedPieces.From (data.UnhashedPieces);

            HashChecked = true;
        }

        public FastResume SaveFastResume ()
        {
            CheckMetadata ();
            if (!HashChecked)
                throw new InvalidOperationException ("Fast resume data cannot be created when the TorrentManager has not been hash checked");
            return new FastResume (InfoHash, Bitfield, UnhashedPieces);
        }

        internal void SetTrackerManager (ITrackerManager manager)
        {
            if (TrackerManager != null) {
                TrackerManager.AnnounceComplete -= HandleTrackerAnnounceComplete;
            }

            TrackerManager = manager;

            if (TrackerManager != null) {
                TrackerManager.AnnounceComplete += HandleTrackerAnnounceComplete;
            }
        }

        async void HandleTrackerAnnounceComplete (object o, AnnounceResponseEventArgs e)
        {
            if (e.Successful) {
                await ClientEngine.MainLoop;

                int count = AddPeers (e.Peers, true);
                RaisePeersFound (new TrackerPeersAdded (this, count, e.Peers.Count, e.Tracker));
            }
        }

        #endregion Private Methods

        internal bool TrySetError (Reason reason, Exception ex)
        {
            if (Mode is ErrorMode)
                return false;

            Error = new Error (reason, ex);
            Mode = new ErrorMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
            return true;
        }

        internal void HandlePeerConnected (PeerId id)
        {
            // The only message sent/received so far is the Handshake message.
            // The current mode decides what additional messages need to be sent.
            RaisePeerConnected (new PeerConnectedEventArgs (this, id));
            Mode.HandlePeerConnected (id);
        }
    }
}
