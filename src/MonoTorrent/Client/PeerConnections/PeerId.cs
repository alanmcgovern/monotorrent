//
// PeerConnectionId.cs
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




using System.Net.Sockets;
using System;
using MonoTorrent.Common;
using System.Diagnostics;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages.Libtorrent;
using System.Collections.Generic;
using MonoTorrent.Client.Messages.Standard;
namespace MonoTorrent.Client
{
    public class PeerId //: IComparable<PeerIdInternal>
    {
        #region Choke/Unchoke
        
        internal DateTime? LastUnchoked
        {
            get { return this.lastUnchoked; }
            set { this.lastUnchoked = value; }
        }

        internal long BytesDownloadedAtLastReview
        {
            get { return this.bytesDownloadedAtLastReview; }
            set { this.bytesDownloadedAtLastReview = value; }
        }

        internal long BytesUploadedAtLastReview
        {
            get { return this.bytesUploadedAtLastReview; }
            set { this.bytesUploadedAtLastReview = value; }
        }

        public IConnection Connection {
            get { return connection; }
            internal set { connection = value; }
        }

        internal double LastReviewDownloadRate
        {
            get { return this.lastReviewDownloadRate; }
            set { this.lastReviewDownloadRate = value; }
        }

        internal double LastReviewUploadRate
        {
            get { return this.lastReviewUploadRate; }
            set { this.lastReviewUploadRate = value; }
        }

		internal bool FirstReviewPeriod
		{
			get { return this.firstReviewPeriod; }
			set { this.firstReviewPeriod = value; }
		}

		internal DateTime LastBlockReceived
		{
			get { return this.lastBlockReceived; }
			set { this.lastBlockReceived = value; }
		}

        private DateTime? lastUnchoked = null;        //When this peer was last unchoked, or null if we haven't unchoked it yet
        private long bytesDownloadedAtLastReview = 0; //Number of bytes downloaded when this peer was last reviewed - allows us to determine number of bytes
        //downloaded during a review period
        private long bytesUploadedAtLastReview = 0;   //Ditto for uploaded bytes
        private double lastReviewDownloadRate = 0;    //Download rate determined at the end of the last full review period when this peer was unchoked
        private double lastReviewUploadRate = 0;      //Ditto for upload rate
        private bool firstReviewPeriod;               //Set true if this is the first review period since this peer was last unchoked
		private DateTime lastBlockReceived = DateTime.Now;

        #endregion

        #region Member Variables

        public List<PieceMessage> PieceReads = new List<PieceMessage>();

        private MonoTorrentCollection<int> amAllowedFastPieces;
        private bool amChoking;
        private bool amInterested;
        private int amRequestingPiecesCount;
        private BitField bitField;
        private Software clientApp;
        private IConnection connection;
        private PeerMessage currentlySendingMessage;
        private IEncryption decryptor;
        private string disconnectReason;
        private IEncryption encryptor;
        private ClientEngine engine;
        private MonoTorrentCollection<int> isAllowedFastPieces;
        private bool isChoking;
        private bool isInterested;
        private int isRequestingPiecesCount;
        private DateTime lastMessageReceived;
        private DateTime lastMessageSent;
		private DateTime whenConnected;
        private ExtensionSupports extensionSupports;
        private int maxPendingRequests;
        private int maxSupportedPendingRequests;
        private MessagingCallback messageReceivedCallback;
        private MessagingCallback messageSentCallback;
        private ConnectionMonitor monitor;
        private Peer peer;
        private PeerExchangeManager pexManager;
        private int piecesSent;
        private int piecesReceived;
        private ushort port;
        private bool processingQueue;
        private MonoTorrentCollection<PeerMessage> sendQueue;                  // This holds the peermessages waiting to be sent
        private MonoTorrentCollection<int> suggestedPieces;
        private bool supportsFastPeer;
        private bool supportsLTMessages;
        private TorrentManager torrentManager;

        #endregion Member Variables

        #region Properties

