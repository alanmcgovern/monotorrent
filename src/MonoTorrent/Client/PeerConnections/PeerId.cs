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
            get { return lastUnchoked; }
            set { lastUnchoked = value; }
        }

        internal long BytesDownloadedAtLastReview
        {
            get { return bytesDownloadedAtLastReview; }
            set { bytesDownloadedAtLastReview = value; }
        }

        internal long BytesUploadedAtLastReview
        {
            get { return bytesUploadedAtLastReview; }
            set { bytesUploadedAtLastReview = value; }
        }

        public IConnection Connection
        {
            get { return connection; }
            internal set { connection = value; }
        }

        internal double LastReviewDownloadRate
        {
            get { return lastReviewDownloadRate; }
            set { lastReviewDownloadRate = value; }
        }

        internal double LastReviewUploadRate
        {
            get { return lastReviewUploadRate; }
            set { lastReviewUploadRate = value; }
        }

        internal bool FirstReviewPeriod
        {
            get { return firstReviewPeriod; }
            set { firstReviewPeriod = value; }
        }

        internal DateTime LastBlockReceived
        {
            get { return lastBlockReceived; }
            set { lastBlockReceived = value; }
        }

        private DateTime? lastUnchoked = null; //When this peer was last unchoked, or null if we haven't unchoked it yet

        private long bytesDownloadedAtLastReview = 0;
            //Number of bytes downloaded when this peer was last reviewed - allows us to determine number of bytes

        //downloaded during a review period
        private long bytesUploadedAtLastReview = 0; //Ditto for uploaded bytes

        private double lastReviewDownloadRate = 0;
            //Download rate determined at the end of the last full review period when this peer was unchoked

        private double lastReviewUploadRate = 0; //Ditto for upload rate
        private bool firstReviewPeriod; //Set true if this is the first review period since this peer was last unchoked
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
        private MonoTorrentCollection<PeerMessage> sendQueue; // This holds the peermessages waiting to be sent
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
            get { return amAllowedFastPieces; }
            set { amAllowedFastPieces = value; }
        }

        public bool AmChoking
        {
            get { return amChoking; }
            internal set { amChoking = value; }
        }

        public bool AmInterested
        {
            get { return amInterested; }
            internal set { amInterested = value; }
        }

        public int AmRequestingPiecesCount
        {
            get { return amRequestingPiecesCount; }
            set { amRequestingPiecesCount = value; }
        }

        public BitField BitField
        {
            get { return bitField; }
            set { bitField = value; }
        }

        public Software ClientApp
        {
            get { return clientApp; }
            internal set { clientApp = value; }
        }

        internal ConnectionManager ConnectionManager
        {
            get { return engine.ConnectionManager; }
        }

        internal PeerMessage CurrentlySendingMessage
        {
            get { return currentlySendingMessage; }
            set { currentlySendingMessage = value; }
        }

        internal IEncryption Decryptor
        {
            get { return decryptor; }
            set { decryptor = value; }
        }

        internal string DisconnectReason
        {
            get { return disconnectReason; }
            set { disconnectReason = value; }
        }

        public IEncryption Encryptor
        {
            get { return encryptor; }
            set { encryptor = value; }
        }

        public ClientEngine Engine
        {
            get
            {
                return engine;
                ;
            }
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
            get { return isAllowedFastPieces; }
            set { isAllowedFastPieces = value; }
        }

        public bool IsChoking
        {
            get { return isChoking; }
            internal set { isChoking = value; }
        }

        public bool IsConnected
        {
            get { return Connection != null; }
        }

        public bool IsInterested
        {
            get { return isInterested; }
            internal set { isInterested = value; }
        }

        public bool IsSeeder
        {
            get { return bitField.AllTrue || peer.IsSeeder; }
        }

        public int IsRequestingPiecesCount
        {
            get { return isRequestingPiecesCount; }
            set { isRequestingPiecesCount = value; }
        }

        internal DateTime LastMessageReceived
        {
            get { return lastMessageReceived; }
            set { lastMessageReceived = value; }
        }

        internal DateTime LastMessageSent
        {
            get { return lastMessageSent; }
            set { lastMessageSent = value; }
        }

        internal DateTime WhenConnected
        {
            get { return whenConnected; }
            set { whenConnected = value; }
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
            get { return messageSentCallback; }
            set { messageSentCallback = value; }
        }

        internal MessagingCallback MessageReceivedCallback
        {
            get { return messageReceivedCallback; }
            set { messageReceivedCallback = value; }
        }

        public ConnectionMonitor Monitor
        {
            get { return monitor; }
        }

        internal Peer Peer
        {
            get { return peer; }
            set { peer = value; }
        }

        internal PeerExchangeManager PeerExchangeManager
        {
            get { return pexManager; }
            set { pexManager = value; }
        }

        public string PeerID
        {
            get { return peer.PeerId; }
        }

        public int PiecesSent
        {
            get { return piecesSent; }
            internal set { piecesSent = value; }
        }

        public int PiecesReceived
        {
            get { return piecesReceived; }
            internal set { piecesReceived = value; }
        }

        internal ushort Port
        {
            get { return port; }
            set { port = value; }
        }

        internal bool ProcessingQueue
        {
            get { return processingQueue; }
            set { processingQueue = value; }
        }

        public bool SupportsFastPeer
        {
            get { return supportsFastPeer; }
            internal set { supportsFastPeer = value; }
        }

        public bool SupportsLTMessages
        {
            get { return supportsLTMessages; }
            internal set { supportsLTMessages = value; }
        }

        internal MonoTorrentCollection<int> SuggestedPieces
        {
            get { return suggestedPieces; }
        }

        public TorrentManager TorrentManager
        {
            get { return torrentManager; }
            set
            {
                torrentManager = value;
                if (value != null)
                {
                    engine = value.Engine;
                    if (value.HasMetadata)
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

            suggestedPieces = new MonoTorrentCollection<int>();
            amChoking = true;
            isChoking = true;

            isAllowedFastPieces = new MonoTorrentCollection<int>();
            amAllowedFastPieces = new MonoTorrentCollection<int>();
            lastMessageReceived = DateTime.Now;
            lastMessageSent = DateTime.Now;
            this.peer = peer;
            maxPendingRequests = 2;
            maxSupportedPendingRequests = 50;
            monitor = new ConnectionMonitor();
            sendQueue = new MonoTorrentCollection<PeerMessage>(12);
            ExtensionSupports = new ExtensionSupports();
            TorrentManager = manager;
            InitializeTyrant();
        }

        #endregion

        #region Methods

        public void CloseConnection()
        {
            ClientEngine.MainLoop.QueueWait((MainLoopTask) delegate
            {
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
            var id = obj as PeerId;
            return id == null ? false : peer.Equals(id.peer);
        }

        public override int GetHashCode()
        {
            return peer.ConnectionUri.GetHashCode();
        }

        internal int QueueLength
        {
            get { return sendQueue.Count; }
        }

        public void SendMessage(PeerMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            ClientEngine.MainLoop.QueueWait((MainLoopTask) delegate
            {
                if (Connection == null)
                    return;

                Enqueue(message);
            });
        }

        public override string ToString()
        {
            return peer.ConnectionUri.ToString();
        }

        #endregion

        #region BitTyrantasaurus implementation

        private const int MARKET_RATE = 7000; // taken from reference BitTyrant implementation
        private RateLimiter rateLimiter; // used to limit the upload capacity we give this peer
        private DateTime lastChokedTime; // last time we looked that we were still choked
        private DateTime lastRateReductionTime; // last time we reduced rate of this peer
        private int lastMeasuredDownloadRate; // last download rate measured
        private long startTime;

        // stats
        private int maxObservedDownloadSpeed;
        private short roundsChoked, roundsUnchoked; // for stats measurement

        private void InitializeTyrant()
        {
            haveMessagesReceived = 0;
            startTime = Stopwatch.GetTimestamp();

            rateLimiter = new RateLimiter();
            uploadRateForRecip = MARKET_RATE;
            lastRateReductionTime = DateTime.Now;
            lastMeasuredDownloadRate = 0;

            maxObservedDownloadSpeed = 0;
            roundsChoked = 0;
            roundsUnchoked = 0;
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
            get { return haveMessagesReceived; }
            set { haveMessagesReceived = value; }
        }

        /// <summary>
        /// This is Up
        /// </summary>
        internal int UploadRateForRecip
        {
            get { return uploadRateForRecip; }
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
                var timeElapsed = (int) new TimeSpan(Stopwatch.GetTimestamp() - startTime).TotalSeconds;
                return
                    (int)
                        (timeElapsed == 0
                            ? 0
                            : (long) haveMessagesReceived*TorrentManager.Torrent.PieceLength/timeElapsed);
            }
        }

        /// <summary>
        /// This is the ratio of Dp to Up
        /// </summary>
        internal float Ratio
        {
            get
            {
                var downloadRate = (float) GetDownloadRate();
                return downloadRate/(float) uploadRateForRecip;
            }
        }

        /// <summary>
        /// Last time we looked that this peer was choking us
        /// </summary>
        internal DateTime LastChokedTime
        {
            get { return lastChokedTime; }
        }

        /// <summary>
        /// Used to check how much upload capacity we are giving this peer
        /// </summary>
        internal RateLimiter RateLimiter
        {
            get { return rateLimiter; }
        }

        internal short RoundsChoked
        {
            get { return roundsChoked; }
        }

        internal short RoundsUnchoked
        {
            get { return roundsUnchoked; }
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
            if (lastMeasuredDownloadRate > 0)
            {
                return lastMeasuredDownloadRate;
            }
            else
            {
                // assume that his upload rate will match his estimated download rate, and 
                // get the estimated active set size
                var estimatedDownloadRate = EstimatedDownloadRate;
                var activeSetSize = GetActiveSetSize(estimatedDownloadRate);

                return estimatedDownloadRate/activeSetSize;
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
                roundsChoked++;

                lastChokedTime = DateTime.Now;
            }
            else
            {
                roundsUnchoked++;

                if (amInterested)
                {
                    //if we are interested and unchoked, update last measured download rate, unless it is 0
                    if (Monitor.DownloadSpeed > 0)
                    {
                        lastMeasuredDownloadRate = Monitor.DownloadSpeed;

                        maxObservedDownloadSpeed = Math.Max(lastMeasuredDownloadRate,
                            maxObservedDownloadSpeed);
                    }
                }
            }

            // last rate wasn't sufficient to achieve reciprocation
            if (!amChoking && isChoking && isInterested)
                // only increase upload rate if he's interested, otherwise he won't request any pieces
            {
                uploadRateForRecip = uploadRateForRecip*12/10;
            }

            // we've been unchoked by this guy for a while....
            if (!isChoking && !amChoking
                && (DateTime.Now - lastChokedTime).TotalSeconds > 30
                && (DateTime.Now - lastRateReductionTime).TotalSeconds > 30) // only do rate reduction every 30s
            {
                uploadRateForRecip = uploadRateForRecip*9/10;
                lastRateReductionTime = DateTime.Now;
            }
        }


        /// <summary>
        /// Compares the actual upload rate with the upload rate that we are supposed to be limiting them to (UploadRateForRecip)
        /// </summary>
        /// <returns>True if the upload rate for recip is greater than the actual upload rate</returns>
        internal bool IsUnderUploadLimit()
        {
            return uploadRateForRecip > Monitor.UploadSpeed;
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
            var existingReads = 0;
            if (currentlySendingMessage is PieceMessage)
                existingReads++;

            for (var i = 0; existingReads < 2 && i < sendQueue.Count; i++)
                if (sendQueue[i] is PieceMessage)
                    existingReads++;

            if (existingReads >= 2)
                return;

            PieceMessage m = null;
            for (var i = 0; m == null && i < PieceReads.Count; i++)
                if (PieceReads[i].Data == BufferManager.EmptyBuffer)
                    m = PieceReads[i];

            if (m == null)
                return;

            var offset = (long) m.PieceIndex*torrentManager.Torrent.PieceLength + m.StartOffset;
            ClientEngine.BufferManager.GetBuffer(ref m.Data, m.RequestLength);
            engine.DiskManager.QueueRead(torrentManager, offset, m.Data, m.RequestLength, delegate
            {
                ClientEngine.MainLoop.Queue(delegate
                {
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