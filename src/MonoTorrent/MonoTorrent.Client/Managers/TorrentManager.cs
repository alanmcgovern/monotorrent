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
using MonoTorrent.Dht;

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
        private ClientEngine engine;            // The engine that this torrent is registered with
        private FileManager fileManager;        // Controls all reading/writing to/from the disk
        internal Queue<int> finishedPieces;     // The list of pieces which we should send "have" messages for
        private bool hashChecked;               // True if the manager has been hash checked
        private int hashFails;                  // The total number of pieces receieved which failed the hashcheck
        private ConnectionMonitor monitor;      // Calculates download/upload speed
        private PeerManager peers;              // Stores all the peers we know of in a list
        private PieceManager pieceManager;      // Tracks all the piece requests we've made and decides what pieces we can request off each peer
        internal RateLimiter uploadLimiter;        // Contains the logic to decide how many chunks we can download
        internal RateLimiter downloadLimiter;        // Contains the logic to decide how many chunks we can download
        private TorrentSettings settings;       // The settings for this torrent
        private DateTime startTime;             // The time at which the torrent was started at.
        private TorrentState state;             // The current state (seeding, downloading etc)
        private Torrent torrent;                // All the information from the physical torrent that was loaded
        private TrackerManager trackerManager;  // The class used to control all access to the tracker
        private int uploadingTo;                // The number of peers which we're currently uploading to
        private ChokeUnchokeManager chokeUnchoker; //???AGH Used to choke and unchoke peers
        private InitialSeed initialSeed;	//superseed class manager

        #endregion Member Variables


        #region Properties

        public BitField Bitfield
        {
            get { return this.bitfield; }
        }

        public bool CanUseDht
        {
            get { return !torrent.IsPrivate && settings.UseDht; }
        }

        public bool Complete
        {
            get { return this.bitfield.AllTrue; }
        }

        public ClientEngine Engine
        {
            get { return this.engine; }
            internal set { this.engine = value; }
        }

        public FileManager FileManager
        {
            get { return this.fileManager; }
        }

        public int PeerReviewRoundsComplete
        {
            get
            {
                if (this.chokeUnchoker != null)
                    return this.chokeUnchoker.ReviewsExecuted;
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
            get { return this.fileManager.SavePath; }
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
        }


        /// <summary>
        /// The number of peers that we are currently uploading to
        /// </summary>
        public int UploadingTo
        {
            get { return this.uploadingTo; }
            internal set { this.uploadingTo = value; }
        }

        internal InitialSeed InitialSeed
        {
            get { 
                if (initialSeed == null)
                    this.initialSeed = new InitialSeed (this);
                return this.initialSeed;
            }
        }

        public bool IsInitialSeeding
        {
            get
            {
                return settings.InitialSeedingEnabled
                    && state == TorrentState.Seeding
                    && ClientEngine.SupportsInitialSeed;
            }
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
            : this(torrent, savePath, settings, torrent.Files.Length == 1 ? "" : torrent.Name, null)
        {

        }

        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings, FastResume fastResumeData)
            : this(torrent, savePath, settings, torrent.Files.Length == 1 ? "" : torrent.Name, fastResumeData)
        {

        }

        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings, string baseDirectory)
            : this(torrent, savePath, settings, baseDirectory, null)
        {
        }
        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        /// <param name="settings">The settings to use for controlling connections</param>
        /// <param name="baseDirectory">In the case of a multi-file torrent, the name of the base directory containing the files. Defaults to Torrent.Name</param>
        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings, string baseDirectory, FastResume fastResumeData)
        {
            Check.Torrent(torrent);
            Check.SavePath(savePath);
            Check.Settings(settings);
            Check.BaseDirectory(baseDirectory);

            this.bitfield = new BitField(torrent.Pieces.Count);
            this.fileManager = new FileManager(this, torrent.Files, torrent.PieceLength, savePath, baseDirectory);
            this.finishedPieces = new Queue<int>();
            this.hashingWaitHandle = new ManualResetEvent(false);
            this.monitor = new ConnectionMonitor();
            this.settings = settings;
            this.peers = new PeerManager();
            this.pieceManager = new PieceManager(bitfield, torrent.Files);
            this.torrent = torrent;
            this.trackerManager = new TrackerManager(this);
            this.downloadLimiter = new RateLimiter();
            this.uploadLimiter = new RateLimiter();

            if (fastResumeData != null)
                LoadFastResume(fastResumeData);

            if (ClientEngine.SupportsInitialSeed)
                this.initialSeed = (settings.InitialSeedingEnabled ? (new InitialSeed(this)) : null);
        }


        #endregion


        #region Public Methods

        public void ChangePicker(PiecePickerBase picker)
        {
            Check.Picker(picker);

            ClientEngine.MainLoop.QueueWait(delegate {
                this.pieceManager.ChangePicker(picker, torrent.Files);
            });
        }
        public void Dispose()
        {
            pieceManager.Dispose();
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
            return (other == null) ? false : this.torrent.Equals(other.torrent);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Toolbox.HashCode(this.torrent.infoHash);
        }


        /// <summary>
        /// Starts a hashcheck. If forceFullScan is false, the library will attempt to load fastresume data
        /// before performing a full scan, otherwise fast resume data will be ignored and a full scan will be started
        /// </summary>
        /// <param name="forceFullScan">True if a full hash check should be performed ignoring fast resume data</param>
        public void HashCheck(bool autoStart)
        {
            ClientEngine.MainLoop.QueueWait(delegate {
                if (this.state != TorrentState.Stopped)
                    throw new TorrentException(string.Format("A hashcheck can only be performed when the manager is stopped. State is: {0}", state));

                CheckRegistered();
                this.startTime = DateTime.Now;
                UpdateState(TorrentState.Hashing);
                ThreadPool.QueueUserWorkItem(delegate { PerformHashCheck(autoStart); }); 
            });
        }


        /// <summary>
        /// Pauses the TorrentManager
        /// </summary>
        public void Pause()
        {
            ClientEngine.MainLoop.QueueWait(delegate
            {
                CheckRegistered();
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
            ClientEngine.MainLoop.QueueWait(delegate {
                CheckRegistered();

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

                if (this.Complete)
                    UpdateState(TorrentState.Seeding);
                else
                    UpdateState(TorrentState.Downloading);

                if (TrackerManager.CurrentTracker != null)
                {
                    if (this.trackerManager.CurrentTracker.CanScrape)
                        this.TrackerManager.Scrape();
                    this.trackerManager.Announce(TorrentEvent.Started); // Tell server we're starting
                }

                if (!torrent.IsPrivate)
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
                this.startTime = DateTime.Now;
                if (engine.ConnectionManager.IsRegistered(this))
                    Logger.Log(null, "TorrentManager - Error, this manager is already in the connectionmanager!");
                else
                    engine.ConnectionManager.RegisterManager(this);
                this.pieceManager.Reset();

                ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromSeconds(2), delegate {
                    if (State != TorrentState.Downloading && State != TorrentState.Seeding)
                        return false;
                    pieceManager.PiecePicker.CancelTimedOutRequests();
                    return true;
                });
            });
        }


        /// <summary>
        /// Stops the TorrentManager
        /// </summary>
        public WaitHandle Stop()
        {
            return (WaitHandle)ClientEngine.MainLoop.QueueWait(delegate {
                CheckRegistered();

                engine.DhtEngine.PeersFound -= DhtPeersFound;
                ManagerWaitHandle handle = new ManagerWaitHandle("Global");
                try
                {
                    if (this.state == TorrentState.Stopped)
                        return handle;

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

                    handle.AddHandle(engine.DiskManager.CloseFileStreams(FileManager.SavePath, Torrent.Files), "DiskManager");

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

        internal void PreLogicTick(int counter)
        {
            PeerId id;

            engine.ConnectionManager.TryConnect();

            //Execute iniitial logic for individual peers
            if (counter % (1000 / ClientEngine.TickLength) == 0)     // Call it every second... ish
                this.monitor.Tick();

            if (this.finishedPieces.Count > 0)
                SendHaveMessagesToAll();

            for (int i = 0; i < this.Peers.ConnectedPeers.Count; i++)
            {
                id = this.Peers.ConnectedPeers[i];
                if (id.Connection == null)
                    continue;

                if (counter % (1000 / ClientEngine.TickLength) == 0)     // Call it every second... ish
                    id.Monitor.Tick();

            }
        }

        internal void PostLogicTick(int counter)
        {
            PeerId id;
            DateTime nowTime = DateTime.Now;
            DateTime thirtySecondsAgo = nowTime.AddSeconds(-50);
            DateTime nintySecondsAgo = nowTime.AddSeconds(-90);
            DateTime onhundredAndEightySecondsAgo = nowTime.AddSeconds(-180);

            for (int i = 0; i < this.Peers.ConnectedPeers.Count; i++)
            {
                id = this.Peers.ConnectedPeers[i];
                if (id.Connection == null)
                    continue;

                if (Peers.ConnectedPeers[i].QueueLength > 0 && !Peers.ConnectedPeers[i].ProcessingQueue)
                {
                    Peers.ConnectedPeers[i].ProcessingQueue = true;
                    MessageHandler.EnqueueSend(Peers.ConnectedPeers[i]);
                }

                if (nintySecondsAgo > id.LastMessageSent)
                {
                    id.LastMessageSent = DateTime.Now;
                    id.Enqueue(new KeepAliveMessage());
                }

                if (onhundredAndEightySecondsAgo > id.LastMessageReceived)
                {
                    engine.ConnectionManager.CleanupSocket(id, true, "Inactivity");
                    continue;
                }

                if (thirtySecondsAgo > id.LastMessageReceived && id.AmRequestingPiecesCount > 0)
                {
                    engine.ConnectionManager.CleanupSocket(id, true, "Didn't send pieces");
                    continue;
                }
            }

            if (counter % 100 == 0 && (state == TorrentState.Seeding || state == TorrentState.Downloading))
            {
                // If the last connection succeeded, then update at the regular interval
                if (this.trackerManager.UpdateSucceeded)
                {
                    if (DateTime.Now > (this.trackerManager.LastUpdated.AddSeconds(this.trackerManager.TrackerTiers[0].Trackers[0].UpdateInterval)))
                    {
                        this.trackerManager.Announce(TorrentEvent.None);
                    }
                }
                // Otherwise update at the min interval
                else if (DateTime.Now > (this.trackerManager.LastUpdated.AddSeconds(this.trackerManager.TrackerTiers[0].Trackers[0].MinUpdateInterval)))
                {
                    this.trackerManager.Announce(TorrentEvent.None);
                }
            }

            if (counter % (1000 / ClientEngine.TickLength) == 0)
            {
                this.downloadLimiter.UpdateChunks(settings.MaxDownloadSpeed, monitor.DownloadSpeed);
                this.uploadLimiter.UpdateChunks(settings.MaxUploadSpeed, monitor.UploadSpeed);
            }
        }

        internal void DownloadLogic(int counter)
        {
            //???AGH if download is complete, set state to 'Seeding'
            if (this.Progress == 100.0 && this.State != TorrentState.Seeding)
                UpdateState(TorrentState.Seeding);

            //Now choke/unchoke peers; first instantiate the choke/unchoke manager if we haven't done so already
            if (chokeUnchoker == null)
                chokeUnchoker = new ChokeUnchokeManager(this, this.Settings.MinimumTimeBetweenReviews, this.Settings.PercentOfMaxRateToSkipReview);

            // FIXME: Hardcoded 15kB/sec - is this ok?
            if ((DateTime.Now - startTime) > TimeSpan.FromMinutes(1) && Monitor.DownloadSpeed < 15 * 1024)
            {
                foreach (string s in this.torrent.GetRightHttpSeeds)
                {
                    Uri uri = new Uri(s);
                    Peer peer = new Peer(new string('0', 20), uri);
                    PeerId id = new PeerId(peer, this);
                    HttpConnection connection = new HttpConnection(new Uri(s));
                    connection.Manager = this;
                    peer.IsSeeder = true;
                    id.BitField.SetAll(true);
                    id.Encryptor = new PlainTextEncryption();
                    id.Decryptor = new PlainTextEncryption();
                    id.IsChoking = false;
                    id.Connection = connection;
                    peers.ConnectedPeers.Add(id);
                    engine.ConnectionManager.ReceiveMessage(id);
                }

                // FIXME: In future, don't clear out this list. It may be useful to keep the list of HTTP seeds
                // Add a boolean or something so that we don't add them twice.
                torrent.GetRightHttpSeeds.Clear();
            }
            chokeUnchoker.TimePassed();
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

        internal void SeedingLogic(int counter)
        {
            //Choke/unchoke peers; first instantiate the choke/unchoke manager if we haven't done so already
            if (chokeUnchoker == null)
                chokeUnchoker = new ChokeUnchokeManager(this, this.Settings.MinimumTimeBetweenReviews, this.Settings.PercentOfMaxRateToSkipReview);

            chokeUnchoker.TimePassed();
        }

        internal void SetAmInterestedStatus(PeerId id, bool interesting)
        {
            if (interesting && !id.AmInterested)
            {
                id.AmInterested = true;
                id.Enqueue(new InterestedMessage());

                // He's interesting, so attempt to queue up any FastPieces (if that's possible)
                while (id.TorrentManager.pieceManager.AddPieceRequest(id)) { }
            }
            else if (!interesting && id.AmInterested)
            {
                id.AmInterested = false;
                id.Enqueue(new NotInterestedMessage());
            }
        }

        #endregion Internal Methods


        #region Private Methods

        private void CheckRegistered()
        {
            if (engine == null)
                throw new TorrentException("This manager has not been registed with an Engine");
        }

        private void DhtPeersFound(object o, PeersFoundEventArgs e)
        {
            if (!Toolbox.ByteMatch(torrent.InfoHash, e.InfoHash))
                return;

            int count = AddPeers(e.Peers);
            RaisePeersFound(new DhtPeersAdded(this, count));
        }

        private void PerformHashCheck(bool autoStart)
        {
            try
            {
                // Store the value for whether the streams are open or not
                // If they are initially closed, we need to close them again after we hashcheck

                // We only need to hashcheck if at least one file already exists on the disk
                bool filesExist = fileManager.CheckFilesExist();

                // A hashcheck should only be performed if some/all of the files exist on disk
                if (filesExist)
                {
                    for (int i = 0; i < this.torrent.Pieces.Count; i++)
                    {
                        bitfield[i] = this.torrent.Pieces.IsValid(this.fileManager.GetHash(i, true), i);
                        RaisePieceHashed(new PieceHashedEventArgs(this, i, bitfield[i]));

                        // This happens if the user cancels the hash by stopping the torrent.
                        if (abortHashing)
                            return;
                    }
                }
                else
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
                engine.DiskManager.Writer.Close(SavePath, this.torrent.Files);

                if (abortHashing)
                {
                    abortHashing = false;
                    this.hashingWaitHandle.Set();
                }
            }
        }

        private void LoadFastResume(FastResume fastResumeData)
        {
            if (fastResumeData == null)
                throw new ArgumentNullException ("fastResumeData");
            if (!Toolbox.ByteMatch(torrent.infoHash, fastResumeData.InfoHash) || torrent.Pieces.Count != fastResumeData.Bitfield.Length)
                throw new ArgumentException("The fast resume data does not match this torrent", "fastResumeData");

            for (int i = 0; i < this.bitfield.Length; i++)
                this.bitfield[i] = fastResumeData.Bitfield[i];

            for (int i = 0; i < torrent.Pieces.Count; i++)
                RaisePieceHashed (new PieceHashedEventArgs (this, i, bitfield[i]));

            this.hashChecked = true;
        }

        public FastResume SaveFastResume()
        {
            return new FastResume(this.torrent.infoHash, this.bitfield, new List<Peer>());
        }

        private void SendHaveMessagesToAll()
        {
            // This is "Have Suppression" as defined in the spec.
            List<int> pieces;
            lock (finishedPieces)
            {
                pieces = new List<int>(finishedPieces);
                finishedPieces.Clear();
            }

            for (int i = 0; i < this.Peers.ConnectedPeers.Count; i++)
            {
                if (this.Peers.ConnectedPeers[i].Connection == null)
                    continue;

                MessageBundle bundle = new MessageBundle();

                foreach (int pieceIndex in pieces)
                {
                    // If the peer has the piece already, we need to recalculate his "interesting" status.
                    bool hasPiece = this.Peers.ConnectedPeers[i].BitField[pieceIndex];
                    if (hasPiece)
                    {
                        bool isInteresting = this.pieceManager.IsInteresting(this.Peers.ConnectedPeers[i]);
                        SetAmInterestedStatus(this.Peers.ConnectedPeers[i], isInteresting);
                    }

                    // Check to see if have supression is enabled and send the have message accordingly
                    if (!hasPiece || (hasPiece && !this.engine.Settings.HaveSupressionEnabled))
                        bundle.Messages.Add(new HaveMessage(pieceIndex));
                }

                this.Peers.ConnectedPeers[i].Enqueue(bundle);
            }
        }

        private void UpdateState(TorrentState newState)
        {
            if (this.state == newState)
                return;

            TorrentStateChangedEventArgs e = new TorrentStateChangedEventArgs(this, this.state, newState);
            this.state = newState;

            RaiseTorrentStateChanged(e);

        }

        #endregion Private Methods
    }
}