        internal byte[] AddressBytes
        {
            get { return Connection.AddressBytes; }
        }

        /// <summary>
        /// The remote peer can request these and we'll fulfill the request if we're choking them
        /// </summary>
        internal MonoTorrentCollection<int> AmAllowedFastPieces
        {
            get { return this.amAllowedFastPieces; }
            set { this.amAllowedFastPieces = value; }
        }

        public bool AmChoking
        {
            get { return this.amChoking; }
            internal set { this.amChoking = value; }
        }

        public bool AmInterested
        {
            get { return this.amInterested; }
            internal set { this.amInterested = value; }
        }

        public int AmRequestingPiecesCount
        {
            get { return this.amRequestingPiecesCount; }
            set { this.amRequestingPiecesCount = value; }
        }

        public BitField BitField
        {
            get { return this.bitField; }
            set { this.bitField = value; }
        }

        public Software ClientApp
        {
            get { return this.clientApp; }
            internal set { this.clientApp = value; }
        }

        internal ConnectionManager ConnectionManager
        {
            get { return this.engine.ConnectionManager; }
        }

        internal PeerMessage CurrentlySendingMessage
        {
            get { return this.currentlySendingMessage; }
            set { this.currentlySendingMessage = value; }
        }

        internal IEncryption Decryptor
        {
            get { return this.decryptor; }
            set { this.decryptor = value; }
        }

        internal string DisconnectReason
        {
            get { return this.disconnectReason; }
            set { this.disconnectReason = value; }
        }

        public IEncryption Encryptor
        {
            get { return this.encryptor; }
            set { this.encryptor = value; }
        }

        public ClientEngine Engine
        {
            get { return this.engine; ; }
        }

        internal ExtensionSupports ExtensionSupports
        {
            get { return extensionSupports; }
            set { extensionSupports = value; }
        }

        public int HashFails
        {
            get { return peer.TotalHashFails; }
        }
        
        internal MonoTorrentCollection<int> IsAllowedFastPieces
        {
            get { return this.isAllowedFastPieces; }
            set { this.isAllowedFastPieces = value; }
        }

        public bool IsChoking
        {
            get { return this.isChoking; }
            internal set { this.isChoking = value; }
        }

        public bool IsConnected
        {
            get { return Connection != null; }
        }
        
        public bool IsInterested
        {
            get { return this.isInterested; }
            internal set { this.isInterested = value; }
        }

        public bool IsSeeder
        {
            get { return bitField.AllTrue || peer.IsSeeder; }
        }
        
        public int IsRequestingPiecesCount
        {
            get { return this.isRequestingPiecesCount; }
            set { this.isRequestingPiecesCount = value; }
        }

        internal DateTime LastMessageReceived
        {
            get { return this.lastMessageReceived; }
            set { this.lastMessageReceived = value; }
        }

		internal DateTime LastMessageSent
		{
			get { return this.lastMessageSent; }
			set { this.lastMessageSent = value; }
		}

		internal DateTime WhenConnected
		{
			get { return this.whenConnected; }
			set { this.whenConnected = value; }
		}

        internal int MaxPendingRequests
        {
            get { return maxPendingRequests; }
            set { maxPendingRequests = value; }
        }

        internal int MaxSupportedPendingRequests
        {
            get { return maxSupportedPendingRequests; }
            set { maxSupportedPendingRequests = value; }
        }

        internal MessagingCallback MessageSentCallback
        {
            get { return this.messageSentCallback; }
            set { this.messageSentCallback = value; }
        }

        internal MessagingCallback MessageReceivedCallback
        {
            get { return this.messageReceivedCallback; }
            set { this.messageReceivedCallback = value; }
        }

        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }

        internal Peer Peer
        {
            get { return this.peer; }
            set { this.peer = value; }
        }

        internal PeerExchangeManager PeerExchangeManager
        {
            get { return this.pexManager; }
            set { this.pexManager = value; }
        }

        public string PeerID
        {
            get { return peer.PeerId; }
        }
        
