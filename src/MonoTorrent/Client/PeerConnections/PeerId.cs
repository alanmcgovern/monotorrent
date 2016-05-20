using System;
using System.Collections.Generic;
using System.Diagnostics;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class PeerId //: IComparable<PeerIdInternal>
    {
        #region Constructors

        internal PeerId(Peer peer, TorrentManager manager)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            SuggestedPieces = new MonoTorrentCollection<int>();
            AmChoking = true;
            IsChoking = true;

            IsAllowedFastPieces = new MonoTorrentCollection<int>();
            AmAllowedFastPieces = new MonoTorrentCollection<int>();
            LastMessageReceived = DateTime.Now;
            LastMessageSent = DateTime.Now;
            Peer = peer;
            MaxPendingRequests = 2;
            MaxSupportedPendingRequests = 50;
            Monitor = new ConnectionMonitor();
            sendQueue = new MonoTorrentCollection<PeerMessage>(12);
            ExtensionSupports = new ExtensionSupports();
            TorrentManager = manager;
            InitializeTyrant();
        }

        #endregion

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
            if (CurrentlySendingMessage is PieceMessage)
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
            Engine.DiskManager.QueueRead(torrentManager, offset, m.Data, m.RequestLength, delegate
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

        #region Choke/Unchoke

        internal DateTime? LastUnchoked { get; set; } = null;

        internal long BytesDownloadedAtLastReview { get; set; } = 0;

        internal long BytesUploadedAtLastReview { get; set; } = 0;

        public IConnection Connection { get; internal set; }

        internal double LastReviewDownloadRate { get; set; } = 0;

        internal double LastReviewUploadRate { get; set; } = 0;

        internal bool FirstReviewPeriod { get; set; }

        internal DateTime LastBlockReceived { get; set; } = DateTime.Now;

        //Number of bytes downloaded when this peer was last reviewed - allows us to determine number of bytes

        //downloaded during a review period

        //Download rate determined at the end of the last full review period when this peer was unchoked

        #endregion

        #region Member Variables

        public List<PieceMessage> PieceReads = new List<PieceMessage>();

        private readonly MonoTorrentCollection<PeerMessage> sendQueue; // This holds the peermessages waiting to be sent
        private TorrentManager torrentManager;

        #endregion Member Variables

        #region Properties

        internal byte[] AddressBytes
        {
            get { return Connection.AddressBytes; }
        }

        /// <summary>
        ///     The remote peer can request these and we'll fulfill the request if we're choking them
        /// </summary>
        internal MonoTorrentCollection<int> AmAllowedFastPieces { get; set; }

        public bool AmChoking { get; internal set; }

        public bool AmInterested { get; internal set; }

        public int AmRequestingPiecesCount { get; set; }

        public BitField BitField { get; set; }

        public Software ClientApp { get; internal set; }

        internal ConnectionManager ConnectionManager
        {
            get { return Engine.ConnectionManager; }
        }

        internal PeerMessage CurrentlySendingMessage { get; set; }

        internal IEncryption Decryptor { get; set; }

        internal string DisconnectReason { get; set; }

        public IEncryption Encryptor { get; set; }

        public ClientEngine Engine { get; private set; }

        internal ExtensionSupports ExtensionSupports { get; set; }

        public int HashFails
        {
            get { return Peer.TotalHashFails; }
        }

        internal MonoTorrentCollection<int> IsAllowedFastPieces { get; set; }

        public bool IsChoking { get; internal set; }

        public bool IsConnected
        {
            get { return Connection != null; }
        }

        public bool IsInterested { get; internal set; }

        public bool IsSeeder
        {
            get { return BitField.AllTrue || Peer.IsSeeder; }
        }

        public int IsRequestingPiecesCount { get; set; }

        internal DateTime LastMessageReceived { get; set; }

        internal DateTime LastMessageSent { get; set; }

        internal DateTime WhenConnected { get; set; }

        internal int MaxPendingRequests { get; set; }

        internal int MaxSupportedPendingRequests { get; set; }

        internal MessagingCallback MessageSentCallback { get; set; }

        internal MessagingCallback MessageReceivedCallback { get; set; }

        public ConnectionMonitor Monitor { get; }

        internal Peer Peer { get; set; }

        internal PeerExchangeManager PeerExchangeManager { get; set; }

        public string PeerID
        {
            get { return Peer.PeerId; }
        }

        public int PiecesSent { get; internal set; }

        public int PiecesReceived { get; internal set; }

        internal ushort Port { get; set; }

        internal bool ProcessingQueue { get; set; }

        public bool SupportsFastPeer { get; internal set; }

        public bool SupportsLTMessages { get; internal set; }

        internal MonoTorrentCollection<int> SuggestedPieces { get; }

        public TorrentManager TorrentManager
        {
            get { return torrentManager; }
            set
            {
                torrentManager = value;
                if (value != null)
                {
                    Engine = value.Engine;
                    if (value.HasMetadata)
                        BitField = new BitField(value.Torrent.Pieces.Count);
                }
            }
        }

        public Uri Uri
        {
            get { return Peer.ConnectionUri; }
        }

        #endregion Properties

        #region Methods

        public void CloseConnection()
        {
            ClientEngine.MainLoop.QueueWait(delegate
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
            if (!ProcessingQueue)
            {
                ProcessingQueue = true;
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
            return id == null ? false : Peer.Equals(id.Peer);
        }

        public override int GetHashCode()
        {
            return Peer.ConnectionUri.GetHashCode();
        }

        internal int QueueLength
        {
            get { return sendQueue.Count; }
        }

        public void SendMessage(PeerMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            ClientEngine.MainLoop.QueueWait(delegate
            {
                if (Connection == null)
                    return;

                Enqueue(message);
            });
        }

        public override string ToString()
        {
            return Peer.ConnectionUri.ToString();
        }

        #endregion

        #region BitTyrantasaurus implementation

        private const int MARKET_RATE = 7000; // taken from reference BitTyrant implementation
        private DateTime lastRateReductionTime; // last time we reduced rate of this peer
        private int lastMeasuredDownloadRate; // last download rate measured
        private long startTime;

        // stats
        private int maxObservedDownloadSpeed;

        private void InitializeTyrant()
        {
            haveMessagesReceived = 0;
            startTime = Stopwatch.GetTimestamp();

            RateLimiter = new RateLimiter();
            uploadRateForRecip = MARKET_RATE;
            lastRateReductionTime = DateTime.Now;
            lastMeasuredDownloadRate = 0;

            maxObservedDownloadSpeed = 0;
            RoundsChoked = 0;
            RoundsUnchoked = 0;
        }

        /// <summary>
        ///     Measured from number of Have messages
        /// </summary>
        private int haveMessagesReceived;

        /// <summary>
        ///     how much we have to send to this peer to guarantee reciprocation
        ///     TODO: Can't allow upload rate to exceed this
        /// </summary>
        private int uploadRateForRecip;


        internal int HaveMessagesReceived
        {
            get { return haveMessagesReceived; }
            set { haveMessagesReceived = value; }
        }

        /// <summary>
        ///     This is Up
        /// </summary>
        internal int UploadRateForRecip
        {
            get { return uploadRateForRecip; }
        }


        /// <summary>
        ///     TGS CHANGE: Get the estimated download rate of this peer based on the rate at which he sends
        ///     us Have messages. Note that this could be false if the peer has a malicious client.
        ///     Units: Bytes/s
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
        ///     This is the ratio of Dp to Up
        /// </summary>
        internal float Ratio
        {
            get
            {
                var downloadRate = (float) GetDownloadRate();
                return downloadRate/uploadRateForRecip;
            }
        }

        /// <summary>
        ///     Last time we looked that this peer was choking us
        /// </summary>
        internal DateTime LastChokedTime { get; private set; }

        /// <summary>
        ///     Used to check how much upload capacity we are giving this peer
        /// </summary>
        internal RateLimiter RateLimiter { get; private set; }

        internal short RoundsChoked { get; private set; }

        internal short RoundsUnchoked { get; private set; }

        /// <summary>
        ///     Get our download rate from this peer -- this is Dp.
        ///     1. If we are not choked by this peer, return the actual measure download rate.
        ///     2. If we are choked, then attempt to make an educated guess at the download rate using the following steps
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
            // assume that his upload rate will match his estimated download rate, and 
            // get the estimated active set size
            var estimatedDownloadRate = EstimatedDownloadRate;
            var activeSetSize = GetActiveSetSize(estimatedDownloadRate);

            return estimatedDownloadRate/activeSetSize;
        }


        /// <summary>
        ///     Should be called by ChokeUnchokeManager.ExecuteReview
        ///     Logic taken from BitTyrant implementation
        /// </summary>
        internal void UpdateTyrantStats()
        {
            // if we're still being choked, set the time of our last choking
            if (IsChoking)
            {
                RoundsChoked++;

                LastChokedTime = DateTime.Now;
            }
            else
            {
                RoundsUnchoked++;

                if (AmInterested)
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
            if (!AmChoking && IsChoking && IsInterested)
                // only increase upload rate if he's interested, otherwise he won't request any pieces
            {
                uploadRateForRecip = uploadRateForRecip*12/10;
            }

            // we've been unchoked by this guy for a while....
            if (!IsChoking && !AmChoking
                && (DateTime.Now - LastChokedTime).TotalSeconds > 30
                && (DateTime.Now - lastRateReductionTime).TotalSeconds > 30) // only do rate reduction every 30s
            {
                uploadRateForRecip = uploadRateForRecip*9/10;
                lastRateReductionTime = DateTime.Now;
            }
        }


        /// <summary>
        ///     Compares the actual upload rate with the upload rate that we are supposed to be limiting them to
        ///     (UploadRateForRecip)
        /// </summary>
        /// <returns>True if the upload rate for recip is greater than the actual upload rate</returns>
        internal bool IsUnderUploadLimit()
        {
            return uploadRateForRecip > Monitor.UploadSpeed;
        }


        /// <summary>
        ///     Stolen from reference BitTyrant implementation (see org.gudy.azureus2.core3.peer.TyrantStats)
        /// </summary>
        /// <param name="uploadRate">Upload rate of peer</param>
        /// <returns>Estimated active set size of peer</returns>
        internal static int GetActiveSetSize(int uploadRate)
        {
            if (uploadRate < 11)
                return 2;
            if (uploadRate < 35)
                return 3;
            if (uploadRate < 80)
                return 4;
            if (uploadRate < 200)
                return 5;
            if (uploadRate < 350)
                return 6;
            if (uploadRate < 600)
                return 7;
            if (uploadRate < 900)
                return 8;
            return 9;
        }

        #endregion BitTyrant
    }
}