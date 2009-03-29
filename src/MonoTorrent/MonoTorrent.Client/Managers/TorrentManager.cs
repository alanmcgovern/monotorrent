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
using System.Text;
using System.Net;
using MonoTorrent.Common;
using System.Threading;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
{
    public class TorrentManager : IDisposable, IEquatable<TorrentManager>
    {
        private bool abortHashing;
        private ManualResetEvent hashingWaitHandle;

        #region Events

        public event EventHandler<PeerConnectionEventArgs> PeerConnected;

        public event EventHandler<PeerConnectionEventArgs> PeerDisconnected;

        internal event EventHandler<PeerConnectionFailedEventArgs> ConnectionAttemptFailed;

        public event EventHandler<PeersAddedEventArgs> PeersFound;

        public event EventHandler<PieceHashedEventArgs> PieceHashed;

        public event EventHandler<TorrentStateChangedEventArgs> TorrentStateChanged;

        internal event EventHandler<PeerAddedEventArgs> OnPeerFound;

        #endregion


        #region Member Variables

        private BitField bitfield;              // The bitfield representing the pieces we've downloaded and have to download
        private bool disposed;
        private ClientEngine engine;            // The engine that this torrent is registered with
        private Error error;
        internal Queue<int> finishedPieces;     // The list of pieces which we should send "have" messages for
        private bool hashChecked;               // True if the manager has been hash checked
        private int hashFails;                  // The total number of pieces receieved which failed the hashcheck
        private InfoHash infohash;
		internal bool isInEndGame = false;       // Set true when the torrent enters end game processing
        private Mode mode;
        private ConnectionMonitor monitor;      // Calculates download/upload speed
        private PeerManager peers;              // Stores all the peers we know of in a list
        private PieceManager pieceManager;      // Tracks all the piece requests we've made and decides what pieces we can request off each peer
        private string savePath;
        private RateLimiterGroup uploadLimiter;     // Contains the logic to decide how many chunks we can download
        private RateLimiterGroup downloadLimiter;   // Contains the logic to decide how many chunks we can download
        private TorrentSettings settings;       // The settings for this torrent
        private DateTime startTime;             // The time at which the torrent was started at.
        private TorrentState state;             // The current state (seeding, downloading etc)
        private Torrent torrent;                // All the information from the physical torrent that was loaded
        private TrackerManager trackerManager;  // The class used to control all access to the tracker
        private int uploadingTo;                // The number of peers which we're currently uploading to
        internal IUnchoker chokeUnchoker; // Used to choke and unchoke peers
		private InactivePeerManager inactivePeerManager; // Used to identify inactive peers we don't want to connect to
		internal DateTime lastCalledInactivePeerManager = DateTime.Now;

        #endregion Member Variables


        #region Properties

        public BitField Bitfield
        {
            get { return this.bitfield; }
            internal set { bitfield = value; }
        }

        public bool CanUseDht
        {
            get { return !torrent.IsPrivate && settings.UseDht; }
        }

        public bool Complete
        {
            get { return this.bitfield.AllTrue; }
        }

        internal RateLimiterGroup DownloadLimiter
        {
            get { return downloadLimiter; }
        }

        public ClientEngine Engine
        {
            get { return this.engine; }
            internal set { this.engine = value; }
        }

        public Error Error
        {
            get { return error; }
            internal set { error = value; }
        }

        internal Mode Mode
        {
            get { return mode; }
            set { mode = value; }
        }

        public int PeerReviewRoundsComplete
        {
            get
            {
                if (this.chokeUnchoker is ChokeUnchokeManager)
                    return ((ChokeUnchokeManager)this.chokeUnchoker).ReviewsExecuted;
                else
                    return 0;
            }
        }


        public bool HashChecked
        {
            get { return this.hashChecked; }
            internal set { this.hashChecked = value; }
        }

        public int HashFails
        {
            get { return this.hashFails; }
        }

        public bool HasMetadata
        {
            get { return torrent != null; }
        }

		/// <summary>
		/// True if this torrent has activated special processing for the final few pieces
		/// </summary>
		public bool IsInEndGame
		{
			get { return this.state == TorrentState.Downloading && this.isInEndGame; }
		}

        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }


        /// <summary>
        /// The number of peers that this torrent instance is connected to
        /// </summary>
        public int OpenConnections
        {
            get { return this.Peers.ConnectedPeers.Count; }
        }


        /// <summary>
        /// 
        /// </summary>
        public PeerManager Peers
        {
            get { return this.peers; }
        }


		/// <summary>
		/// The piecemanager for this TorrentManager
		/// </summary>
		public PieceManager PieceManager
		{
			get { return this.pieceManager; }
            internal set { pieceManager = value; }
		}


		/// <summary>
		/// The inactive peer manager for this TorrentManager
		/// </summary>
		internal InactivePeerManager InactivePeerManager
		{
			get { return this.inactivePeerManager; }
		}


        /// <summary>
        /// The current progress of the torrent in percent
        /// </summary>
        public double Progress
        {
            get { return (this.bitfield.PercentComplete); }
        }


        /// <summary>
        /// The directory to download the files to
        /// </summary>
        public string SavePath
        {
            get { return this.savePath; }
        }


        /// <summary>
        /// The settings for with this TorrentManager
        /// </summary>
        public TorrentSettings Settings
        {
            get { return this.settings; }
        }


        /// <summary>
        /// The current state of the TorrentManager
        /// </summary>
        public TorrentState State
        {
            get { return this.state; }
        }


        /// <summary>
        /// The time the torrent manager was started at
        /// </summary>
        public DateTime StartTime
        {
            get { return this.startTime; }
        }


        /// <summary>
        /// The tracker connection associated with this TorrentManager
        /// </summary>
        public TrackerManager TrackerManager
        {
            get { return this.trackerManager; }
        }


        /// <summary>
        /// The Torrent contained within this TorrentManager
        /// </summary>
        public Torrent Torrent
        {
            get { return this.torrent; }
            internal set { torrent = value; }
        }


        /// <summary>
        /// The number of peers that we are currently uploading to
        /// </summary>
        public int UploadingTo
        {
            get { return this.uploadingTo; }
            internal set { this.uploadingTo = value; }
        }

        internal RateLimiterGroup UploadLimiter
        {
            get { return uploadLimiter; }
        }

        public bool IsInitialSeeding
        {
            get { return mode is InitialSeedingMode; }
        }

		/// <summary>
		/// Number of peers we have inactivated for this torrent
		/// </summary>
		public int InactivePeers
		{
			get { return inactivePeerManager.InactivePeers; }
		}

        public InfoHash InfoHash
        {
            get { return infohash; }
        }

		/// <summary>
		/// List of peers we have inactivated for this torrent
		/// </summary>
		public List<Uri> InactivePeerList
		{
			get { return inactivePeerManager.InactivePeerList; }
		}

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        /// <param name="settings">The settings to use for controlling connections</param>
        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings)
            : this(torrent, savePath, settings, torrent.Files.Length == 1 ? "" : torrent.Name)
        {

        }

        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        /// <param name="settings">The settings to use for controlling connections</param>
        /// <param name="baseDirectory">In the case of a multi-file torrent, the name of the base directory containing the files. Defaults to Torrent.Name</param>
        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings, string baseDirectory)
        {
            Check.Torrent(torrent);
            Check.SavePath(savePath);
            Check.Settings(settings);
            Check.BaseDirectory(baseDirectory);

            this.torrent = torrent;
            this.infohash = torrent.infoHash;
            this.settings = settings;

            mode = new DownloadMode(this);
            Initialise(savePath, baseDirectory, torrent.AnnounceUrls);
            ChangePicker(CreateStandardPicker());
        }


        public TorrentManager(InfoHash infoHash, string savePath, TorrentSettings settings, string torrentSave, List<MonoTorrentCollection<string>> announces)
        {
            Check.InfoHash(infoHash);
            Check.SavePath(savePath);
            Check.Settings(settings);
            Check.TorrentSave(torrentSave);
            Check.Announces(announces);

            this.infohash = infoHash;
            this.settings = settings;

            mode = new MetadataMode(this, torrentSave);
            Initialise(savePath, "", announces);
        }

        void Initialise(string savePath, string baseDirectory, List<MonoTorrentCollection<string>> announces)
        {
            this.bitfield = new BitField(HasMetadata ? torrent.Pieces.Count : 1);
            this.savePath = Path.Combine(savePath, baseDirectory);
            this.finishedPieces = new Queue<int>();
            this.hashingWaitHandle = new ManualResetEvent(false);
            this.monitor = new ConnectionMonitor();
            this.inactivePeerManager = new InactivePeerManager(this);
            this.peers = new PeerManager();
            this.pieceManager = new PieceManager();
            this.trackerManager = new TrackerManager(this, InfoHash, announces);
            CreateRateLimiters();

            PieceHashed += delegate(object o, PieceHashedEventArgs e) {
                PieceManager.UnhashedPieces[e.PieceIndex] = false;
            };
        }

        void CreateRateLimiters()
        {
            RateLimiter downloader = new RateLimiter();
            downloadLimiter = new RateLimiterGroup();
            downloadLimiter.Add(new PauseLimiter(this));
            downloadLimiter.Add(downloader);

            RateLimiter uploader = new RateLimiter();
            uploadLimiter = new RateLimiterGroup();
            uploadLimiter.Add(new PauseLimiter(this));
            uploadLimiter.Add(uploader);

            ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromSeconds(1), delegate {
                downloader.UpdateChunks(Settings.MaxDownloadSpeed, Monitor.DownloadSpeed);
                uploader.UpdateChunks(Settings.MaxUploadSpeed, Monitor.UploadSpeed);
                return !disposed;
            });
        }

        #endregion


        #region Public Methods

        public void ChangePicker(PiecePicker picker)
        {
            Check.Picker(picker);

            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                this.pieceManager.ChangePicker(picker, bitfield, torrent.Files);
            });
        }

        public void Dispose()
        {
            disposed = true;
        }


        /// <summary>
        /// Overrridden. Returns the name of the torrent.
        /// </summary>
        /// <returns></returns>
        public override string ToString( )
        {
            return this.Torrent.Name;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            TorrentManager m = obj as TorrentManager;
            return (m == null) ? false : this.Equals(m);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(TorrentManager other)
        {
            return (other == null) ? false : infohash == other.infohash;
        }

        public List<Piece> GetActiveRequests()
        {
            return (List<Piece>)ClientEngine.MainLoop.QueueWait((MainLoopJob)delegate {
                return PieceManager.Picker.ExportActiveRequests();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return infohash.GetHashCode();
        }

        public List<PeerId> GetPeers()
        {
            return (List<PeerId>)ClientEngine.MainLoop.QueueWait((MainLoopJob)delegate {
                return new List<PeerId>(peers.ConnectedPeers);
            });
        }

        /// <summary>
        /// Starts a hashcheck. If forceFullScan is false, the library will attempt to load fastresume data
        /// before performing a full scan, otherwise fast resume data will be ignored and a full scan will be started
        /// </summary>
        /// <param name="forceFullScan">True if a full hash check should be performed ignoring fast resume data</param>
        public void HashCheck(bool autoStart)
        {
            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                if (this.state != TorrentState.Stopped)
                    throw new TorrentException(string.Format("A hashcheck can only be performed when the manager is stopped. State is: {0}", state));

                CheckRegisteredAndDisposed();
                this.startTime = DateTime.Now;
                UpdateState(TorrentState.Hashing);
                ThreadPool.QueueUserWorkItem(delegate { PerformHashCheck(autoStart); }); 
            });
        }

        public void MoveFiles(string newPath, bool overWriteExisting)
        {
            CheckRegisteredAndDisposed();
            CheckMetadata();

            if (State != TorrentState.Stopped)
                throw new TorrentException("Cannot move the files when the torrent is active");

            Engine.DiskManager.MoveFiles(this, savePath, newPath, overWriteExisting);
            savePath = newPath;
        }

        /// <summary>
        /// Pauses the TorrentManager
        /// </summary>
        public void Pause()
        {
            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                CheckRegisteredAndDisposed();
                if (state != TorrentState.Downloading && state != TorrentState.Seeding)
                    return;

                // By setting the state to "paused", peers will not be dequeued from the either the
                // sending or receiving queues, so no traffic will be allowed.
                UpdateState(TorrentState.Paused);
                this.SaveFastResume();
            });
        }


        /// <summary>
        /// Starts the TorrentManager
        /// </summary>
        public void Start()
        {
            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                CheckRegisteredAndDisposed();

                this.engine.Start();
                // If the torrent was "paused", then just update the state to Downloading and forcefully
                // make sure the peers begin sending/receiving again
                if (this.state == TorrentState.Paused)
                {
                    UpdateState(TorrentState.Downloading);
                    return;
                }

                // If the torrent has not been hashed, we start the hashing process then we wait for it to finish
                // before attempting to start again
                if (!hashChecked)
                {
                    if (state != TorrentState.Hashing)
                        HashCheck(true);
                    return;
                }

                if (this.state == TorrentState.Seeding || this.state == TorrentState.Downloading)
                    return;

                if (this.Complete) {
                    mode = new InitialSeedingMode(this);
                    UpdateState(TorrentState.Seeding);
                }
                else
                    UpdateState(TorrentState.Downloading);

                if (TrackerManager.CurrentTracker != null)
                {
                    if (this.trackerManager.CurrentTracker.CanScrape)
                        this.TrackerManager.Scrape();
                    this.trackerManager.Announce(TorrentEvent.Started); // Tell server we're starting
                }

#if !DISABLE_DHT
                if (HasMetadata && !torrent.IsPrivate)
                {
                    engine.DhtEngine.PeersFound += delegate (object o, PeersFoundEventArgs e) { DhtPeersFound(o, e);};

                    // First get some peers
                    engine.DhtEngine.GetPeers(torrent.infoHash);

                    // Second, get peers every 10 minutes (if we need them)
                    ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromMinutes(10), delegate {
                        // Torrent is no longer active
                        if (State != TorrentState.Seeding && State != TorrentState.Downloading)
                            return false;

                        // Only use DHT if it hasn't been (temporarily?) disabled in settings
                        if (CanUseDht && Peers.AvailablePeers.Count < Settings.MaxConnections)
                        {
                            engine.DhtEngine.Announce(torrent.infoHash, engine.Settings.ListenPort);
                            engine.DhtEngine.GetPeers(torrent.infoHash);
                        }
                        return true;
                    });
                }