        public int PiecesSent
        {
            get { return this.piecesSent; }
            internal set { this.piecesSent = value; }
        }

        public int PiecesReceived
        {
            get { return piecesReceived; }
            internal set { piecesReceived = value; }
        }

        internal ushort Port
        {
            get { return this.port; }
            set { this.port = value; }
        }

        internal bool ProcessingQueue
        {
            get { return this.processingQueue; }
            set { this.processingQueue = value; }
        }

        public bool SupportsFastPeer
        {
            get { return this.supportsFastPeer; }
            internal set { this.supportsFastPeer = value; }
        }

        public bool SupportsLTMessages
        {
            get { return this.supportsLTMessages; }
            internal set { this.supportsLTMessages = value; }
        }

        internal MonoTorrentCollection<int> SuggestedPieces
        {
            get { return this.suggestedPieces; }
        }

        public TorrentManager TorrentManager
        {
            get { return this.torrentManager; }
            set
            {
                this.torrentManager = value;
                if (value != null)
                {
                    this.engine = value.Engine;
                    if(value.HasMetadata)
                        BitField = new BitField(value.Torrent.Pieces.Count);
                }
            }
        }
        
        public Uri Uri
        {
            get { return peer.ConnectionUri; }
        }

        #endregion Properties

        #region Constructors

        internal PeerId(Peer peer, TorrentManager manager)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            this.suggestedPieces = new MonoTorrentCollection<int>();
            this.amChoking = true;
            this.isChoking = true;

