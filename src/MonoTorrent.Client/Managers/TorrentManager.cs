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

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class TorrentManager : IDisposable
    {
        #region Events
        /// <summary>
        /// Event that's fired every time new peers are added from a tracker update
        /// </summary>
        public event EventHandler<PeersAddedEventArgs> OnPeersAdded;


        /// <summary>
        /// Event that's fired every time a piece is hashed
        /// </summary>
        public event EventHandler<PieceHashedEventArgs> OnPieceHashed;


        /// <summary>
        /// Event that's fired every time the TorrentManagers state changes
        /// </summary>
        public event EventHandler<TorrentStateChangedEventArgs> OnTorrentStateChanged;
        #endregion


        #region Member Variables
        //internal Queue<PeerConnectionID> downloadQueue;
        //internal Queue<PeerConnectionID> uploadQueue;


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
        internal PieceManager PieceManager
        {
            get { return this.pieceManager; }
            //set { this.pieceManager = (PieceManager) value; }
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


        /// <summary>
        /// The list of peers that are available to be connected to
        /// </summary>
        internal Peers Available
        {
            get { return this.available; }
        }
        private Peers available;


        /// <summary>
        /// The list of peers that we are currently connected to
        /// </summary>
        internal Peers ConnectedPeers
        {
            get { return this.connectedPeers; }
        }
        private Peers connectedPeers;


        /// <summary>
        /// The list of peers that we are currently trying to connect to
        /// </summary>
        internal Peers ConnectingTo
        {
            get { return this.connectingTo; }
        }
        private Peers connectingTo;


        /// <summary>
        /// The number of peers that this torrent instance is connected to
        /// </summary>
        public int OpenConnections
        {
            get { return this.connectedPeers.Count; }
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
        /// The number of bytes which have been downloaded for the BitTorrent protocol
        /// </summary>
        public long ProtocolBytesDownloaded
        {
            get { return this.protocolBytesDownloaded; }
            internal set { this.protocolBytesDownloaded = value; }
        }
        private long protocolBytesDownloaded;


        /// <summary>
        /// The number of bytes which have been uploaded for the BitTorrent protocol
        /// </summary>
        public long ProtocolBytesUploaded
        {
            get { return this.protocolBytesUploaded; }
            internal set { this.protocolBytesUploaded = value; }
        }
        private long protocolBytesUploaded;


        /// <summary>
        /// The number of bytes which have been downloaded for the files
        /// </summary>
        public long DataBytesDownloaded
        {
            get { return this.dataBytesDownloaded; }
            internal set { this.dataBytesDownloaded = value; }
        }
        private long dataBytesDownloaded;


        /// <summary>
        /// The number of bytes which have been uploaded for the files
        /// </summary>
        public long DataBytesUploaded
        {
            get { return this.dataBytesUploaded; }
            internal set { this.dataBytesUploaded = value; }
        }
        private long dataBytesUploaded;


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
        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings)
        {
            this.torrent = torrent;
            this.settings = settings;

            this.trackerManager = new TrackerManager(this);

            this.connectedPeers = new Peers(16);
            this.available = new Peers(16);
            this.uploadQueue = new Queue<PeerConnectionID>(16);
            this.downloadQueue = new Queue<PeerConnectionID>(16);
            this.connectingTo = new Peers(ClientEngine.ConnectionManager.MaxHalfOpenConnections);

            this.savePath = savePath;

            if (string.IsNullOrEmpty(savePath))
                throw new TorrentException("Torrent savepath cannot be null");

            this.fileManager = new FileManager(this.torrent.Files, this.torrent.Name, this.savePath, this.torrent.PieceLength, System.IO.FileAccess.ReadWrite);
            this.pieceManager = new PieceManager(new BitField(this.torrent.Pieces.Length), (TorrentFile[])this.torrent.Files);
        }
        #endregion


        #region Start/Stop/Pause
        /// <summary>
        /// Starts the TorrentManager
        /// </summary>
        internal void Start()
        {
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

            if (this.Progress() == 100.0)
                UpdateState(TorrentState.Seeding);
            else
                UpdateState(TorrentState.Downloading);

            this.trackerManager.Announce(0, 0, (long)((1.0 - this.Progress() / 100.0) * this.torrent.Size), TorrentEvent.Started); // Tell server we're starting
        }


        /// <summary>
        /// Stops the TorrentManager
        /// </summary>
        internal WaitHandle Stop()
        {
            WaitHandle handle;

            UpdateState(TorrentState.Stopped);

            this.fileManager.FlushAll();
            handle = this.trackerManager.Announce(this.dataBytesDownloaded, this.dataBytesUploaded, (long)((1.0 - this.Progress() / 100.0) * this.torrent.Size), TorrentEvent.Stopped);
            lock (this.listLock)
            {
                while (this.connectingTo.Count > 0)
                    lock (this.connectingTo[0])
                        ClientEngine.ConnectionManager.CleanupSocket(this.connectingTo[0]);

                while (this.connectedPeers.Count > 0)
                    lock (this.connectedPeers[0])
                        ClientEngine.ConnectionManager.CleanupSocket(this.connectedPeers[0]);
            }

            this.SaveFastResume();
            this.connectedPeers = new Peers();
            this.available = new Peers();
            this.connectingTo = new Peers();

            return handle;
        }


        /// <summary>
        /// Pauses the TorrentManager
        /// </summary>
        internal void Pause()
        {
            TorrentStateChangedEventArgs args;
            lock (this.listLock)
            {
                UpdateState(TorrentState.Paused);

                for (int i = 0; i < this.connectingTo.Count; i++)
                    lock (this.connectingTo[i])
                        ClientEngine.ConnectionManager.CleanupSocket(this.connectingTo[i]);

                for (int i = 0; i < this.connectedPeers.Count; i++)
                    lock (this.connectedPeers[i])
                        ClientEngine.ConnectionManager.CleanupSocket(this.connectedPeers[i]);

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
            IPeerMessageInternal msg;
            PeerConnectionID id;

            lock (this.listLock)
                if (this.downloadQueue.Count > 0 || this.uploadQueue.Count > 0)
                    this.ResumePeers();

            lock (this.listLock)
            {
                // If we havn't reached our max connected peers, connect to another one.
                if ((this.available.Count > 0) && (this.connectedPeers.Count < this.settings.MaxConnections))
                    ClientEngine.ConnectionManager.ConnectToPeer(this);

                for (int i = 0; i < this.connectedPeers.Count; i++)
                {
                    id = this.connectedPeers[i];
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                            continue;

                        if (counter % 40 == 0)     // Call it every second... ish
                            id.Peer.Connection.Monitor.TimePeriodPassed();


                        if (id.Peer.Connection.IsInterestingToMe && (!id.Peer.Connection.AmInterested))
                        {
                            // If we used to be not interested but now we are, send a message.
                            id.Peer.Connection.AmInterested = true;
                            id.Peer.Connection.EnQueue(new InterestedMessage());
                        }

                        else if (!id.Peer.Connection.IsInterestingToMe && id.Peer.Connection.AmInterested)
                        {
                            // If we used to be interested but now we're not, send a message
                            // We only become uninterested once we've Received all our requested bits.
                            id.Peer.Connection.AmInterested = false;
                            id.Peer.Connection.EnQueue(new NotInterestedMessage());
                        }

                        if (id.Peer.Connection.AmChoking && id.Peer.Connection.IsInterested && this.uploadingTo < this.settings.UploadSlots)
                        {
                            this.uploadingTo++;
                            id.Peer.Connection.AmChoking = false;
                            id.Peer.Connection.EnQueue(new UnchokeMessage());
                            Debug.WriteLine("UnChoking: " + this.uploadingTo);
                        }

                        if (id.Peer.Connection.PiecesSent > 50)  // Send 50 blocks before moving on
                        {
                            for (int j = 0; j < id.Peer.Connection.QueueLength; j++)
                            {
                                msg = id.Peer.Connection.DeQueue();
                                if (msg is PieceMessage && id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer)
                                    id.Peer.Connection.EnQueue(new RejectRequestMessage((PieceMessage)msg));
                                else
                                    id.Peer.Connection.EnQueue(msg);
                            }

                            this.uploadingTo--;
                            id.Peer.Connection.PiecesSent = 0;
                            id.Peer.Connection.AmChoking = true;
                            id.Peer.Connection.IsRequestingPiecesCount = 0;
                            id.Peer.Connection.EnQueue(new ChokeMessage());
                            Debug.WriteLine("ReChoking: " + this.uploadingTo);
                        }

                        while (!id.Peer.Connection.IsChoking && id.Peer.Connection.AmRequestingPiecesCount < 6 && id.Peer.Connection.AmInterested)
                        {
                            msg = this.pieceManager.PickPiece(id, this.connectedPeers);
                            if (msg == null)
                                break;

                            id.Peer.Connection.EnQueue(msg);
                            id.Peer.Connection.AmRequestingPiecesCount++;
                        }

                        if (!(id.Peer.Connection.ProcessingQueue) && id.Peer.Connection.QueueLength > 0)
                            ClientEngine.ConnectionManager.ProcessQueue(id);
                    }
                }

                if (counter % 100 == 0)
                {
                    if(this.Progress() == 100.0)
                        UpdateState(TorrentState.Seeding);
                    // If the last connection succeeded, then update at the regular interval
                    if (this.trackerManager.UpdateSucceeded)
                    {
                        if (DateTime.Now > (this.trackerManager.LastUpdated.AddSeconds(this.trackerManager.CurrentTracker.UpdateInterval)))
                        {
                            this.trackerManager.Announce(this.dataBytesDownloaded, this.dataBytesUploaded, (long)((1.0 - this.Progress() / 100.0) * this.torrent.Size), TorrentEvent.None);
                        }
                    }
                    // Otherwise update at the min interval
                    else if (DateTime.Now > (this.trackerManager.LastUpdated.AddSeconds(this.trackerManager.CurrentTracker.MinUpdateInterval)))
                    {
                        this.trackerManager.Announce(this.dataBytesDownloaded, this.dataBytesUploaded, (long)((1.0 - this.Progress() / 100.0) * this.torrent.Size), TorrentEvent.None);
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
            lock (this.listLock)
            {
                if (this.available.Contains(peer) || this.connectedPeers.Contains(peer) || this.connectingTo.Contains(peer))
                    return 0;

                this.available.Add(peer);
                return 1;
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
                    System.Diagnostics.Trace.WriteLine(ex.ToString());
                }
            }

            if (this.OnPeersAdded != null)
                this.OnPeersAdded(this, new PeersAddedEventArgs(added));
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

            if (this.OnPeersAdded != null)
                this.OnPeersAdded(this, new PeersAddedEventArgs(added));
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
            if (this.OnPieceHashed != null)
                this.OnPieceHashed(this, pieceHashedEventArgs);
        }


        private void UpdateState(TorrentState newState)
        {
            if (this.state == newState)
                return;

            TorrentStateChangedEventArgs e = new TorrentStateChangedEventArgs(this.state, newState);
            this.state = newState;

            if (this.OnTorrentStateChanged != null)
                this.OnTorrentStateChanged(this, e);
        }

        #endregion


        #region Rate limiting
        internal Queue<PeerConnectionID> downloadQueue;
        internal Queue<PeerConnectionID> uploadQueue;


        /// <summary>
        /// Restarts peers which have been suspended from downloading/uploading due to rate limiting
        /// </summary>
        /// <param name="downloading"></param>
        internal void ResumePeers()
        {
            while (this.downloadQueue.Count > 0 && ((this.rateLimiter.DownloadChunks > 0) || this.settings.MaxDownloadSpeed == 0))
                if (ClientEngine.ConnectionManager.ResumePeer(this.downloadQueue.Dequeue(), true) > ConnectionManager.ChunkLength / 2.0)
                    Interlocked.Decrement(ref this.rateLimiter.DownloadChunks);

            while (this.uploadQueue.Count > 0 && ((this.rateLimiter.UploadChunks > 0)|| this.settings.MaxUploadSpeed == 0))
                if (ClientEngine.ConnectionManager.ResumePeer(this.uploadQueue.Dequeue(), false) > ConnectionManager.ChunkLength / 2.0)
                    Interlocked.Decrement(ref this.rateLimiter.UploadChunks);
            //Console.WriteLine("Upload: " + this.rateLimiter.UploadChunks);
        }
        #endregion


        #region Misc
        /// <summary>
        /// Returns the number of Seeds we are currently connected to
        /// </summary>
        /// <returns></returns>
        public int Seeds()
        {
            int seeds = 0;
            lock (this.listLock)
                foreach (PeerConnectionID id in this.connectedPeers)
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
                foreach (PeerConnectionID id in this.connectedPeers)
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
            get { return this.available.Count + this.connectedPeers.Count + this.connectingTo.Count; }
        }


        /// <summary>
        /// The current download speed in bytes per second
        /// </summary>
        /// <returns></returns>
        public double DownloadSpeed()
        {
            double total = 0;
            lock (this.listLock)
                for (int i = 0; i < this.connectedPeers.Count; i++)
                    lock (this.connectedPeers[i])
                        if (this.connectedPeers[i].Peer.Connection != null)
                            total += this.connectedPeers[i].Peer.Connection.Monitor.DownloadSpeed;

            return total;
        }


        /// <summary>
        /// The current upload speed in bytes per second
        /// </summary>
        /// <returns></returns>
        public double UploadSpeed()
        {
            double total = 0;

            lock (this.listLock)
                for (int i = 0; i < this.connectedPeers.Count; i++)
                    lock (this.connectedPeers[i])
                        if (this.connectedPeers[i].Peer.Connection != null)
                            total += this.connectedPeers[i].Peer.Connection.Monitor.UploadSpeed;

            return total;
        }


        /// <summary>
        /// Saves data to allow fastresumes to the disk
        /// </summary>
        private void SaveFastResume()
        {
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

            for (int i = 0; i < manager.torrent.Pieces.Length; i++)
            {
                result = ToolBox.ByteMatch(manager.torrent.Pieces[i], manager.fileManager.GetHash(i));
                lock (manager.pieceManager.MyBitField)
                    manager.pieceManager.MyBitField[i] = result;

                if (manager.OnPieceHashed != null)
                    manager.OnPieceHashed(this, new PieceHashedEventArgs(i, result));
            }

            manager.hashChecked = true;

#warning Don't *always* start the torrent in the future.
            if (manager.state == TorrentState.Stopped || (manager.state == TorrentState.Paused) || manager.state == TorrentState.Hashing)
                manager.Start();
        }


        /// <summary>
        /// The current progress of the torrent in percent
        /// </summary>
        public double Progress()
        {
            return ((this.pieceManager.MyBitField.TrueCount * 100.0) / this.pieceManager.MyBitField.Length);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        internal void PieceCompleted(int p)
        {
            // Only send a "have" message if the peer needs the piece.
            // This is "Have Suppression" as defined in the spec.

            lock (this.listLock)
                for (int i = 0; i < this.connectedPeers.Count; i++)
                    lock (this.connectedPeers[i])
                        if (this.connectedPeers[i].Peer.Connection != null)
                            if (!this.connectedPeers[i].Peer.Connection.BitField[p])
                                this.connectedPeers[i].Peer.Connection.EnQueue(new HaveMessage(p));
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
        #endregion
    }
}
