using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Added BitTyrant stats
    /// See: http://www.cs.washington.edu/homes/piatek/papers/BitTyrant.pdf
    /// </summary>
    public class PeerId
    {
        #region Member Variables

        private bool amChoking;
        private bool amInterested;
        private int amRequestingPiecesCount;
        private BitField bitField;
        private IConnection connection;
		private PeerIdInternal id;
        private Software clientApp;
        private IEncryption encryptor;
        private int hashFails;
        private bool isChoking;
        private bool isInterested;
        private int isRequestingPiecesCount;
        private bool isSeeder;
        private bool isValid;
        private Uri location;
        private ConnectionMonitor monitor;
        private string peerId;
        private int piecesSent;
        private int piecesReceived;             // TGS CHANGE: Added variable
        private int sendQueueLength;
        private bool supportsFastPeer;
        private TorrentManager manager;

        #endregion Member Variables


        #region Properties

        public bool AmChoking
        {
            get { return this.amChoking; }
        }

        public bool AmInterested
        {
            get { return this.amInterested; }
        }

        public int AmRequestingPiecesCount
        {
            get { return this.amRequestingPiecesCount; }
        }

        public BitField Bitfield
        {
            get { return this.bitField; }
        }

        public Software ClientSoftware
        {
            get { return this.clientApp; }
        }

        public IConnection Connection
        {
            get { return this.connection; }
        }

        public IEncryption Encryptor
        {
            get { return this.encryptor; }
        }

        public int HashFails
        {
            get { return this.hashFails; }
        }

        public bool IsChoking
        {
            get { return this.isChoking; }
        }

        public bool IsInterested
        {
            get { return this.isInterested; }
        }

        public int IsRequestingPiecesCount
        {
            get { return this.isRequestingPiecesCount; }
        }

        public bool IsSeeder
        {
            get { return this.isSeeder; }
        }

        public bool IsValid
        {
            get { return isValid; }
            internal set { isValid = value; }
        }

        public Uri Location
        {
            get { return this.location; }
        }

        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }

        public string Name
        {
            get { return this.peerId; }
        }

        public int PiecesSent
        {
            get { return this.piecesSent; }
        }

        /// <summary>
        /// TGS CHANGE: Added property
        /// </summary>
        public int PiecesReceived
        {
            get { return this.piecesReceived; }
        }

        public int SendQueueLength
        {
            get { return this.sendQueueLength; }
        }

        public bool SupportsFastPeer
        {
            get { return this.supportsFastPeer; }
        }

        public TorrentManager TorrentManager
        {
            get { return this.manager; }
        }

        #endregion Properties


        #region Constructors

        internal PeerId()
        {
            this.isValid = true;
            InitializeTyrant();
        }

        #endregion


        #region Methods
        public void CloseConnection ()
        {
            MainLoop.QueueWait(delegate {
                if (id.Connection.Connection != null)
                    id.Connection.Connection.Dispose();
            });
        }
		
        internal void UpdateStats(PeerIdInternal id)
        {
			this.id = id;
			
            if (connection == null)
               connection = id.Connection.Connection;
				
            amChoking = id.Connection.AmChoking;
            amInterested = id.Connection.AmInterested;
            amRequestingPiecesCount = id.Connection.AmRequestingPiecesCount;
            bitField = id.Connection.BitField;
            clientApp = id.Connection.ClientApp;
            encryptor = id.Connection.Encryptor;
            hashFails = id.Peer.TotalHashFails;
            isChoking = id.Connection.IsChoking;
            isInterested = id.Connection.IsInterested;
            isRequestingPiecesCount = id.Connection.IsRequestingPiecesCount;
            isSeeder = id.Peer.IsSeeder;
            location = id.Peer.ConnectionUri;
            monitor = id.Connection.Monitor;
            peerId = id.Peer.PeerId;
            piecesSent = id.Connection.PiecesSent;
            sendQueueLength = id.Connection.QueueLength;
            supportsFastPeer = id.Connection.SupportsFastPeer;
            manager = id.TorrentManager;

            piecesReceived = id.Connection.PiecesReceived;
            rateLimiter.UpdateChunks(uploadRateForRecip, monitor.UploadSpeed);
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


        public int HaveMessagesReceived
        {
            get { return this.haveMessagesReceived; }
            internal set { this.haveMessagesReceived++; }
        }

        /// <summary>
        /// This is Up
        /// </summary>
        public int UploadRateForRecip
        {
            get { return this.uploadRateForRecip; }
        }


        /// <summary>
        /// TGS CHANGE: Get the estimated download rate of this peer based on the rate at which he sends
        /// us Have messages. Note that this could be false if the peer has a malicious client.
        /// Units: Bytes/s
        /// </summary>
        public int EstimatedDownloadRate
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
        public float Ratio
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
        public DateTime LastChokedTime
        {
            get { return this.lastChokedTime; }
        }

        /// <summary>
        /// Used to check how much upload capacity we are giving this peer
        /// </summary>
        public RateLimiter RateLimiter
        {
            get { return this.rateLimiter; }
        }

        public short RoundsChoked
        {
            get { return this.roundsChoked; }
        }

        public short RoundsUnchoked
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
        public int GetDownloadRate()
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
            return this.uploadRateForRecip > this.monitor.UploadSpeed;
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