            this.isAllowedFastPieces = new MonoTorrentCollection<int>();
            this.amAllowedFastPieces = new MonoTorrentCollection<int>();
            this.lastMessageReceived = DateTime.Now;
            this.lastMessageSent = DateTime.Now;
            this.peer = peer;
            this.maxPendingRequests = 2;
            this.maxSupportedPendingRequests = 50;
            this.monitor = new ConnectionMonitor();
            this.sendQueue = new MonoTorrentCollection<PeerMessage>(12);
            ExtensionSupports = new ExtensionSupports();
            TorrentManager = manager;
            InitializeTyrant();
        }

        #endregion

        #region Methods

        public void CloseConnection()
        {
            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                if (Connection != null)
                    Connection.Dispose();
            });
        }

        internal PeerMessage Dequeue()
        {
            return sendQueue.Dequeue();
        }

        internal void Enqueue(PeerMessage msg)
        {
            sendQueue.Add(msg);
            if (!processingQueue)
            {
                processingQueue = true;
                ConnectionManager.ProcessQueue(this);
            }
        }

        internal void EnqueueAt(PeerMessage message, int index)
        {
            if (sendQueue.Count == 0 || index >= sendQueue.Count)
                Enqueue(message);
            else
                sendQueue.Insert(index, message);
        }

        public override bool Equals(object obj)
        {
            PeerId id = obj as PeerId;
            return id == null ? false : this.peer.Equals(id.peer);
        }

        public override int GetHashCode()
        {
            return this.peer.ConnectionUri.GetHashCode();
        }
        
        internal int QueueLength
        {
            get { return this.sendQueue.Count; }
        }

        public void SendMessage(PeerMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                if (Connection == null)
                    return;

                Enqueue(message);
            });
        }

        public override string ToString()
        {
            return this.peer.ConnectionUri.ToString();
        }

        #endregion

        #region BitTyrantasaurus implementation

        private const int MARKET_RATE = 7000;       // taken from reference BitTyrant implementation
        private RateLimiter rateLimiter;            // used to limit the upload capacity we give this peer
        private DateTime lastChokedTime;            // last time we looked that we were still choked
        private DateTime lastRateReductionTime;     // last time we reduced rate of this peer
        private int lastMeasuredDownloadRate;       // last download rate measured
        private long startTime;

        // stats
        private int maxObservedDownloadSpeed;
        private short roundsChoked, roundsUnchoked;     // for stats measurement

        private void InitializeTyrant()
        {
            this.haveMessagesReceived = 0;
            this.startTime = Stopwatch.GetTimestamp();

            this.rateLimiter = new RateLimiter();
            this.uploadRateForRecip = MARKET_RATE;
            this.lastRateReductionTime = DateTime.Now;
            this.lastMeasuredDownloadRate = 0;

            this.maxObservedDownloadSpeed = 0;
            this.roundsChoked = 0;
            this.roundsUnchoked = 0;
        }

        /// <summary>
        /// Measured from number of Have messages
        /// </summary>
        private int haveMessagesReceived;

        /// <summary>
        /// how much we have to send to this peer to guarantee reciprocation
        /// TODO: Can't allow upload rate to exceed this
        /// </summary>
        private int uploadRateForRecip;


        internal int HaveMessagesReceived
        {
            get { return this.haveMessagesReceived; }
            set { this.haveMessagesReceived = value; }
        }

        /// <summary>
        /// This is Up
        /// </summary>
        internal int UploadRateForRecip
        {
            get { return this.uploadRateForRecip; }
        }


        /// <summary>
        /// TGS CHANGE: Get the estimated download rate of this peer based on the rate at which he sends
        /// us Have messages. Note that this could be false if the peer has a malicious client.
        /// Units: Bytes/s
        /// </summary>
        internal int EstimatedDownloadRate
        {
            get
            {
                int timeElapsed = (int)new TimeSpan(Stopwatch.GetTimestamp() - this.startTime).TotalSeconds;
                return (int) (timeElapsed == 0 ? 0 : ((long) this.haveMessagesReceived * this.TorrentManager.Torrent.PieceLength) / timeElapsed);
            }
        }

        /// <summary>
        /// This is the ratio of Dp to Up
        /// </summary>
        internal float Ratio
        {
            get
            {
                float downloadRate = (float)GetDownloadRate();
                return downloadRate / (float)uploadRateForRecip;
            }
        }

        /// <summary>
        /// Last time we looked that this peer was choking us
        /// </summary>
        internal DateTime LastChokedTime
        {
            get { return this.lastChokedTime; }
        }

        /// <summary>
        /// Used to check how much upload capacity we are giving this peer
        /// </summary>
        internal RateLimiter RateLimiter
        {
            get { return this.rateLimiter; }
        }

        internal short RoundsChoked
        {
            get { return this.roundsChoked; }
        }

        internal short RoundsUnchoked
        {
            get { return this.roundsUnchoked; }
        }

        /// <summary>
        /// Get our download rate from this peer -- this is Dp.
        /// 
        /// 1. If we are not choked by this peer, return the actual measure download rate.
        /// 2. If we are choked, then attempt to make an educated guess at the download rate using the following steps
        ///     - use the rate of Have messages received from this peer as an estimate of its download rate
        ///     - assume that its upload rate is equivalent to its estimated download rate
        ///     - divide this upload rate by the standard implementation's active set size for that rate
        /// </summary>
        /// <returns></returns>
        internal int GetDownloadRate()
        {
            if (this.lastMeasuredDownloadRate > 0)
            {
                return this.lastMeasuredDownloadRate;
            }
            else
            {
                // assume that his upload rate will match his estimated download rate, and 
                // get the estimated active set size
                int estimatedDownloadRate = this.EstimatedDownloadRate;
                int activeSetSize = GetActiveSetSize(estimatedDownloadRate);

                return estimatedDownloadRate / activeSetSize;
            }
        }


        /// <summary>
        /// Should be called by ChokeUnchokeManager.ExecuteReview
        /// Logic taken from BitTyrant implementation
        /// </summary>
        internal void UpdateTyrantStats()
        {
            // if we're still being choked, set the time of our last choking
            if (isChoking)
            {
                this.roundsChoked++;

                this.lastChokedTime = DateTime.Now;
            }
            else
            {
                this.roundsUnchoked++;

                if (amInterested)
                {
                    //if we are interested and unchoked, update last measured download rate, unless it is 0
                    if (this.Monitor.DownloadSpeed > 0)
                    {
                        this.lastMeasuredDownloadRate = this.Monitor.DownloadSpeed;

                        this.maxObservedDownloadSpeed = Math.Max(this.lastMeasuredDownloadRate, this.maxObservedDownloadSpeed);
                    }
                }
            }

            // last rate wasn't sufficient to achieve reciprocation
            if (!amChoking && isChoking && isInterested) // only increase upload rate if he's interested, otherwise he won't request any pieces
            {
                this.uploadRateForRecip = (this.uploadRateForRecip * 12) / 10;
            }

            // we've been unchoked by this guy for a while....
            if (!isChoking && !amChoking
                    && (DateTime.Now - lastChokedTime).TotalSeconds > 30
                    && (DateTime.Now - lastRateReductionTime).TotalSeconds > 30)           // only do rate reduction every 30s
            {
                this.uploadRateForRecip = (this.uploadRateForRecip * 9) / 10;
                lastRateReductionTime = DateTime.Now;
            }
        }


        /// <summary>
        /// Compares the actual upload rate with the upload rate that we are supposed to be limiting them to (UploadRateForRecip)
        /// </summary>
        /// <returns>True if the upload rate for recip is greater than the actual upload rate</returns>
        internal bool IsUnderUploadLimit()
        {
            return this.uploadRateForRecip > this.Monitor.UploadSpeed;
        }


        /// <summary>
        /// Stolen from reference BitTyrant implementation (see org.gudy.azureus2.core3.peer.TyrantStats)
        /// </summary>
        /// <param name="uploadRate">Upload rate of peer</param>
        /// <returns>Estimated active set size of peer</returns>
        internal static int GetActiveSetSize(int uploadRate)
        {
            if (uploadRate < 11)
                return 2;
            else if (uploadRate < 35)
                return 3;
            else if (uploadRate < 80)
                return 4;
            else if (uploadRate < 200)
                return 5;
            else if (uploadRate < 350)
                return 6;
            else if (uploadRate < 600)
                return 7;
            else if (uploadRate < 900)
                return 8;
            else
                return 9;
        }

        #endregion BitTyrant

        internal void TryProcessAsyncReads()
        {
            foreach (PeerMessage message in PieceReads)
                Enqueue(message);
            PieceReads.Clear();
            return;
            // We only allow 2 simultaenous PieceMessages in a peers send queue.
            // This way if the peer requests 100 pieces, we don't bloat our memory
            // usage unnecessarily. Once the first message is sent, we read data
            // for the *next* message asynchronously and then add it to the queue.
            // While this is happening, we send data from the second PieceMessage in
            // the queue, thus the queue should rarely be empty.
            int existingReads = 0;
            if (currentlySendingMessage is PieceMessage)
                existingReads++;

            for (int i = 0; existingReads < 2 && i < sendQueue.Count; i++)
                if (sendQueue[i] is PieceMessage)
                    existingReads++;

            if (existingReads >= 2)
                return;

            PieceMessage m = null;
            for (int i = 0; m == null && i < PieceReads.Count; i++)
                if (PieceReads [i].Data == BufferManager.EmptyBuffer)
                    m = PieceReads[i];

            if (m == null)
                return;

            long offset = (long)m.PieceIndex * torrentManager.Torrent.PieceLength + m.StartOffset;
            ClientEngine.BufferManager.GetBuffer(ref m.Data, m.RequestLength);
            engine.DiskManager.QueueRead(torrentManager, offset, m.Data, m.RequestLength, delegate {
                ClientEngine.MainLoop.Queue(delegate {
                    if (!PieceReads.Contains(m))
                        ClientEngine.BufferManager.FreeBuffer(ref m.Data);
                    else
                    {
                        PieceReads.Remove(m);
                        Enqueue(m);
                    }
                    TryProcessAsyncReads();
                });
            });
        }
    }
}
