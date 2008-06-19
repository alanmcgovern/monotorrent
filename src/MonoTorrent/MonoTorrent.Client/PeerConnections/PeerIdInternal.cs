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
namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class PeerId //: IComparable<PeerIdInternal>
    {
        #region Choke/Unchoke
        /// <summary>
        /// When this peer was last unchoked, or null if we haven't unchoked it yet
        /// </summary>
        internal DateTime? LastUnchoked
        {
            get { return this.lastUnchoked; }
            set { this.lastUnchoked = value; }
        }

        /// <summary>
        /// Number of bytes downloaded when this peer was last reviewed
        /// </summary>
        internal long BytesDownloadedAtLastReview
        {
            get { return this.bytesDownloadedAtLastReview; }
            set { this.bytesDownloadedAtLastReview = value; }
        }

        /// <summary>
        /// Number of bytes uploaded when this peer was last reviewed
        /// </summary>
        internal long BytesUploadedAtLastReview
        {
            get { return this.bytesUploadedAtLastReview; }
            set { this.bytesUploadedAtLastReview = value; }
        }

        /// <summary>
        /// Download rate determined at the end of the last full review period when this peer was unchoked
        /// </summary>
        internal double LastReviewDownloadRate
        {
            get { return this.lastReviewDownloadRate; }
            set { this.lastReviewDownloadRate = value; }
        }

        /// <summary>
        /// Upload rate determined at the end of the last full review period when this peer was unchoked
        /// </summary>
        internal double LastReviewUploadRate
        {
            get { return this.lastReviewUploadRate; }
            set { this.lastReviewUploadRate = value; }
        }

        /// <summary>
        /// True if this is the first review period since this peer was last unchoked
        /// </summary>
        internal bool FirstReviewPeriod
        {
            get { return this.firstReviewPeriod; }
            set { this.firstReviewPeriod = value; }
        }
        private DateTime? lastUnchoked = null;        //When this peer was last unchoked, or null if we haven't unchoked it yet
        private long bytesDownloadedAtLastReview = 0; //Number of bytes downloaded when this peer was last reviewed - allows us to determine number of bytes
        //downloaded during a review period
        private long bytesUploadedAtLastReview = 0;   //Ditto for uploaded bytes
        private double lastReviewDownloadRate = 0;    //Download rate determined at the end of the last full review period when this peer was unchoked
        private double lastReviewUploadRate = 0;      //Ditto for upload rate
        private bool firstReviewPeriod;               //Set true if this is the first review period since this peer was last unchoked

        #endregion

        #region Member Variables

        private MonoTorrentCollection<int> amAllowedFastPieces;
        private bool amChoking;
        private bool amInterested;
        private int amRequestingPiecesCount;
        private BitField bitField;
        private int bytesReceived;
        private int bytesSent;
        private int bytesToRecieve;
        private int bytesToSend;
        private Software clientApp;
        internal IConnection Connection;
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
        private MonoTorrentCollection<ExtensionSupport> extensionSupports;
        private int maxPendingRequests;
        private MessagingCallback messageReceivedCallback;
        private MessagingCallback messageSentCallback;
        private ConnectionMonitor monitor;
        private Peer peer;
        private PeerExchangeManager pexManager;
        private int piecesSent;
        private int piecesReceived;
        private ushort port;
        private bool processingQueue;
        internal ArraySegment<byte> recieveBuffer = BufferManager.EmptyBuffer;      // The byte array used to buffer data while it's being received
        internal ArraySegment<byte> sendBuffer = BufferManager.EmptyBuffer;         // The byte array used to buffer data before it's sent
        private Queue<PeerMessage> sendQueue;                  // This holds the peermessages waiting to be sent
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
        /// Contains the indexs of all the pieces we will let the peer download even if they are choked
        /// </summary>
        internal MonoTorrentCollection<int> AmAllowedFastPieces
        {
            get { return this.amAllowedFastPieces; }
            set { this.amAllowedFastPieces = value; }
        }

        /// <summary>
        /// True if we are currently choking the peer
        /// </summary>
        public bool AmChoking
        {
            get { return this.amChoking; }
            internal set { this.amChoking = value; }
        }

        /// <summary>
        /// True if the peer has some pieces that we need
        /// </summary>
        public bool AmInterested
        {
            get { return this.amInterested; }
            internal set { this.amInterested = value; }
        }

        /// <summary>
        /// Returns the number of pieces currently being requested
        /// </summary>
        public int AmRequestingPiecesCount
        {
            get { return this.amRequestingPiecesCount; }
            set { this.amRequestingPiecesCount = value; }
        }

        /// <summary>
        /// The peers bitfield
        /// </summary>
        public BitField BitField
        {
            get { return this.bitField; }
            set { this.bitField = value; }
        }

        /// <summary>
        /// The total number of bytes Received into the current recieve buffer
        /// </summary>
        internal int BytesReceived
        {
            get { return this.bytesReceived; }
            set { this.bytesReceived = value; }
        }

        /// <summary>
        /// The total number of bytes sent from the current send buffer
        /// </summary>
        internal int BytesSent
        {
            get { return this.bytesSent; }
            set { this.bytesSent = value; }
        }

        /// <summary>
        /// The total number of bytes to receive
        /// </summary>
        internal int BytesToRecieve
        {
            get { return this.bytesToRecieve; }
            set { this.bytesToRecieve = value; }
        }

        /// <summary>
        /// The total bytes to send from the buffer
        /// </summary>
        internal int BytesToSend
        {
            get { return this.bytesToSend; }
            set { this.bytesToSend = value; }
        }

        /// <summary>
        /// Contains the version and name of the application this client is using.
        /// </summary>
        public Software ClientApp
        {
            get { return this.clientApp; }
            internal set { this.clientApp = value; }
        }

        internal ConnectionManager ConnectionManager
        {
            get { return this.engine.ConnectionManager; }
        }

        /// <summary>
        /// This is the message we're currently sending to a peer
        /// </summary>
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

        /// <summary>
        /// Reason for disconnecting from this peer.
        /// </summary>
        internal string DisconnectReason
        {
            get { return this.disconnectReason; }
            set { this.disconnectReason = value; }
        }

        internal IEncryption Encryptor
        {
            get { return this.encryptor; }
            set { this.encryptor = value; }
        }

        public ClientEngine Engine
        {
            get { return this.engine; ; }
        }

        internal MonoTorrentCollection<ExtensionSupport> ExtensionSupports
        {
            get { return extensionSupports; }
            set { extensionSupports = value; }
        }

        /// <summary>
        /// Contains the indexes of all the pieces which the peer will let us download even if we are choked
        /// </summary>
        internal MonoTorrentCollection<int> IsAllowedFastPieces
        {
            get { return this.isAllowedFastPieces; }
            set { this.isAllowedFastPieces = value; }
        }

        /// <summary>
        /// True if the peer is currently choking us
        /// </summary>
        public bool IsChoking
        {
            get { return this.isChoking; }
            internal set { this.isChoking = value; }
        }

        /// <summary>
        /// True if the peer is currently interested in us
        /// </summary>
        public bool IsInterested
        {
            get { return this.isInterested; }
            internal set { this.isInterested = value; }
        }

        /// <summary>
        /// The number of pieces the peer has requested off me
        /// </summary>
        public int IsRequestingPiecesCount
        {
            get { return this.isRequestingPiecesCount; }
            set { this.isRequestingPiecesCount = value; }
        }

        /// <summary>
        /// The time at which the last message was received at
        /// </summary>
        internal DateTime LastMessageReceived
        {
            get { return this.lastMessageReceived; }
            set { this.lastMessageReceived = value; }
        }

        /// <summary>
        /// The time at which the last message was sent at
        /// </summary>
        internal DateTime LastMessageSent
        {
            get { return this.lastMessageSent; }
            set { this.lastMessageSent = value; }
        }

        internal int MaxPendingRequests
        {
            get { return maxPendingRequests; }
            set { maxPendingRequests = value; }
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

        /// <summary>
        /// The connection Monitor for this peer
        /// </summary>
        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// The number of pieces that we've sent the peer.
        /// </summary>
        public int PiecesSent
        {
            get { return this.piecesSent; }
            internal set { this.piecesSent = value; }
        }

        /// <summary>
        /// Number of pieces we've received from the peer
        /// </summary>
        public int PiecesReceived
        {
            get { return piecesReceived; }
            internal set { piecesReceived = value; }
        }

        /// <summary>
        /// The port the peer is listening on for DHT
        /// </summary>
        internal ushort Port
        {
            get { return this.port; }
            set { this.port = value; }
        }

        /// <summary>
        /// True if we are currently processing the peers message queue
        /// </summary>
        internal bool ProcessingQueue
        {
            get { return this.processingQueue; }
            set { this.processingQueue = value; }
        }

        /// <summary>
        /// True if the peer supports the Fast Peers extension
        /// </summary>
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

        /// <summary>
        /// A list of pieces that this peer has suggested we download. These should be downloaded
        /// with higher priority than standard pieces.
        /// </summary>
        internal MonoTorrentCollection<int> SuggestedPieces
        {
            get { return this.suggestedPieces; }
        }

        /// <summary>
        /// 
        /// </summary>
        public TorrentManager TorrentManager
        {
            get { return this.torrentManager; }
            set
            {
                this.torrentManager = value;
                if (value != null)
                {
                    this.engine = value.Engine;
                    this.BitField = new BitField(value.Torrent.Pieces.Count);
                }
            }
        }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="manager"></param>
        internal PeerId(Peer peer, TorrentManager manager)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            this.suggestedPieces = new MonoTorrentCollection<int>();
            this.amChoking = true;
            this.isChoking = true;
            this.bitField = new BitField(manager.Bitfield.Length);

            this.isAllowedFastPieces = new MonoTorrentCollection<int>();
            this.amAllowedFastPieces = new MonoTorrentCollection<int>();
            this.lastMessageReceived = DateTime.Now;
            this.lastMessageSent = DateTime.Now;
            this.peer = peer;
            this.monitor = new ConnectionMonitor();
            this.sendQueue = new Queue<PeerMessage>(12);
            TorrentManager = manager;
            InitializeTyrant();
        }

        #endregion

        #region Methods

        public void CloseConnection()
        {
            MainLoop.QueueWait(delegate
            {
                if (Connection != null)
                    Connection.Dispose();
            });
        }

        /// <summary>
        /// Returns the PeerMessage at the head of the queue
        /// </summary>
        /// <returns></returns>
        internal PeerMessage Dequeue()
        {
            return sendQueue.Dequeue();
        }

        /// <summary>
        /// Queues a PeerMessage up to be sent to the remote host
        /// </summary>
        /// <param name="msg"></param>
        internal void Enqueue(PeerMessage msg)
        {
            sendQueue.Enqueue(msg);
        }

        /// <summary>
        /// Enqueues a peer message at the specified index
        /// </summary>
        /// <param name="message"></param>
        /// <param name="index"></param>
        internal void EnqueueAt(PeerMessage message, int index)
        {
            int length = this.sendQueue.Count;

            if (length == 0)
                this.sendQueue.Enqueue(message);
            else
                for (int i = 0; i < length; i++)
                {
                    if (i == index)
                        this.sendQueue.Enqueue(message);

                    this.sendQueue.Enqueue(this.sendQueue.Dequeue());
                }
        }

        public override bool Equals(object obj)
        {
            PeerId id = obj as PeerId;
            return id == null ? false : this.peer.ConnectionUri.Equals(id.peer.ConnectionUri);
        }

        public override int GetHashCode()
        {
            return this.peer.ConnectionUri.GetHashCode();
        }
        
        /// <summary>
        /// The length of the Message queue
        /// </summary>
        internal int QueueLength
        {
            get { return this.sendQueue.Count; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytesRecieved"></param>
        /// <param name="type"></param>
        internal void ReceivedBytes(int bytesRecieved, TransferType type)
        {
            this.bytesReceived += bytesRecieved;
            this.monitor.BytesReceived(bytesRecieved, type);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytesSent"></param>
        /// <param name="type"></param>
        internal void SentBytes(int bytesSent, TransferType type)
        {
            this.bytesSent += bytesSent;
            this.monitor.BytesSent(bytesSent, type);
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
            set { this.haveMessagesReceived++; }
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
                return timeElapsed == 0 ? 0 : (this.haveMessagesReceived * this.TorrentManager.Torrent.PieceLength) / timeElapsed;
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
    }
}
