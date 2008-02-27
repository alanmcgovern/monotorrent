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

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class TorrentManager : IDisposable, IEquatable<TorrentManager>
    {
        internal MonoTorrentCollection<PeerIdInternal> downloadQueue = new MonoTorrentCollection<PeerIdInternal>();
        internal MonoTorrentCollection<PeerIdInternal> uploadQueue = new MonoTorrentCollection<PeerIdInternal>();
        internal List<PeerIdInternal> ConnectedPeers;
        internal List<PeerIdInternal> ConnectingToPeers;

        #region Events

        
        public event EventHandler<PeerConnectionEventArgs> PeerConnected;


        public event EventHandler<PeerConnectionEventArgs> PeerDisconnected;
        /// <summary>
        /// Event that's fired every time new peers are added from a tracker update
        /// </summary>
        public event EventHandler<PeersAddedEventArgs> PeersFound;

        /// <summary>
        /// Event that's fired every time a piece is hashed
        /// </summary>
        public event EventHandler<PieceHashedEventArgs> PieceHashed;

        /// <summary>
        /// Event that's fired every time the TorrentManagers state changes
        /// </summary>
        public event EventHandler<TorrentStateChangedEventArgs> TorrentStateChanged;

        #endregion


        #region Member Variables

        private BitField bitfield;              // The bitfield representing the pieces we've downloaded and have to download
        private ClientEngine engine;            // The engine that this torrent is registered with
        private FileManager fileManager;        // Controls all reading/writing to/from the disk
        internal Queue<int> finishedPieces;     // The list of pieces which we should send "have" messages for
        private bool hashChecked;               // True if the manager has been hash checked
        private int hashFails;                  // The total number of pieces receieved which failed the hashcheck
        internal object listLock;               // The object we use to syncronize list access
        private ConnectionMonitor monitor;      // Calculates download/upload speed
        private PeerManager peers;              // Stores all the peers we know of in a list
        private PieceManager pieceManager;      // Tracks all the piece requests we've made and decides what pieces we can request off each peer
        private RateLimiter rateLimiter;        // Contains the logic to decide how many chunks we can download
        private TorrentSettings settings;       // The settings for this torrent
        private DateTime startTime;             // The time at which the torrent was started at.
        private TorrentState state;             // The current state (seeding, downloading etc)
        private Torrent torrent;                // All the information from the physical torrent that was loaded
        private TrackerManager trackerManager;  // The class used to control all access to the tracker
        private int uploadingTo;                // The number of peers which we're currently uploading to
        private ChokeUnchokeManager chokeUnchoker; //???AGH Used to choke and unchoke peers

        #endregion Member Variables


        #region Properties

        internal BitField Bitfield
        {
            get { return this.bitfield; }
        }


        public bool Complete
        {
            get { return this.bitfield.AllTrue; }
        }


        internal ClientEngine Engine
        {
            get { return this.engine; }
            set { this.engine = value; }
        }


        /// <summary>
        /// The DiskManager associated with this torrent
        /// </summary>
        public FileManager FileManager
        {
            get { return this.fileManager; }
        }


        /// <summary>
        /// True if this file has been hashchecked
        /// </summary>
        public bool HashChecked
        {
            get { return this.hashChecked; }
            internal set { this.hashChecked = value; }
        }


        /// <summary>
        /// The number of times we recieved a piece that failed the hashcheck
        /// </summary>
        public int HashFails
        {
            get { return this.hashFails; }
        }


        /// <summary>
        /// Records statistics such as Download speed, Upload speed and amount of data uploaded/downloaded
        /// </summary>
        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }


        /// <summary>
        /// The number of peers that this torrent instance is connected to
        /// </summary>
        public int OpenConnections
        {
            get { return this.ConnectedPeers.Count; }
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
            if (torrent == null)
                throw new ArgumentNullException("torrent");

            if (savePath == null)
                throw new ArgumentNullException("savePath");

            if (settings == null)
                throw new ArgumentNullException("settings");

            this.bitfield = new BitField(torrent.Pieces.Count);
            this.ConnectedPeers = new List<PeerIdInternal>();
            this.ConnectingToPeers = new List<PeerIdInternal>();
            this.fileManager = new FileManager(this, torrent.Files, torrent.PieceLength, savePath, baseDirectory);
            this.finishedPieces = new Queue<int>();
            this.listLock = new object();
            this.monitor = new ConnectionMonitor();
            this.settings = settings;
            this.peers = new PeerManager(engine, this);
            this.pieceManager = new PieceManager(bitfield, torrent.Files);
            this.torrent = torrent;
            this.trackerManager = new TrackerManager(this);

            if (fastResumeData != null)
                LoadFastResume(fastResumeData);
        }


        #endregion


        #region Public Methods

        public void Dispose()
        {
            // Do nothing?
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
            return (other == null) ? false : Toolbox.ByteMatch(this.torrent.infoHash, other.torrent.infoHash);
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
        public void HashCheck(bool forceFullScan)
        {
            lock (this.engine.asyncCompletionLock)
                HashCheck(forceFullScan, false);
        }


        /// <summary>
        /// Starts a hashcheck. If forceFullScan is false, the library will attempt to load fastresume data
        /// before performing a full scan, otherwise fast resume data will be ignored and a full scan will be started
        /// </summary>
        /// <param name="forceFullScan">True if a full hash check should be performed ignoring fast resume data</param>
        /// <param name="autoStart">True if the manager should start downloading immediately after hash checking is complete</param>
        internal void HashCheck(bool forceFullScan, bool autoStart)
        {
            if (this.state != TorrentState.Stopped)
                throw new TorrentException("A hashcheck can only be performed when the manager is stopped");

            this.startTime = DateTime.Now;
            UpdateState(TorrentState.Hashing);
            ThreadPool.QueueUserWorkItem(new WaitCallback(PerformHashCheck), new bool[] { forceFullScan, autoStart });
        }


        /// <summary>
        /// Pauses the TorrentManager
        /// </summary>
        public void Pause()
        {
            lock (this.engine.asyncCompletionLock)
                lock (this.listLock)
                {
                    if (state != TorrentState.Downloading && state != TorrentState.Seeding)
                        return;

                    // By setting the state to "paused", peers will not be dequeued from the either the
                    // sending or receiving queues, so no traffic will be allowed.
                    UpdateState(TorrentState.Paused);
                    this.SaveFastResume();
                }
        }


        /// <summary>
        /// Starts the TorrentManager
        /// </summary>
        public void Start()
        {
            this.engine.Start();
            lock (this.engine.asyncCompletionLock)
            {
                // If the torrent was "paused", then just update the state to Downloading and forcefully
                // make sure the peers begin sending/receiving again
                if (this.state == TorrentState.Paused)
                {
                    UpdateState(TorrentState.Downloading);
                    lock (this.listLock)
                        this.ResumePeers();
                    return;
                }

                // If the torrent has not been hashed, we start the hashing process then we wait for it to finish
                // before attempting to start again
                if (!hashChecked)
                {
                    if (state != TorrentState.Hashing)
                        HashCheck(false, true);
                    return;
                }

                if (this.state == TorrentState.Seeding || this.state == TorrentState.Downloading)
                    return;

                if (TrackerManager.CurrentTracker != null && TrackerManager.CurrentTracker.CanScrape)
                    this.TrackerManager.Scrape();

                this.trackerManager.Announce(TorrentEvent.Started); // Tell server we're starting
                this.startTime = DateTime.Now;

                if (this.Progress == 100.0)
                    UpdateState(TorrentState.Seeding);
                else
                    UpdateState(TorrentState.Downloading);

                engine.ConnectionManager.RegisterManager(this);
                this.pieceManager.Reset();
            }
        }


        /// <summary>
        /// Stops the TorrentManager
        /// </summary>
        public WaitHandle Stop()
        {
            WaitHandle handle;
            lock (this.engine.asyncCompletionLock)
            {
                if (this.state == TorrentState.Stopped)
                    return new ManualResetEvent(true);

                UpdateState(TorrentState.Stopped);

                handle = this.trackerManager.Announce(TorrentEvent.Stopped);
                lock (this.listLock)
                {
                    while (this.ConnectingToPeers.Count > 0)
                        lock (this.ConnectingToPeers[0])
                        {
                            if (this.ConnectingToPeers[0].Connection == null)
                                this.ConnectingToPeers.RemoveAt(0);
                            else
                                engine.ConnectionManager.AsyncCleanupSocket(this.ConnectingToPeers[0], true, "Called stop");
                        }

                    while (this.ConnectedPeers.Count > 0)
                        lock (this.ConnectedPeers[0])
                        {
                            if (this.ConnectedPeers[0].Connection == null)
                                this.ConnectedPeers.RemoveAt(0);
                            else
                                engine.ConnectionManager.AsyncCleanupSocket(this.ConnectedPeers[0], true, "Called stop");
                        }
                }

                engine.DiskManager.CloseFileStreams(this);

                if (this.hashChecked)
                    this.SaveFastResume();
                this.peers.ClearAll();
                this.monitor.Reset();
                this.pieceManager.Reset();
                if (this.engine.ConnectionManager.IsRegistered(this))
                    this.engine.ConnectionManager.UnregisterManager(this);
                this.engine.Stop();

                return handle;
            }
        }

        #endregion


        #region Internal Methods

        /// <summary>
        /// Adds an individual peer to the list
        /// </summary>
        /// <param name="peer">The peer to add</param>
        /// <returns>The number of peers added</returns>
        internal int AddPeers(Peer peer)
        {
            try
            {
                lock (this.listLock)
                {
                    if (this.peers.Contains(peer))
                        return 0;

                    this.peers.AvailablePeers.Add(peer);

                    // When we successfully add a peer we try to connect to the next available peer
                    return 1;
                }
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
            PeerIdInternal id;

            // First attempt to resume downloading (just in case we've stalled for whatever reason)
            lock (this.listLock)
                if (this.downloadQueue.Count > 0 || this.uploadQueue.Count > 0)
                    this.ResumePeers();

            engine.ConnectionManager.TryConnect();

            //Execute iniitial logic for individual peers
            lock (this.listLock)
            {
                if (counter % (1000 / ClientEngine.TickLength) == 0)     // Call it every second... ish
                    this.monitor.TimePeriodPassed();

                if (this.finishedPieces.Count > 0 || (this.finishedPieces.Count > 0 && state == TorrentState.Seeding))
                    SendHaveMessagesToAll();

                for (int i = 0; i < this.ConnectedPeers.Count; i++)
                {
                    id = this.ConnectedPeers[i];
                    lock (id)
                    {
                        if (id.Connection == null)
                        {
                            Console.WriteLine("Nulled out: " + id.Peer.ConnectionUri.ToString());
                            continue;
                        }

                        id.UpdatePublicStats();

                        if (counter % (1000 / ClientEngine.TickLength) == 0)     // Call it every second... ish
                            id.Connection.Monitor.TimePeriodPassed();

                    }
                }
            }
        }

        internal void PostLogicTick(int counter)
        {
            PeerIdInternal id;
            DateTime nowTime = DateTime.Now;
            DateTime thirtySecondsAgo = nowTime.AddSeconds(-50);
            DateTime nintySecondsAgo = nowTime.AddSeconds(-90);
            DateTime onhundredAndEightySecondsAgo = nowTime.AddSeconds(-180);

            lock (this.listLock)
            {
                for (int i = 0; i < this.ConnectedPeers.Count; i++)
                {
                    id = this.ConnectedPeers[i];
                    lock (id)
                    {
                        if (id.Connection == null)
                            continue;

                        if (nintySecondsAgo > id.Connection.LastMessageSent)
                        {
                            id.Connection.LastMessageSent = DateTime.Now;
                            id.Connection.Enqueue(new KeepAliveMessage());
                        }

                        if (onhundredAndEightySecondsAgo > id.Connection.LastMessageReceived)
                        {
                            engine.ConnectionManager.CleanupSocket(id, true, "Inactivity");
                            continue;
                        }

                        if (thirtySecondsAgo > id.Connection.LastMessageReceived && id.Connection.AmRequestingPiecesCount > 0)
                        {
                            engine.ConnectionManager.CleanupSocket(id, true, "Didn't send pieces");
                            continue;
                        }

                        if (!id.Connection.ProcessingQueue && id.Connection.QueueLength > 0)
                        {
                            id.Connection.ProcessingQueue = true;
                            engine.ConnectionManager.MessageHandler.EnqueueSend(id);
                        }
                    }
                }
            }

            if (counter % 100 == 0)
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
                int activeCount;
                using(new ReaderLock(engine.torrentsLock))
                activeCount = Toolbox.Accumulate<TorrentManager>(engine.Torrents, delegate(TorrentManager m) {
                    return m.State == TorrentState.Downloading || m.state == TorrentState.Seeding ? 1 : 0;
                });

                int maxDownload = this.engine.Settings.GlobalMaxDownloadSpeed / activeCount;
                int maxUpload = this.engine.Settings.GlobalMaxUploadSpeed / activeCount;
                int currentDownload = this.engine.TotalDownloadSpeed / activeCount;
                int currentUpload = this.engine.TotalUploadSpeed / activeCount;

                if (maxDownload == 0)
                {
                    maxDownload = settings.MaxDownloadSpeed;
                    currentDownload = monitor.DownloadSpeed;
                }

                if (maxUpload == 0)
                {
                    maxUpload = settings.MaxUploadSpeed;
                    currentUpload = monitor.UploadSpeed;
                }

                this.rateLimiter.UpdateDownloadChunks((int)(maxDownload * 1.1),
                                                      (int)(maxUpload * 1.1),
                                                      currentDownload,
                                                      currentUpload);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="counter"></param>
        internal void DownloadLogic(int counter)
        {
            //???AGH if download is complete, set state to 'Seeding'
            if (this.Progress == 100.0 && this.State != TorrentState.Seeding)
                UpdateState(TorrentState.Seeding);

            //Now choke/unchoke peers; first instantiate the choke/unchoke manager if we haven't done so already
            if (chokeUnchoker == null)
                chokeUnchoker = new ChokeUnchokeManager(this, this.Settings.MinimumTimeBetweenReviews, this.Settings.PercentOfMaxRateToSkipReview);

            lock (listLock)
                chokeUnchoker.TimePassed();
        }


        /// <summary>
        /// Called when a Piece has been hashed by the FileManager
        /// </summary>
        /// <param name="pieceHashedEventArgs">The event args for the event</param>
        internal void HashedPiece(PieceHashedEventArgs pieceHashedEventArgs)
        {
            if (!pieceHashedEventArgs.HashPassed)
                Interlocked.Increment(ref this.hashFails);

            RaisePieceHashed(pieceHashedEventArgs);
        }
        
        internal void RaisePeerConnected(PeerConnectionEventArgs args)
        {
            ThreadPool.QueueUserWorkItem(delegate {
                EventHandler<PeerConnectionEventArgs> h = PeerConnected;
                if (h != null)
                    h (this, args);
            });
        }
        
        internal void RaisePeerDisconnected(PeerConnectionEventArgs args)
        {
            ThreadPool.QueueUserWorkItem(delegate {
                EventHandler<PeerConnectionEventArgs> h = PeerDisconnected;
                if (h != null)
                    h (this, args);
            });
        }

        internal void RaisePeersFound(PeersAddedEventArgs args)
        {
            ThreadPool.QueueUserWorkItem(delegate {
                EventHandler<PeersAddedEventArgs> h = PeersFound;
                if (h != null)
                    h (this, args);
            });
        }

        internal void RaisePieceHashed(PieceHashedEventArgs pieceHashedEventArgs)
        {
            int index = pieceHashedEventArgs.PieceIndex;
            TorrentFile[] files = this.torrent.Files;
            
            for (int i = 0; i < files.Length; i++)
                if (index >= files[i].StartPieceIndex && index <= files[i].EndPieceIndex)
                    files[i].BitField[index - files[i].StartPieceIndex] = pieceHashedEventArgs.HashPassed;
            
            ThreadPool.QueueUserWorkItem(delegate {
                EventHandler<PieceHashedEventArgs> h = PieceHashed;
                if (h != null)
                    h (this, pieceHashedEventArgs);
            });
        }

        internal void RaiseTorrentStateChanged(TorrentStateChangedEventArgs e)
        {
            engine.DiskManager.Flush(this);
            ThreadPool.QueueUserWorkItem(delegate {
                EventHandler<TorrentStateChangedEventArgs> h = TorrentStateChanged;
                if (h != null)
                    h (this, e);
            });
        }

        /// <summary>
        /// Restarts peers which have been suspended from downloading/uploading due to rate limiting
        /// </summary>
        /// <param name="downloading"></param>
        internal void ResumePeers()
        {
            if (this.state == TorrentState.Paused)
                return;

            // While there are peers queued in the list and i haven't used my download allowance, resume downloading
            // from that peer. Don't resume if there are more than 20 queued writes in the download queue.
            int downloadSpeed = this.engine.Settings.GlobalMaxDownloadSpeed;
            int uploadSpeed = this.engine.Settings.GlobalMaxUploadSpeed;

            if (downloadSpeed == 0)
                downloadSpeed = settings.MaxDownloadSpeed;
            if (uploadSpeed == 0)
                uploadSpeed = settings.MaxUploadSpeed;

            while (this.downloadQueue.Count > 0 &&
                    this.engine.DiskManager.QueuedWrites < 20 &&
                    ((this.rateLimiter.DownloadChunks > 0) || downloadSpeed == 0))
            {
                if (engine.ConnectionManager.ResumePeer(this.downloadQueue.Dequeue(), true) > ConnectionManager.ChunkLength / 2.0)
                    Interlocked.Decrement(ref this.rateLimiter.DownloadChunks);
            }
            while (this.uploadQueue.Count > 0 && ((this.rateLimiter.UploadChunks > 0) || uploadSpeed == 0))
                if (engine.ConnectionManager.ResumePeer(this.uploadQueue.Dequeue(), false) > ConnectionManager.ChunkLength / 2.0)
                    Interlocked.Decrement(ref this.rateLimiter.UploadChunks);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="counter"></param>
        internal void SeedingLogic(int counter)
        {
            //Choke/unchoke peers; first instantiate the choke/unchoke manager if we haven't done so already
            if (chokeUnchoker == null)
                chokeUnchoker = new ChokeUnchokeManager(this, this.Settings.MinimumTimeBetweenReviews, this.Settings.PercentOfMaxRateToSkipReview);

            lock (listLock)
                chokeUnchoker.TimePassed();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="counter"></param>
        internal void SuperSeedingLogic(int counter)
        {
            SeedingLogic(counter);     // Initially just seed as per normal. This could be a V2.0 feature.
        }

        #endregion Internal Methods


        #region Private Methods

        /// <summary>
        /// Hash checks the supplied torrent
        /// </summary>
        /// <param name="state">The TorrentManager to hashcheck</param>
        private void PerformHashCheck(object state)
        {
            int enterCount = 0;
            try
            {
                System.Threading.Monitor.Enter(this.engine.asyncCompletionLock);
                enterCount++;
                // Store the value for whether the streams are open or not
                // If they are initially closed, we need to close them again after we hashcheck
                bool forceCheck = ((bool[])state)[0];
                bool autoStart = ((bool[])state)[1];

                // We only need to hashcheck if at least one file already exists on the disk
                bool filesExist = fileManager.CheckFilesExist();

                // We only load fast resume if one (or more) of the files exist on disk.
                // FIXME: We now never load fastresume from the disk as it's passed into the constructor
                //bool loadedFastResume = filesExist && FileManager.LoadFastResume(this);
                bool loadedFastResume = filesExist && false;

                // If we are performing a forced scan OR we aren't forcing a full scan but can't load the fast resume data
                // perform a full scan.
                if (forceCheck || !loadedFastResume)
                {
                    for (int i = 0; i < this.torrent.Pieces.Count; i++)
                    {
                        bitfield[i] = this.torrent.Pieces.IsValid(this.fileManager.GetHash(i, true), i);
                        System.Threading.Monitor.Exit(this.engine.asyncCompletionLock);
                        enterCount--;
                        RaisePieceHashed(new PieceHashedEventArgs(this, i, bitfield[i]));
                        System.Threading.Monitor.Enter(this.engine.asyncCompletionLock);
                        enterCount++;

                        // This happens if the user cancels the hash by stopping the torrent.
                        if (State != TorrentState.Hashing)
                            return;
                    }
                }

                // If we loaded fastresume data then we need to fire the piece hashed events
                if (loadedFastResume)
                    for (int i = 0; i < bitfield.Length; i++)
                        RaisePieceHashed(new PieceHashedEventArgs(this, i, bitfield[i]));

                this.hashChecked = true;

                if (autoStart)
                    Start();
                else
                    UpdateState(TorrentState.Stopped);
            }
            finally
            {
                // Ensure file streams are all closed after hashing
                engine.DiskManager.Writer.CloseFileStreams(this);
                while (enterCount-- > 0)
                    System.Threading.Monitor.Exit(this.engine.asyncCompletionLock);
            }
        }


        ///// <summary>
        ///// Checks the send queue of the peer to see if there are any outstanding pieces which they requested
        ///// and rejects them as necessary
        ///// </summary>
        ///// <param name="id"></param>
        //private void RejectPendingRequests(PeerIdInternal id)
        //{
        //    PeerMessage message;
        //    PieceMessage pieceMessage;
        //    int length = id.Connection.QueueLength;

        //    for (int i = 0; i < length; i++)
        //    {
        //        message = id.Connection.Dequeue();
        //        if (!(message is PieceMessage))
        //        {
        //            id.Connection.Enqueue(message);
        //            continue;
        //        }

        //        pieceMessage = (PieceMessage)message;

        //        // If the peer doesn't support fast peer, then we will never requeue the message
        //        if (!(id.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer))
        //        {
        //            id.Connection.IsRequestingPiecesCount--;
        //            continue;
        //        }

        //        // If the peer supports fast peer, queue the message if it is an AllowedFast piece
        //        // Otherwise send a reject message for the piece
        //        if (id.Connection.AmAllowedFastPieces.Contains(pieceMessage.PieceIndex))
        //            id.Connection.Enqueue(pieceMessage);
        //        else
        //        {
        //            id.Connection.IsRequestingPiecesCount--;
        //            id.Connection.Enqueue(new RejectRequestMessage(pieceMessage));
        //        }
        //    }
        //}


        private void LoadFastResume(FastResume fastResumeData)
        {
            if (fastResumeData == null)
                throw new ArgumentNullException ("fastResumeData");
            if (!Toolbox.ByteMatch(torrent.infoHash, fastResumeData.InfoHash) || torrent.Pieces.Count != fastResumeData.Bitfield.Length)
                throw new ArgumentException("The fast resume data does not match this torrent", "fastResumeData");

            this.bitfield = fastResumeData.Bitfield;
            this.hashChecked = true;
        }


        /// <summary>
        /// Saves data to allow fastresumes to the disk
        /// </summary>
        private FastResume SaveFastResume()
        {
            // Do not create fast-resume data if we do not support it for this TorrentManager object
            if (!Settings.FastResumeEnabled || string.IsNullOrEmpty(this.torrent.TorrentPath))
                return null;

            return new FastResume(this.torrent.infoHash, this.bitfield, new List<Peer>());
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        private void SendHaveMessagesToAll()
        {
            // This is "Have Suppression" as defined in the spec.
            List<int> pieces;
            lock (finishedPieces)
            {
                pieces = new List<int>(finishedPieces);
                finishedPieces.Clear();
            }

            lock (this.listLock)
            {
                for (int i = 0; i < this.ConnectedPeers.Count; i++)
                {
                    lock (this.ConnectedPeers[i])
                    {
                        if (this.ConnectedPeers[i].Connection == null)
                            continue;

                        MessageBundle bundle = new MessageBundle();

                        foreach (int pieceIndex in pieces)
                        {
                            // If the peer has the piece already, we need to recalculate his "interesting" status.
                            bool hasPiece = this.ConnectedPeers[i].Connection.BitField[pieceIndex];
                            if (hasPiece)
                            {
                                bool isInteresting = this.pieceManager.IsInteresting(this.ConnectedPeers[i]);
                                SetAmInterestedStatus(this.ConnectedPeers[i], isInteresting);
                            }

                            // Check to see if have supression is enabled and send the have message accordingly
                            if (!hasPiece || (hasPiece && !this.engine.Settings.HaveSupressionEnabled))
                                bundle.Messages.Add(new HaveMessage(pieceIndex));
                        }

                        this.ConnectedPeers[i].Connection.Enqueue(bundle);
                    }
                }
            }
        }


        ///// <summary>
        ///// Sets the "AmChoking" status of the peer to the new value and enqueues the relevant peer message
        ///// </summary>
        ///// <param name="id">The peer to update the choke status for</param>
        ///// <param name="amChoking">The new status for "AmChoking"</param>
        //private void SetChokeStatus(PeerIdInternal id, bool amChoking)
        //{
        //    if (id.Connection.AmChoking == amChoking)
        //        return;

        //    id.Connection.PiecesSent = 0;
        //    id.Connection.AmChoking = amChoking;
        //    if (amChoking)
        //    {
        //        Interlocked.Decrement(ref this.uploadingTo);
        //        RejectPendingRequests(id);
        //        id.Connection.EnqueueAt(new ChokeMessage(), 0);
        //        Logger.Log("Choking: " + this.uploadingTo);
        //    }
        //    else
        //    {
        //        Interlocked.Increment(ref this.uploadingTo);
        //        id.Connection.Enqueue(new UnchokeMessage());
        //        Logger.Log("UnChoking: " + this.uploadingTo);
        //    }
        //}


        /// <summary>
        /// Fires the TorrentStateChanged event
        /// </summary>
        /// <param name="newState">The new state for the torrent manager</param>
        private void UpdateState(TorrentState newState)
        {
            if (this.state == newState)
                return;

            TorrentStateChangedEventArgs e = new TorrentStateChangedEventArgs(this, this.state, newState);
            this.state = newState;

            RaiseTorrentStateChanged(e);

        }

        #endregion Private Methods

        internal void SetAmInterestedStatus(PeerIdInternal id, bool interesting)
        {
            bool enqueued = false;
            if (interesting && !id.Connection.AmInterested)
            {
                id.Connection.AmInterested = true;
                id.Connection.Enqueue(new InterestedMessage());

                // He's interesting, so attempt to queue up any FastPieces (if that's possible)
                while (id.TorrentManager.pieceManager.AddPieceRequest(id)) { }
                enqueued = true;
            }
            else if (!interesting && id.Connection.AmInterested)
            {
                id.Connection.AmInterested = false;
                id.Connection.Enqueue(new NotInterestedMessage());
                enqueued = true;
            }

            if (enqueued && !id.Connection.ProcessingQueue)
            {
                id.Connection.ProcessingQueue = true;
                id.ConnectionManager.MessageHandler.EnqueueSend(id);
            }
        }
    }
}
