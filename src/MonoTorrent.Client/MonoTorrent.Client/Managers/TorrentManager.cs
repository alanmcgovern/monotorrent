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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Modes;
using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Messages.Peer;
using MonoTorrent.PiecePicking;
using MonoTorrent.Streaming;
using MonoTorrent.Trackers;

using ReusableTasks;

namespace MonoTorrent.Client
{
    public class TorrentManager : IEquatable<TorrentManager>, ITorrentData
    {
        #region Events

        internal event EventHandler<byte[]> MetadataReceived;

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

        public async Task SetFilePriorityAsync (ITorrentFileInfo file, Priority priority)
        {
            if (!Files.Contains (file))
                throw new ArgumentNullException (nameof (file), "The file is not part of this torrent");

            // No change
            if (priority == file.Priority)
                return;

            await ClientEngine.MainLoop;

            // If the old priority, or new priority, is 'DoNotDownload' then the selector needs to be refreshed
            bool needsToUpdateSelector = file.Priority == Priority.DoNotDownload || priority == Priority.DoNotDownload;
            ((TorrentFileInfo) file).Priority = priority;

            if (needsToUpdateSelector) {
                // If we change the priority of a file we need to figure out which files are marked
                // as 'DoNotDownload' and which ones are downloadable.
                PartialProgressSelector.SetAll (false);
                if (Files.All (t => t.Priority != Priority.DoNotDownload)) {
                    PartialProgressSelector.SetAll (true);
                } else {
                    PartialProgressSelector.SetAll (false);
                    foreach (var f in Files.Where (t => t.Priority != Priority.DoNotDownload))
                        PartialProgressSelector.SetTrue ((f.StartPieceIndex, f.EndPieceIndex));
                }
            }
        }

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

        internal Queue<HaveMessage> finishedPieces;     // The list of pieces which we should send "have" messages for
        Mode mode;
        internal DateTime lastCalledInactivePeerManager = DateTime.Now;
        TaskCompletionSource<Torrent> MetadataTask { get; }
        #endregion Member Variables


        #region Properties

        public BitField Bitfield => MutableBitField;

        internal MutableBitField MutableBitField { get; private set; }

        public bool CanUseDht => Settings.AllowDht && (Torrent == null || !Torrent.IsPrivate);

        public bool CanUseLocalPeerDiscovery => ClientEngine.SupportsLocalPeerDiscovery && (Torrent == null || !Torrent.IsPrivate) && Engine != null;

        /// <summary>
        /// Returns true only when all files have been fully downloaded. If some files are marked as 'DoNotDownload' then the
        /// torrent will not be considered to be Complete until they are downloaded.
        /// </summary>
        public bool Complete => Bitfield.AllTrue;

        internal bool Disposed { get; private set; }

        RateLimiter DownloadLimiter { get; }

        internal RateLimiterGroup DownloadLimiters { get; }

        public ClientEngine Engine { get; }

        public Error Error { get; private set; }

        public IList<ITorrentFileInfo> Files { get; private set; }

        string ITorrentData.Name => Torrent == null ? null : Torrent.Name;

        public int PieceLength => Torrent == null ? -1 : Torrent.PieceLength;
        public long Size => Torrent == null ? -1 : Torrent.Size;

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

        internal void RaiseMetadataReceived (byte[] metadata)
        {
            MetadataReceived?.Invoke (this, metadata);
        }

        /// <summary>
        /// Marks the <see cref="TorrentManager"/> as needing a full hash check. If <see cref="EngineSettings.AutoSaveLoadFastResume"/>
        /// is enabled this method will also delete fast resume data from the location specified by
        /// <see cref="EngineSettings.GetFastResumePath(InfoHash)"/>. This can only be invoked when the <see cref="State"/> is
        /// <see cref="TorrentState.Stopped"/>.
        /// </summary>
        /// <returns></returns>
        public async Task SetNeedsHashCheckAsync ()
        {
            await ClientEngine.MainLoop;
            if (State != TorrentState.Stopped)
                throw new InvalidOperationException ("SetNeedsHashCheckAsync can only be called when the TorrentManager is in the 'Stopped' state");
            SetNeedsHashCheck ();
        }