#endif
                this.startTime = DateTime.Now;
                if (engine.ConnectionManager.IsRegistered(this))
                    Logger.Log(null, "TorrentManager - Error, this manager is already in the connectionmanager!");
                else
                    engine.ConnectionManager.RegisterManager(this);
                this.pieceManager.Reset();

                ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromSeconds(2), delegate {
                    if (State != TorrentState.Downloading && State != TorrentState.Seeding)
                        return false;
                    pieceManager.Picker.CancelTimedOutRequests();
                    return true;
                });
            });
        }


        /// <summary>
        /// Stops the TorrentManager
        /// </summary>
        public WaitHandle Stop()
        {
            return (WaitHandle)ClientEngine.MainLoop.QueueWait((MainLoopJob)delegate {
                CheckRegisteredAndDisposed();
#if !DISABLE_DHT
                engine.DhtEngine.PeersFound -= DhtPeersFound;
#endif
                ManagerWaitHandle handle = new ManagerWaitHandle("Global");
                try
                {
                    if (this.state == TorrentState.Stopped)
                        return handle;

                    if (this.state == TorrentState.Error)
                    {
                        UpdateState(TorrentState.Stopped);
                        error = null;
                        mode = new DownloadMode(this);
                        return handle;
                    }
                    if (this.state == TorrentState.Hashing)
                    {
                        hashingWaitHandle = new ManualResetEvent(false);
                        handle.AddHandle(hashingWaitHandle, "Hashing");
                        abortHashing = true;
                        UpdateState(TorrentState.Stopped);
                        return handle;
                    }

                    UpdateState(TorrentState.Stopped);

                    if (trackerManager.CurrentTracker != null)
                        handle.AddHandle(this.trackerManager.Announce(TorrentEvent.Stopped), "Announcing");

                    foreach (PeerId id in Peers.ConnectedPeers)
                        if (id.Connection != null)
                            id.Connection.Dispose();

                    this.peers.ClearAll();

                    handle.AddHandle(engine.DiskManager.CloseFileStreams(this, SavePath, Torrent.Files), "DiskManager");

                    if (this.hashChecked)
                        this.SaveFastResume();
                    this.monitor.Reset();
                    this.pieceManager.Reset();
                    if (this.engine.ConnectionManager.IsRegistered(this))
                        this.engine.ConnectionManager.UnregisterManager(this);
                    this.engine.Stop();
                }
                finally
                {

                }

                return handle; ;
            });
        }

        #endregion


        #region Internal Methods

        internal int AddPeers(Peer peer)
        {
            try
            {
                if (this.peers.Contains(peer))
                    return 0;

				// Ignore peers in the inactive list
				if (this.inactivePeerManager.InactivePeerList.Contains(peer.ConnectionUri))
					return 0;

                this.peers.AvailablePeers.Add(peer);
                if (OnPeerFound != null)
                    OnPeerFound(this, new PeerAddedEventArgs(this, peer));
                // When we successfully add a peer we try to connect to the next available peer
                return 1;
            }
            finally
            {
                ClientEngine e = this.engine;
                if (e != null)
                    e.ConnectionManager.TryConnect();
            }
        }

        internal int AddPeers(IEnumerable<Peer> peers)
        {
            int count = 0;
            foreach (Peer p in peers)
                count += AddPeers(p);
            return count;
        }

        internal void HashedPiece(PieceHashedEventArgs pieceHashedEventArgs)
        {
            if (!pieceHashedEventArgs.HashPassed)
                Interlocked.Increment(ref this.hashFails);

            RaisePieceHashed(pieceHashedEventArgs);
        }
        
        internal void RaisePeerConnected(PeerConnectionEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<PeerConnectionEventArgs>(PeerConnected, this, args);
        }
        
        internal void RaisePeerDisconnected(PeerConnectionEventArgs args)
        {
            mode.HandlePeerDisconnected(args.PeerID);
            Toolbox.RaiseAsyncEvent<PeerConnectionEventArgs>(PeerDisconnected, this, args);
        }

        internal void RaisePeersFound(PeersAddedEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<PeersAddedEventArgs>(PeersFound, this, args);
        }

        internal void RaisePieceHashed(PieceHashedEventArgs args)
        {
            int index = args.PieceIndex;
            TorrentFile[] files = this.torrent.Files;
            
            for (int i = 0; i < files.Length; i++)
                if (index >= files[i].StartPieceIndex && index <= files[i].EndPieceIndex)
                    files[i].BitField[index - files[i].StartPieceIndex] = args.HashPassed;

            Toolbox.RaiseAsyncEvent<PieceHashedEventArgs>(PieceHashed, this, args);
        }

        internal void RaiseTorrentStateChanged(TorrentStateChangedEventArgs e)
        {
            // Whenever we have a state change, we need to make sure that we flush the buffers.
            // For example, Started->Paused, Started->Stopped, Downloading->Seeding etc should all
            // flush to disk.
            Toolbox.RaiseAsyncEvent<TorrentStateChangedEventArgs>(TorrentStateChanged, this, e);
        }

        /// <summary>
        /// Raise the connection attempt failed event
        /// </summary>
        /// <param name="args"></param>
        internal void RaiseConnectionAttemptFailed(PeerConnectionFailedEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<PeerConnectionFailedEventArgs>(this.ConnectionAttemptFailed, this, args);
        }

        #endregion Internal Methods


        #region Private Methods

        void CheckMetadata()
        {
            if (!HasMetadata)
                throw new InvalidOperationException("This action cannot be performed until metadata has been retrieved");
        }

        private void CheckRegisteredAndDisposed()
        {
            if (engine == null)
                throw new TorrentException("This manager has not been registed with an Engine");
            if (engine.Disposed)
                throw new InvalidOperationException("The registered engine has been disposed");
        }

        internal PiecePicker CreateStandardPicker()
        {
            PiecePicker picker;
            if (ClientEngine.EnableEndgameMode)
                picker = new EndGameSwitcher(new StandardPicker(), new EndGamePicker(), torrent.PieceLength / Piece.BlockSize, this);
            else
                picker = new StandardPicker();
            picker = new RandomisedPicker(picker);
            picker = new RarestFirstPicker(picker);
            picker = new PriorityPicker(picker);
            return picker;
        }

