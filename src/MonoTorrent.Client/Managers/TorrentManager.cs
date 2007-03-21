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
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;
using System.Threading;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class TorrentManager : IDisposable, IEquatable<TorrentManager>
    {
        internal Queue<int> finishedPieces = new Queue<int>();
        #region Events
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
        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }
        private ConnectionMonitor monitor;

        internal BitField Bitfield
        {
            get { return this.bitfield; }
        }
        private BitField bitfield;


        /// <summary>
        /// The Torrent contained within this TorrentManager
        /// </summary>
        public Torrent Torrent
        {
            get { return this.torrent; }
        }
        private Torrent torrent;


        /// <summary>
        /// The settings for with this TorrentManager
        /// </summary>
        public TorrentSettings Settings
        {
            get { return this.settings; }
        }
        private TorrentSettings settings;


        /// <summary>
        /// The current state of the TorrentManager
        /// </summary>
        public TorrentState State
        {
            get { return this.state; }
        }
        private TorrentState state;


        /// <summary>
        /// The tracker connection associated with this TorrentManager
        /// </summary>
        public TrackerManager TrackerManager
        {
            get { return this.trackerManager; }
        }
        private TrackerManager trackerManager;


        /// <summary>
        /// The piecemanager for this TorrentManager
        /// </summary>
        public PieceManager PieceManager
        {
            get { return this.pieceManager; }
        }
        private PieceManager pieceManager;


        /// <summary>
        /// The DiskManager associated with this torrent
        /// </summary>
        internal FileManager FileManager
        {
            get { return this.fileManager; }
        }
        private FileManager fileManager;


        /// <summary>
        /// Contains the logic to decide how many chunks we can download
        /// </summary>
        private RateLimiter rateLimiter;


        /// <summary>
        /// The object we use to syncronize list access
        /// </summary>
        internal object listLock = new object();


        internal PeerList Peers
        {
            get { return this.peers; }
        }
        private PeerList peers;


        /// <summary>
        /// The number of peers that this torrent instance is connected to
        /// </summary>
        public int OpenConnections
        {
            get { return this.peers.ConnectedPeers.Count; }
        }


        /// <summary>
        /// True if this file has been hashchecked
        /// </summary>
        public bool HashChecked
        {
            get { return this.hashChecked; }
            internal set { this.hashChecked = value; }
        }
        private bool hashChecked;


        /// <summary>
        /// The number of times we recieved a piece that failed the hashcheck
        /// </summary>
        public int HashFails
        {
            get { return this.hashFails; }
        }
        private int hashFails;


        /// <summary>
        /// The directory to download the files to
        /// </summary>
        public string SavePath
        {
            get { return this.savePath; }
            set { this.savePath = value; }
        }
        private string savePath;


        /// <summary>
        /// The number of peers that we are currently uploading to
        /// </summary>
        public int UploadingTo
        {
            get { return this.uploadingTo; }
            internal set { this.uploadingTo = value; }
        }
        private int uploadingTo;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        /// <param name="settings">The settings to use for controlling connections</param>
        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings, EngineSettings engineSettings)
        {
            this.torrent = torrent;
            this.settings = settings;

            this.trackerManager = new TrackerManager(this, engineSettings);

            this.peers = new PeerList();

            this.savePath = savePath;

            if (string.IsNullOrEmpty(savePath))
                throw new TorrentException("Torrent savepath cannot be null");

            this.fileManager = new FileManager(this.torrent.Files, this.torrent.Name, this.savePath, this.torrent.PieceLength, System.IO.FileAccess.ReadWrite);

            this.bitfield = new BitField(this.torrent.Pieces.Count);
            this.pieceManager = new PieceManager(this.bitfield, (TorrentFile[])this.torrent.Files);
            this.monitor = new ConnectionMonitor();
        }
        #endregion


        #region Start/Stop/Pause
        /// <summary>
        /// Starts the TorrentManager
        /// </summary>
        internal void Start()
        {
            if(!this.fileManager.StreamsOpen)
                this.FileManager.OpenFileStreams(FileAccess.ReadWrite);

            if (this.fileManager.InitialHashRequired)
            {
                if (!this.hashChecked && !(this.state == TorrentState.Hashing))
                {
                    UpdateState(TorrentState.Hashing);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(HashCheck), this);
                    return;
                }

                else if (!this.hashChecked)
                {
                    return;
                }
            }

            this.fileManager.InitialHashRequired = false;
            if (this.state == TorrentState.Seeding || this.state == TorrentState.SuperSeeding || this.state == TorrentState.Downloading)
                throw new TorrentException("Torrent is already running");

            if (this.Progress == 100.0)
                UpdateState(TorrentState.Seeding);
            else
                UpdateState(TorrentState.Downloading);

            this.trackerManager.Announce(0, 0, (long)((1.0 - this.Progress / 100.0) * this.torrent.Size), TorrentEvent.Started); // Tell server we're starting
            ClientEngine.ConnectionManager.RegisterManager(this);
        }


        /// <summary>
        /// Stops the TorrentManager
        /// </summary>
        internal WaitHandle Stop()
        {
            WaitHandle handle;

            UpdateState(TorrentState.Stopped);

            handle = this.trackerManager.Announce(this.monitor.DataBytesDownloaded, this.monitor.DataBytesUploaded, (long)((1.0 - this.Progress / 100.0) * this.torrent.Size), TorrentEvent.Stopped);
            lock (this.listLock)
            {
                while (this.peers.ConnectingToPeers.Count > 0)
                    lock (this.peers.ConnectingToPeers[0])
                        ClientEngine.ConnectionManager.CleanupSocket(this.peers.ConnectingToPeers[0]);

                while (this.peers.ConnectedPeers.Count > 0)
                    lock (this.peers.ConnectedPeers[0])
                        ClientEngine.ConnectionManager.CleanupSocket(this.peers.ConnectedPeers[0]);
            }

            if(this.fileManager.StreamsOpen)
                this.FileManager.CloseFileStreams();
            this.SaveFastResume();
            this.peers.ClearAll();
            ClientEngine.ConnectionManager.UnregisterManager(this);

            return handle;
        }


        /// <summary>
        /// Pauses the TorrentManager
        /// </summary>
        internal void Pause()
        {
            lock (this.listLock)
            {
                UpdateState(TorrentState.Paused);

                for (int i = 0; i < this.peers.ConnectingToPeers.Count; i++)
                    lock (this.peers.ConnectingToPeers[i])
                        ClientEngine.ConnectionManager.CleanupSocket(this.peers.ConnectingToPeers[i]);

                for (int i = 0; i < this.peers.ConnectedPeers.Count; i++)
                    lock (this.peers.ConnectedPeers[i])
                        ClientEngine.ConnectionManager.CleanupSocket(this.peers.ConnectedPeers[i]);

                this.SaveFastResume();
            }
        }
        #endregion


        #region Downloading/Seeding/SuperSeeding

        /// <summary>
        /// 
        /// </summary>
        /// <param name="counter"></param>
        internal void DownloadLogic(int counter)
        {
            PeerConnectionID id;

            // First attempt to resume downloading (just in case we've stalled for whatever reason)
            lock (this.listLock)
                if (this.peers.DownloadQueue.Count > 0 || this.peers.UploadQueue.Count > 0)
                    this.ResumePeers();

            DateTime nowTime = DateTime.Now;
            DateTime nintySecondsAgo = nowTime.AddSeconds(-90);
            DateTime onhundredAndTwentySecondsAgo = nowTime.AddSeconds(-120);

            lock (this.listLock)
            {
                if (counter % (1000 / ClientEngine.TickLength) == 0)     // Call it every second... ish
                    this.monitor.TimePeriodPassed();

                while (this.finishedPieces.Count > 0)
                    this.SendHaveMessageToAll(this.finishedPieces.Dequeue());

                for (int i = 0; i < this.peers.ConnectedPeers.Count; i++)
                {
                    id = this.peers.ConnectedPeers[i];
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                            continue;

                        if (counter % (1000 / ClientEngine.TickLength) == 0)     // Call it every second... ish
                            id.Peer.Connection.Monitor.TimePeriodPassed();

                        //if (counter % 500 == 0)
                        //    DumpStats(id, counter);

                        // If the peer is interesting to me and i havent sent an Interested message
                        SetAmInterestedStatus(id);


                        // If he is not interested and i am not choking him
                        if (!id.Peer.Connection.IsInterested && !id.Peer.Connection.AmChoking)
                            SetChokeStatus(id, true);

                        // If i am choking the peer, and he is interested in downloading from us, and i haven't reached my maximum upload slots
                        if (id.Peer.Connection.AmChoking && id.Peer.Connection.IsInterested && this.uploadingTo < this.settings.UploadSlots)
                            SetChokeStatus(id, false);

                        // If i have sent 50 pieces to the peer, choke him to let someone else download
                        if (id.Peer.Connection.PiecesSent > 50)
                            SetChokeStatus(id, true);

                        while ((!id.Peer.Connection.IsChoking || id.Peer.Connection.IsAllowedFastPieces.Count > 0)
                                && id.Peer.Connection.AmRequestingPiecesCount < PieceManager.MaxRequests && id.Peer.Connection.AmInterested)
                        {
                            // If there are no more pieces to add, AddPieceRequest will return null
                            if (!AddPieceRequest(id))
                                break;
                        }
                        if (nintySecondsAgo > id.Peer.Connection.LastMessageSent)
                        {
                            id.Peer.Connection.LastMessageSent = DateTime.Now;
                            id.Peer.Connection.EnQueue(new KeepAliveMessage());
                        }

                        if (onhundredAndTwentySecondsAgo > id.Peer.Connection.LastMessageReceived)
                        {
                            ClientEngine.ConnectionManager.CleanupSocket(id);
                            continue;
                        }

                        if (!(id.Peer.Connection.ProcessingQueue) && id.Peer.Connection.QueueLength > 0)
                            ClientEngine.ConnectionManager.ProcessQueue(id);
                    }
                }

                if (counter % 100 == 0)
                {
                    if (this.Progress == 100.0 && this.state != TorrentState.Seeding)
                    {
                        //this.Stop();
                        //this.Start();
                        //this.hashChecked = false;
                        //this.fileManager.InitialHashRequired = true;
                        UpdateState(TorrentState.Seeding);
                    }
                    // If the last connection succeeded, then update at the regular interval
                    if (this.trackerManager.UpdateSucceeded)
                    {
                        if (DateTime.Now > (this.trackerManager.LastUpdated.AddSeconds(this.trackerManager.CurrentTracker.UpdateInterval)))
                        {
                            this.trackerManager.Announce(this.monitor.DataBytesDownloaded, this.monitor.DataBytesUploaded, (long)((1.0 - this.Progress / 100.0) * this.torrent.Size), TorrentEvent.None);
                        }
                    }
                    // Otherwise update at the min interval
                    else if (DateTime.Now > (this.trackerManager.LastUpdated.AddSeconds(this.trackerManager.CurrentTracker.MinUpdateInterval)))
                    {
                        this.trackerManager.Announce(this.monitor.DataBytesDownloaded, this.monitor.DataBytesUploaded, (long)((1.0 - this.Progress / 100.0) * this.torrent.Size), TorrentEvent.None);
                    }
                }
                if (counter % 40 == 0)
                    this.rateLimiter.UpdateDownloadChunks((int)(this.settings.MaxDownloadSpeed * 1024 * 1.1),
                                                          (int)(this.settings.MaxUploadSpeed * 1024 * 1.1),
                                                          (int)(this.DownloadSpeed()),
                                                          (int)(this.UploadSpeed()));
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="counter"></param>
        internal void SeedingLogic(int counter)
        {
            DownloadLogic(counter);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="counter"></param>
        internal void SuperSeedingLogic(int counter)
        {
            SeedingLogic(counter);     // Initially just seed as per normal. This could be a V2.0 feature.
        }
        #endregion


        #region AddPeers methods
        /// <summary>
        /// Adds an individual peer to the list
        /// </summary>
        /// <param name="peer">The peer to add</param>
        /// <returns>The number of peers added</returns>
        internal int AddPeers(PeerConnectionID peer)
        {
            try
            {
                lock (this.listLock)
                {
                    if (this.peers.AvailablePeers.Contains(peer) || this.peers.ConnectedPeers.Contains(peer) || this.peers.ConnectingToPeers.Contains(peer))
                        return 0;

                    this.peers.AvailablePeers.Add(peer);

                    // When we successfully add a peer we try to connect to the next available peer
                    return 1;
                }
            }
            finally
            {
                ClientEngine.ConnectionManager.TryConnect();
            }
        }


        /// <summary>
        /// Adds a non-compact tracker response of peers to the list
        /// </summary>
        /// <param name="list">The list of peers to add</param>
        /// <returns>The number of peers added</returns>
        internal int AddPeers(BEncodedList list)
        {
            PeerConnectionID id;
            int added = 0;
            foreach (BEncodedDictionary dict in list)
            {
                try
                {
                    string peerId;

                    if (dict.ContainsKey("peer id"))
                        peerId = dict["peer id"].ToString();
                    else if (dict.ContainsKey("peer_id"))       // HACK: Some trackers return "peer_id" instead of "peer id"
                        peerId = dict["peer_id"].ToString();
                    else
                        peerId = string.Empty;

                    id = new PeerConnectionID(new Peer(peerId, dict["ip"].ToString() + ':' + dict["port"].ToString()), this);
                    added += this.AddPeers(id);
                }
                catch (Exception ex)
                {
                    Logger.Log(null, ex.ToString());
                }
            }

            if (this.PeersFound != null)
                this.PeersFound(this, new PeersAddedEventArgs(added));
            return added;
        }


        /// <summary>
        /// Adds a compact tracker response of peers to the list
        /// </summary>
        /// <param name="byteOrderedData">The byte[] containing the peers to add</param>
        /// <returns>The number of peers added</returns>
        internal int AddPeers(byte[] byteOrderedData)
        {
            // "Compact Response" peers are encoded in network byte order. 
            // IP's are the first four bytes
            // Ports are the following 2 bytes

            int i = 0;
            int added = 0;
            UInt16 port;
            PeerConnectionID id;
            StringBuilder sb = new StringBuilder(16);

            while (i < byteOrderedData.Length)
            {
                sb.Remove(0, sb.Length);

                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);

                port = (UInt16)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(byteOrderedData, i));
                i += 2;
                sb.Append(':');
                sb.Append(port);
                id = new PeerConnectionID(new Peer(null, sb.ToString()), this);

                added += this.AddPeers(id);
            }

            if (this.PeersFound != null)
                this.PeersFound(this, new PeersAddedEventArgs(added));
            return added;
        }

        #endregion


        #region Code to call Events
        /// <summary>
        /// Called when a Piece has been hashed by the FileManager
        /// </summary>
        /// <param name="pieceHashedEventArgs">The event args for the event</param>
        internal void HashedPiece(PieceHashedEventArgs pieceHashedEventArgs)
        {
            if (!pieceHashedEventArgs.HashPassed)
                Interlocked.Increment(ref this.hashFails);

            if (this.PieceHashed != null)
                this.PieceHashed(this, pieceHashedEventArgs);
        }


        private void UpdateState(TorrentState newState)
        {
            if (this.state == newState)
                return;

            TorrentStateChangedEventArgs e = new TorrentStateChangedEventArgs(this.state, newState);
            this.state = newState;

            if (this.TorrentStateChanged != null)
                this.TorrentStateChanged(this, e);
        }

        #endregion


        #region Rate limiting

        /// <summary>
        /// Restarts peers which have been suspended from downloading/uploading due to rate limiting
        /// </summary>
        /// <param name="downloading"></param>
        internal void ResumePeers()
        {
            lock (this.listLock)
            {
                // While there are peers queued in the list and i haven't used my download allowance, resume downloading
                // from that peer. Don't resume if there are more than 20 queued writes in the download queue.
                while (this.peers.DownloadQueue.Count > 0 && ((this.rateLimiter.DownloadChunks > 0) || this.settings.MaxDownloadSpeed == 0))
                    if (this.fileManager.QueuedWrites < 20)
                        if (ClientEngine.ConnectionManager.ResumePeer(this.peers.Dequeue(PeerType.DownloadQueue), true) > ConnectionManager.ChunkLength / 2.0)
                            Interlocked.Decrement(ref this.rateLimiter.DownloadChunks);

                while (this.peers.UploadQueue.Count > 0 && ((this.rateLimiter.UploadChunks > 0) || this.settings.MaxUploadSpeed == 0))
                    if (ClientEngine.ConnectionManager.ResumePeer(this.peers.Dequeue(PeerType.UploadQueue), false) > ConnectionManager.ChunkLength / 2.0)
                        Interlocked.Decrement(ref this.rateLimiter.UploadChunks);
            }
        }
        #endregion



        internal void SetAmInterestedStatus(PeerConnectionID id)
        {
            if (id.Peer.Connection.IsInterestingToMe && (!id.Peer.Connection.AmInterested))
                SetAmInterestedStatus(id, true);

            else if (!id.Peer.Connection.IsInterestingToMe && id.Peer.Connection.AmInterested)
                SetAmInterestedStatus(id, false);
        }

        private void DumpStats(PeerConnectionID id, int counter)
        {
            //string path = Path.Combine(@"C:\Docs\" + counter.ToString(), id.Peer.Location.GetHashCode() + ".txt");
            //if(!Directory.Exists(Path.GetDirectoryName(path)))
            //    Directory.CreateDirectory(Path.GetDirectoryName(path));
            //using (FileStream stream = File.Create(path))
            //    stream.Write(System.Text.UTF8Encoding.UTF8.GetBytes(id.Peer.MessageHistory.ToString()),0, System.Text.UTF8Encoding.UTF8.GetByteCount(id.Peer.MessageHistory.ToString()));
        }


        /// <summary>
        /// Returns the number of Seeds we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Seeds()
        {
            int seeds = 0;
            lock (this.listLock)
                foreach (PeerConnectionID id in this.peers.ConnectedPeers)
                    lock (id)
                        if (id.Peer.IsSeeder)
                            seeds++;
            return seeds;
        }


        /// <summary>
        /// Returns the number of Leechs we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Leechs()
        {
            int leechs = 0;
            lock (this.listLock)
                foreach (PeerConnectionID id in this.peers.ConnectedPeers)
                    lock (id)
                        if (!id.Peer.IsSeeder)
                            leechs++;

            return leechs;
        }


        /// <summary>
        /// Returns the total number of peers available (including ones already connected to)
        /// </summary>
        public int AvailablePeers
        {
            get { return this.peers.AvailablePeers.Count + this.peers.ConnectedPeers.Count + this.peers.ConnectingToPeers.Count; }
        }


        /// <summary>
        /// The current download speed in bytes per second
        /// </summary>
        /// <returns></returns>
        public double DownloadSpeed()
        {
            return this.monitor.DownloadSpeed;
        }


        /// <summary>
        /// The current upload speed in bytes per second
        /// </summary>
        /// <returns></returns>
        public double UploadSpeed()
        {
            double total = 0;

            lock (this.listLock)
                for (int i = 0; i < this.peers.ConnectedPeers.Count; i++)
                    lock (this.peers.ConnectedPeers[i])
                        if (this.peers.ConnectedPeers[i].Peer.Connection != null)
                            total += this.peers.ConnectedPeers[i].Peer.Connection.Monitor.UploadSpeed;

            return total;
        }


        /// <summary>
        /// Saves data to allow fastresumes to the disk
        /// </summary>
        private void SaveFastResume()
        {
            // Do not create fast-resume data if we do not support it for this TorrentManager object
            if (!Settings.FastResumeEnabled)
                return;

            XmlSerializer fastResume = new XmlSerializer(typeof(int[]));

            using (FileStream file = File.Open(this.torrent.TorrentPath + ".fresume", FileMode.Create))
                fastResume.Serialize(file, this.pieceManager.MyBitField.Array);
        }


        /// <summary>
        /// Hash checks the supplied torrent
        /// </summary>
        /// <param name="state">The TorrentManager to hashcheck</param>
        private void HashCheck(object state)
        {
            bool result;
            TorrentManager manager = state as TorrentManager;

            if (manager == null)
                return;

            for (int i = 0; i < manager.torrent.Pieces.Count; i++)
            {
                result = manager.torrent.Pieces.IsValid(manager.fileManager.GetHash(i), i);
                lock (manager.pieceManager.MyBitField)
                    manager.pieceManager.MyBitField[i] = result;

                if (manager.PieceHashed != null)
                    manager.PieceHashed(this, new PieceHashedEventArgs(i, result));
            }

            manager.hashChecked = true;

#warning Don't *always* start the torrent in the future.
            if (manager.state == TorrentState.Stopped || (manager.state == TorrentState.Paused) || manager.state == TorrentState.Hashing)
                manager.Start();
        }


        /// <summary>
        /// The current progress of the torrent in percent
        /// </summary>
        public double Progress
        {
            get { return (this.bitfield.PercentComplete); }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        private void SendHaveMessageToAll(int pieceIndex)
        {
            // This is "Have Suppression" as defined in the spec.

            lock (this.listLock)
                for (int i = 0; i < this.peers.ConnectedPeers.Count; i++)
                    lock (this.peers.ConnectedPeers[i])
                        if (this.peers.ConnectedPeers[i].Peer.Connection != null)
                        {
                            // If the peer has the piece already, we need to recalculate his "interesting" status.
                            if (this.peers.ConnectedPeers[i].Peer.Connection.BitField[pieceIndex])
                            {
                                this.peers.ConnectedPeers[i].Peer.Connection.IsInterestingToMe = this.pieceManager.IsInteresting(this.peers.ConnectedPeers[i]);
                                SetAmInterestedStatus(this.peers.ConnectedPeers[i]);
                            }

                            // Have supression is disabled
                            // If the peer does not have the piece, then we send them a have message so they can request it off me
                            //else
                                this.peers.ConnectedPeers[i].Peer.Connection.EnQueue(new HaveMessage(pieceIndex));
                        }
        }


        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        public void Dispose(bool disposing)
        {
            this.fileManager.Dispose();
        }

        public override bool Equals(object obj)
        {
            TorrentManager m = obj as TorrentManager;
            return (m == null) ? false : this.Equals(m);
        }

        public bool Equals(TorrentManager other)
        {
            return (other == null) ? false : BitConverter.ToString(this.torrent.InfoHash) == BitConverter.ToString(other.torrent.InfoHash);
        }

        public override int GetHashCode()
        {
            return BitConverter.ToString(this.torrent.InfoHash).GetHashCode();
        }

        private void SetChokeStatus(PeerConnectionID id, bool amChoking)
        {
            id.Peer.Connection.PiecesSent = 0;
            if (id.Peer.Connection.AmChoking == amChoking)
                return;

            id.Peer.Connection.AmChoking = amChoking;
            if (amChoking)
            {
                Interlocked.Decrement(ref this.uploadingTo);
                RejectPendingRequests(id);
                id.Peer.Connection.EnQueueAt(new ChokeMessage(), 0);
               Logger.Log("Choking: " + this.uploadingTo);
            }
            else
            {
                Interlocked.Increment(ref this.uploadingTo);
                id.Peer.Connection.EnQueue(new UnchokeMessage());
              Logger.Log("UnChoking: " + this.uploadingTo);
            }
        }

        /// <summary>
        /// Tries to add a piece request to the peers message queue.
        /// </summary>
        /// <param name="id">The peer to add the request too</param>
        /// <returns>True if the request was added</returns>
        internal bool AddPieceRequest(PeerConnectionID id)
        {
            IPeerMessageInternal msg;

            if (id.Peer.Connection.AmRequestingPiecesCount >= PieceManager.MaxRequests)
                return false;

            if (pieceManager.InEndGameMode)// In endgame we only want to queue 2 pieces
                if (id.Peer.Connection.AmRequestingPiecesCount > PieceManager.MaxEndGameRequests)
                    return false;

            msg = this.pieceManager.PickPiece(id, this.peers.ConnectedPeers);
            if (msg == null)
                return false;

            id.Peer.Connection.EnQueue(msg);
            id.Peer.Connection.AmRequestingPiecesCount++;
            return true;
        }

        /// <summary>
        /// Changes the peers "Interesting" status to the new value
        /// </summary>
        /// <param name="id">The peer to change the status of</param>
        /// <param name="amInterested">True if we are interested in the peer, false otherwise</param>
        private void SetAmInterestedStatus(PeerConnectionID id, bool amInterested)
        {
            Console.WriteLine(id.ToString() + ": " + amInterested.ToString());
            // If we used to be not interested but now we are, send a message.
            // If we used to be interested but now we're not, send a message
            id.Peer.Connection.AmInterested = amInterested;

            if (amInterested)
                id.Peer.Connection.EnQueue(new InterestedMessage());
            else
                id.Peer.Connection.EnQueue(new NotInterestedMessage());
        }

        /// <summary>
        /// Checks the sendbuffer of the peer to see if there are any outstanding pieces which they requested
        /// and rejects them as necessary
        /// </summary>
        /// <param name="id"></param>
        private void RejectPendingRequests(PeerConnectionID id)
        {
            IPeerMessageInternal message;
            PieceMessage pieceMessage;
            int length = id.Peer.Connection.QueueLength;

            for (int i = 0; i < length; i++)
            {
                message = id.Peer.Connection.DeQueue();
                if (!(message is PieceMessage))
                {
                    id.Peer.Connection.EnQueue(message);
                    continue;
                }

                pieceMessage = (PieceMessage)message;
                
                // If the peer doesn't support fast peer, then we will never requeue the message
                if (!(id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer))
                {
                    id.Peer.Connection.IsRequestingPiecesCount--;
                    continue;
                }

                // If the peer supports fast peer, queue the message if it is an AllowedFast piece
                // Otherwise send a reject message for the piece
                if (id.Peer.Connection.AmAllowedFastPieces.Contains((uint)pieceMessage.PieceIndex))
                    id.Peer.Connection.EnQueue(pieceMessage);
                else
                {
                    id.Peer.Connection.IsRequestingPiecesCount--;
                    id.Peer.Connection.EnQueue(new RejectRequestMessage(pieceMessage));
                }
            }
        }
    }
}