        internal void SetNeedsHashCheck ()
        {
            HashChecked = false;
            if (Engine.Settings.AutoSaveLoadFastResume) {
                var path = Engine.Settings.GetFastResumePath (InfoHash);
                if (File.Exists (path))
                    File.Delete (path);
            }
        }

        /// <summary>
        /// If <see cref="ITorrentFileInfo.Priority"/> is set to <see cref="Priority.DoNotDownload"/> then the pieces
        /// associated with that <see cref="TorrentFile"/> will not be hash checked. An IgnoringPicker is used
        /// to ensure pieces which have not been hash checked are never downloaded.
        /// </summary>
        internal MutableBitField UnhashedPieces { get; set; }

        public bool HashChecked { get; private set; }

        public int HashFails { get; internal set; }

        public bool HasMetadata => Torrent != null;

        public InfoHash InfoHash => Torrent?.InfoHash ?? MagnetLink.InfoHash;

        /// <summary>
        /// The path to the .torrent metadata used to create the TorrentManager. Typically stored within the <see cref="EngineSettings.MetadataCacheDirectory"/> directory.
        /// </summary>
        public string MetadataPath { get; }

        /// <summary>
        /// True if this torrent has activated special processing for the final few pieces
        /// </summary>
        public bool IsInEndGame => State == TorrentState.Downloading && PieceManager.InEndgameMode;

        public ConnectionMonitor Monitor { get; }

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
        /// Internal timer used to trigger Local PeerDiscovery announces every interval.
        /// </summary>
        internal ValueStopwatch LastLocalPeerAnnounceTimer;

        public MagnetLink MagnetLink { get; }

        internal MutableBitField PartialProgressSelector { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public PeerManager Peers { get; }


        /// <summary>
        /// The piecemanager for this TorrentManager
        /// </summary>
        public PieceManager PieceManager { get; }


        /// <summary>
        /// The inactive peer manager for this TorrentManager
        /// </summary>
        internal InactivePeerManager InactivePeerManager { get; }

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
        public string SavePath { get; private set; }

        /// <summary>
        /// The settings for with this TorrentManager
        /// </summary>
        public TorrentSettings Settings { get; private set; }

        /// <summary>
        /// The current state of the TorrentManager
        /// </summary>
        public TorrentState State => mode.State;

        /// <summary>
        /// The time the torrent manager was started at
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// When a <see cref="Torrent"/> or <see cref="MagnetLink"/> has been added using
        /// the 'AddStreamingAsync' methods on <see cref="ClientEngine"/> then this property
        /// will be non-null and streams can be created to access any of the files in the
        /// torrent while they are downloading. These streams are fully seekable, and if you
        /// seek to a position which has not been downloaded already the required pieces will
        /// be prioritised next.
        /// </summary>
        public StreamProvider StreamProvider { get; internal set; }

        /// <summary>
        /// The tracker connection associated with this TorrentManager
        /// </summary>
        public ITrackerManager TrackerManager { get; private set; }

        /// <summary>
        /// The Torrent contained within this TorrentManager
        /// </summary>
        public Torrent Torrent { get; private set; }

        /// <summary>
        /// The number of peers that we are currently uploading to
        /// </summary>
        public int UploadingTo { get; internal set; }

        RateLimiter UploadLimiter { get; }

        internal RateLimiterGroup UploadLimiters { get; }

        public bool IsInitialSeeding => Mode is InitialSeedingMode;

        #endregion

        #region Constructors

        internal TorrentManager (ClientEngine engine, Torrent torrent, string savePath, TorrentSettings settings)
            : this (engine, torrent, null, savePath, settings)
        {
            SetMetadata (torrent);
        }

        internal TorrentManager (ClientEngine engine, MagnetLink magnetLink, string savePath, TorrentSettings settings)
            : this (engine, null, magnetLink, savePath, settings)
        {
        }

        TorrentManager (ClientEngine engine, Torrent torrent, MagnetLink magnetLink, string savePath, TorrentSettings settings)
        {
            Engine = engine;
            MagnetLink = magnetLink ?? new MagnetLink (torrent.InfoHash, torrent.Name, torrent.AnnounceUrls.SelectMany (t => t).ToArray (), null, torrent.Size);
            Torrent = torrent;
            Settings = settings;

            MetadataTask = new TaskCompletionSource<Torrent> ();
            MetadataPath = engine.Settings.GetMetadataPath (InfoHash);

            var announces = Torrent?.AnnounceUrls;
            if (announces == null) {
                announces = new List<IList<string>> ();
                if (magnetLink.AnnounceUrls != null)
                    announces.Add (magnetLink.AnnounceUrls);
            }
            SetTrackerManager (new TrackerManager (engine.Factories, new TrackerRequestFactory (this), announces, torrent?.IsPrivate ?? false));

            MutableBitField = new MutableBitField (HasMetadata ? Torrent.Pieces.Count : 1);
            PartialProgressSelector = new MutableBitField (HasMetadata ? Torrent.Pieces.Count : 1);
            UnhashedPieces = new MutableBitField (HasMetadata ? Torrent.Pieces.Count : 1).SetAll (true);
            SavePath = string.IsNullOrEmpty (savePath) ? Environment.CurrentDirectory : Path.GetFullPath (savePath);
            finishedPieces = new Queue<HaveMessage> ();
            Monitor = new ConnectionMonitor ();
            InactivePeerManager = new InactivePeerManager (this);
            Peers = new PeerManager ();
            PieceManager = new PieceManager (this);

            mode = new StoppedMode (this, null, null, null);
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

        internal void ChangePicker (IPieceRequester requester)
        {
            if (requester == null)
                throw new ArgumentNullException (nameof (requester));

            PieceManager.ChangePicker (requester);
            if (requester is IStreamingPieceRequester streamingRequester)
                StreamProvider = new StreamProvider (this, streamingRequester);
            else
                StreamProvider = null;
        }

        /// <summary>
        /// Changes the active piece picker. This can be called when the manager is running, or when it is stopped.
        /// </summary>
        /// <param name="requester">The new picker to use.</param>
        /// <returns></returns>
        public async Task ChangePickerAsync (IPieceRequester requester)
        {
            await ClientEngine.MainLoop;
            ChangePicker (requester);
        }

        internal void Dispose ()
        {
            if (Disposed)
                return;

            Disposed = true;
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
                await MaybeWriteFastResumeAsync ();

                Mode = new StoppedMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
            }
        }

        public async Task MoveFileAsync (ITorrentFileInfo file, string path)
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
                throw;
            }
        }