#if !DISABLE_DHT
        private void DhtPeersFound(object o, PeersFoundEventArgs e)
        {
            if (torrent.InfoHash != e.InfoHash)
                return;

            int count = AddPeers(e.Peers);
            RaisePeersFound(new DhtPeersAdded(this, count, e.Peers.Count));
        }
#endif

        private void PerformHashCheck(bool autoStart)
        {
            bool filesExist = false;
            try
            {
                // Store the value for whether the streams are open or not
                // If they are initially closed, we need to close them again after we hashcheck

                // We only need to hashcheck if at least one file already exists on the disk
                filesExist = HasMetadata && Engine.DiskManager.CheckFilesExist(this);

                if (abortHashing || mode is ErrorMode)
                    return;
                // A hashcheck should only be performed if some/all of the files exist on disk
                if (filesExist)
                {
                    for (int i = 0; i < this.torrent.Pieces.Count; i++)
                    {
                        bitfield[i] = this.torrent.Pieces.IsValid(engine.DiskManager.GetHash(this, i), i);
                        RaisePieceHashed(new PieceHashedEventArgs(this, i, bitfield[i]));

                        // This happens if the user cancels the hash by stopping the torrent.
                        if (abortHashing || mode is ErrorMode)
                            return;
                    }
                }
                else if (HasMetadata)
                {
                    bitfield.SetAll(false);
                    for (int i = 0; i < this.torrent.Pieces.Count; i++)
                        RaisePieceHashed(new PieceHashedEventArgs(this, i, false));
                }
				
                this.hashChecked = true;

                if (autoStart)
                    Start();
                else
                    UpdateState(TorrentState.Stopped);
            }
            finally
            {
                // Ensure file streams are all closed after hashing
                if (filesExist)
                    engine.DiskManager.CloseFileStreams (this, SavePath, Torrent.Files);

                if (abortHashing)
                {
                    abortHashing = false;
                    this.hashingWaitHandle.Set();
                }
            }
        }

        public void LoadFastResume(FastResume data)
        {
            Check.Data(data);
            CheckMetadata();
            if (State != TorrentState.Stopped)
                throw new InvalidOperationException("Can only load FastResume when the torrent is stopped");
            if (torrent.InfoHash != data.Infohash || torrent.Pieces.Count != data.Bitfield.Length)
                throw new ArgumentException("The fast resume data does not match this torrent", "fastResumeData");

            bitfield.From(data.Bitfield);
            for (int i = 0; i < torrent.Pieces.Count; i++)
                RaisePieceHashed (new PieceHashedEventArgs (this, i, bitfield[i]));

            this.hashChecked = true;
        }

        public FastResume SaveFastResume()
        {
            CheckMetadata();
            return new FastResume(this.torrent.infoHash, this.bitfield, new List<Peer>());
        }

        internal void UpdateState(TorrentState newState)
        {
            if (this.state == newState)
                return;

            TorrentStateChangedEventArgs e = new TorrentStateChangedEventArgs(this, this.state, newState);
            this.state = newState;

            RaiseTorrentStateChanged(e);
        }

        #endregion Private Methods

        internal void HandlePeerConnected(PeerId id, Direction direction)
        {
            // The only message sent/received so far is the Handshake message.
            // The current mode decides what additional messages need to be sent.
            mode.HandlePeerConnected(id, direction);
            RaisePeerConnected(new PeerConnectionEventArgs(this, id, direction));
        }
    }
}