        public async Task MoveFilesAsync (string newRoot, bool overWriteExisting)
        {
            CheckRegisteredAndDisposed ();
            CheckMetadata ();

            if (State != TorrentState.Stopped)
                throw new TorrentException ("Cannot move files when the torrent is active");

            try {
                await Engine.DiskManager.MoveFilesAsync (Files, newRoot, overWriteExisting);
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

        internal void SetMetadata (Torrent torrent)
        {
            Torrent = torrent;
            foreach (PeerId id in new List<PeerId> (Peers.ConnectedPeers))
                Engine.ConnectionManager.CleanupSocket (this, id);
            MutableBitField = new MutableBitField (Torrent.Pieces.Count);
            PartialProgressSelector = new MutableBitField (Torrent.Pieces.Count).SetAll (true);
            UnhashedPieces = new MutableBitField (Torrent.Pieces.Count).SetAll (true);

            // Now we know the torrent name, use it as the base directory name when it's a multi-file torrent
            var savePath = SavePath;
            if (Torrent.Files.Count > 1 && Settings.CreateContainingDirectory)
                savePath = Path.Combine (savePath, Torrent.Name);

            // All files marked as 'Normal' priority by default so 'PartialProgressSelector'
            // should be set to 'true' for each piece as all files are being downloaded.
            Files = Torrent.Files.Select (file => {
                var downloadCompleteFullPath = Path.Combine (savePath, file.Path);
                var downloadIncompleteFullPath = downloadCompleteFullPath + TorrentFileInfo.IncompleteFileSuffix;

                // FIXME: Is this the best place to futz with actually moving files?
                if (!Engine.Settings.UsePartialFiles) {
                    if (File.Exists (downloadIncompleteFullPath) && !File.Exists (downloadCompleteFullPath))
                        File.Move (downloadIncompleteFullPath, downloadCompleteFullPath);

                    downloadIncompleteFullPath = downloadCompleteFullPath;
                }

                var currentPath = File.Exists (downloadCompleteFullPath) ? downloadCompleteFullPath : downloadIncompleteFullPath;
                return new TorrentFileInfo (file, currentPath) {
                    DownloadCompleteFullPath = downloadCompleteFullPath,
                    DownloadIncompleteFullPath = downloadIncompleteFullPath
                };
            }).Cast<ITorrentFileInfo> ().ToList ().AsReadOnly ();

            PieceManager.Initialise ();
            MetadataTask.SetResult (Torrent);
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

            // If the torrent was "paused", then just update the state to Downloading and forcefully
            // make sure the peers begin sending/receiving again
            if (State == TorrentState.Paused) {
                Mode = new DownloadMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
            } else if (Mode is HashingMode hashing && !HashChecked) {
                if (State == TorrentState.HashingPaused)
                    hashing.Resume ();
            } else {
                await Engine.StartAsync ();
                StartTime = DateTime.Now;
                if (!HasMetadata) {
                    Mode = new MetadataMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings, MetadataPath, metadataOnly);
                } else {
                    var startingMode = new StartingMode (this, Engine.DiskManager, Engine.ConnectionManager, Engine.Settings);
                    Mode = startingMode;
                    _ = startingMode.WaitForStartingToComplete ();
                }
            }
        }

        public async Task LocalPeerAnnounceAsync ()
        {
            await ClientEngine.MainLoop;

            if (CanUseLocalPeerDiscovery && (!LastLocalPeerAnnounceTimer.IsRunning || LastLocalPeerAnnounceTimer.Elapsed > Engine.LocalPeerDiscovery.MinimumAnnounceInternal)) {
                if (Engine.PeerListener.LocalEndPoint != null) {
                    LastLocalPeerAnnounce = DateTime.Now;
                    LastLocalPeerAnnounceTimer.Restart ();

                    await Engine?.LocalPeerDiscovery.Announce (InfoHash, Engine.PeerListener.LocalEndPoint);
                }
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
            if (CanUseDht && Engine != null && (!LastDhtAnnounceTimer.IsRunning || LastDhtAnnounceTimer.Elapsed > Engine.DhtEngine.MinimumAnnounceInterval)) {
                LastDhtAnnounce = DateTime.UtcNow;
                LastDhtAnnounceTimer.Restart ();
                Engine.DhtEngine.GetPeers (InfoHash);
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
                await MaybeWriteFastResumeAsync ();
                await Engine.StopAsync ();
            }
        }

        public async Task UpdateSettingsAsync (TorrentSettings settings)
        {
            await ClientEngine.MainLoop;
            Settings = settings;
        }

        /// <summary>
        /// Waits for the metadata to be available
        /// </summary>
        /// <returns></returns>
        public Task WaitForMetadataAsync ()
            => WaitForMetadataAsync (CancellationToken.None);

        public async Task WaitForMetadataAsync (CancellationToken token)
        {
            // Fast path (if possible).
            if (HasMetadata)
                return;

            var tcs = new TaskCompletionSource<object> ();
            using var registration = token.Register (tcs.SetCanceled);

            // Wait for the token to be cancelled *or* the metadata is received.
            // Await the returned task so the OperationCancelled exception propagates as
            // expected if the token was cancelled. The try/catch is so that we
            // will always throw an OperationCancelled instead of, sometimes, propagating
            // a TaskCancelledException.
            try {
                await (await Task.WhenAny (MetadataTask.Task, tcs.Task));
            } catch {
                token.ThrowIfCancellationRequested ();
                throw;
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

            if (Engine.PeerId.Equals (peer.PeerId))
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
            MutableBitField[index] = hashPassed;
            // The PiecePickers will no longer ignore this piece as it has now been hash checked.
            UnhashedPieces[index] = false;

            var files = Files;
            var fileIndex = files.FindFileByPieceIndex (index);
            for (int i = fileIndex; i < files.Count && files[i].StartPieceIndex <= index; i++) {
                ((MutableBitField) files[i].BitField)[index - files[i].StartPieceIndex] = hashPassed;

                // If we're only hashing 1 piece then we can start moving files now. This occurs when a torrent
                // is actively downloading.
                if (totalToHash == 1)
                    _ = RefreshPartialDownloadFilePaths (i, 1);
            }

            // If we're hashing many pieces, wait for the final piece to be hashed, then start trying to move files.
            // This occurs when we're hash checking, or loading, torrents.
            if (totalToHash > 1 && piecesHashed == totalToHash)
                _ = RefreshPartialDownloadFilePaths (0, files.Count);

            if (hashPassed) {
                List<PeerId> connected = Peers.ConnectedPeers;
                for (int i = 0; i < connected.Count; i++)
                    connected[i].IsAllowedFastPieces.Remove (index);
            }
            PieceHashed?.InvokeAsync (this, new PieceHashedEventArgs (this, index, hashPassed, piecesHashed, totalToHash));
        }

        internal async ReusableTask UpdateUsePartialFiles (bool usePartialFiles)
        {
            foreach (TorrentFileInfo file in Files)
                file.DownloadIncompleteFullPath = file.DownloadCompleteFullPath + (usePartialFiles ? TorrentFileInfo.IncompleteFileSuffix : "");
            await RefreshPartialDownloadFilePaths (0, Files.Count);
        }

        internal async ReusableTask RefreshPartialDownloadFilePaths (int fileStartIndex, int count)
        {
            var files = Files;
            var tasks = new List<Task> ();
            for (int i = fileStartIndex; i < fileStartIndex + count; i++) {
                if (files[i].BitField.AllTrue && files[i].FullPath != files[i].DownloadCompleteFullPath)
                    tasks.Add (Engine.DiskManager.MoveFileAsync (files[i], files[i].DownloadCompleteFullPath));
                else if (!files[i].BitField.AllTrue && files[i].FullPath != files[i].DownloadIncompleteFullPath)
                    tasks.Add (Engine.DiskManager.MoveFileAsync (files[i], files[i].DownloadIncompleteFullPath));
            }
            await Task.WhenAll (tasks).ConfigureAwait (false);
        }

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
            if (Disposed)
                throw new InvalidOperationException ("This TorrentManager has been removed from it's Engine.");
            if (Engine.Disposed)
                throw new InvalidOperationException ("The registered engine has been disposed");
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
                OnPieceHashed (i, data.Bitfield[i], i + 1, Torrent.Pieces.Count);
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

        internal async ReusableTask MaybeDeleteFastResumeAsync ()
        {
            if (!Engine.Settings.AutoSaveLoadFastResume)
                return;

            try {
                var path = Engine.Settings.GetFastResumePath (InfoHash);
                if (File.Exists (path))
                    await Task.Run (() => File.Delete (path));
            } catch {
                // FIXME: I don't think we really care about this? Log it?
            }
        }

        internal async ReusableTask MaybeLoadFastResumeAsync ()
        {
            if (!Engine.Settings.AutoSaveLoadFastResume || !HasMetadata)
                return;

            await MainLoop.SwitchToThreadpool ();
            var fastResumePath = Engine.Settings.GetFastResumePath (InfoHash);
            if (File.Exists (fastResumePath) && FastResume.TryLoad (fastResumePath, out FastResume fastResume) && InfoHash == fastResume.Infohash) {
                await ClientEngine.MainLoop;
                LoadFastResume (fastResume);
            }
        }

        internal async ReusableTask MaybeWriteFastResumeAsync ()
        {
            if (!Engine.Settings.AutoSaveLoadFastResume || !HashChecked)
                return;

            ClientEngine.MainLoop.CheckThread ();
            var fastResumeData = SaveFastResume ().Encode ();

            await MainLoop.SwitchToThreadpool ();
            var fastResumePath = Engine.Settings.GetFastResumePath (InfoHash);
            var parentDirectory = Path.GetDirectoryName (fastResumePath);
            Directory.CreateDirectory (parentDirectory);
            File.WriteAllBytes (fastResumePath, fastResumeData);
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

                int count = AddPeers (e.Peers.Select (t => new Peer (BEncodedString.FromMemory (t.PeerId), t.Uri)).ToArray (), true);
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
