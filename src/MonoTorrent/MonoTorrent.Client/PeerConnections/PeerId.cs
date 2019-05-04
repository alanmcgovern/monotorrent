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




using System;
using System.Collections.Generic;
using System.Diagnostics;
using MonoTorrent.Common;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    public class PeerId : IDisposable
    {
        #region Choke/Unchoke

        internal Stopwatch LastUnchoked { get; } = new Stopwatch ();
        internal long BytesDownloadedAtLastReview { get; set; } = 0;
        internal long BytesUploadedAtLastReview { get; set; } = 0;
        public IConnection Connection { get; internal set; }
        internal double LastReviewDownloadRate { get; set; } = 0;
        internal double LastReviewUploadRate { get; set; } = 0;
        internal bool FirstReviewPeriod { get; set; }
        internal Stopwatch LastBlockReceived { get; } = new Stopwatch ();

        #endregion

        #region Member Variables

        private MonoTorrentCollection<PeerMessage> sendQueue;                  // This holds the peermessages waiting to be sent
        private TorrentManager torrentManager;

        #endregion Member Variables

        #region Properties

        internal byte [] AddressBytes => Connection.AddressBytes;

        /// <summary>
        /// The remote peer can request these and we'll fulfill the request if we're choking them
        /// </summary>
        internal MonoTorrentCollection<int> AmAllowedFastPieces { get; set; }
        public bool AmChoking { get; internal set; }
        public bool AmInterested { get; internal set; }
        public int AmRequestingPiecesCount { get; set; }
        public BitField BitField { get; set; }
        public Software ClientApp { get; internal set; }
        ConnectionManager ConnectionManager => Engine.ConnectionManager;
        internal IEncryption Decryptor { get; set; }
        internal string DisconnectReason { get; set; }
        public bool Disposed { get; private set; }
        public IEncryption Encryptor { get; set; }
        public ClientEngine Engine { get; private set;}
        internal ExtensionSupports ExtensionSupports { get; set; }
        public int HashFails => Peer.TotalHashFails;
        internal MonoTorrentCollection<int> IsAllowedFastPieces { get; set; }
        public bool IsChoking { get; internal set; }
        public bool IsConnected => Connection != null;
        public bool IsInterested { get; internal set; }
        public bool IsSeeder => BitField.AllTrue || Peer.IsSeeder;
        public int IsRequestingPiecesCount { get; set; }
        internal Stopwatch LastMessageReceived { get; } = new Stopwatch ();
        internal Stopwatch LastMessageSent { get; } = new Stopwatch ();
        internal Stopwatch WhenConnected { get; } = new Stopwatch ();
        internal int MaxPendingRequests { get; set; }
        internal int MaxSupportedPendingRequests { get; set; }
        public ConnectionMonitor Monitor { get; }
        internal Peer Peer { get; set; }
        internal PeerExchangeManager PeerExchangeManager { get; set; }
        public string PeerID => Peer.PeerId;
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
                    if(value.HasMetadata)
                        BitField = new BitField(value.Torrent.Pieces.Count);
                }
            }
        }

        public Uri Uri => Peer.ConnectionUri;

        #endregion Properties

        #region Constructors

        internal PeerId(Peer peer, TorrentManager manager)
        {
            Peer = peer ?? throw new ArgumentNullException(nameof (peer));
            TorrentManager = manager;


            SuggestedPieces = new MonoTorrentCollection<int>();
            AmChoking = true;
            IsChoking = true;

            IsAllowedFastPieces = new MonoTorrentCollection<int>();
            AmAllowedFastPieces = new MonoTorrentCollection<int>();
            MaxPendingRequests = 2;
            MaxSupportedPendingRequests = 50;
            Monitor = new ConnectionMonitor();
            sendQueue = new MonoTorrentCollection<PeerMessage>(12);
            ExtensionSupports = new ExtensionSupports();

            InitializeTyrant();
        }

        #endregion

        #region Methods

        public void Dispose ()
        {
            if (Disposed)
                return;

            Disposed = true;
            Connection?.Dispose();
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
            PeerId id = obj as PeerId;
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

        public override string ToString()
        {
            return Peer.ConnectionUri.ToString();
        }

        #endregion

        #region BitTyrantasaurus implementation

        private const int MARKET_RATE = 7000;                                   // taken from reference BitTyrant implementation
        private Stopwatch LastRateReductionTime { get; } = new Stopwatch ();    // last time we reduced rate of this peer
        private int lastMeasuredDownloadRate;                                   // last download rate measured
        private Stopwatch TyrantStartTime { get; } = new Stopwatch ();

        // stats
        private int maxObservedDownloadSpeed;

        private void InitializeTyrant()
        {
            HaveMessagesReceived = 0;
            TyrantStartTime.Restart ();

            RateLimiter = new RateLimiter();
            UploadRateForRecip = MARKET_RATE;
            LastRateReductionTime.Restart ();
            lastMeasuredDownloadRate = 0;

            maxObservedDownloadSpeed = 0;
            RoundsChoked = 0;
            RoundsUnchoked = 0;
        }

        internal int HaveMessagesReceived { get; set; }

        /// <summary>
        /// This is Up
        /// </summary>
        internal int UploadRateForRecip { get; private set; }


        /// <summary>
        /// TGS CHANGE: Get the estimated download rate of this peer based on the rate at which he sends
        /// us Have messages. Note that this could be false if the peer has a malicious client.
        /// Units: Bytes/s
        /// </summary>
        internal int EstimatedDownloadRate
        {
            get
            {
                int timeElapsed = (int)TyrantStartTime.Elapsed.TotalSeconds;
                return (int) (timeElapsed == 0 ? 0 : ((long) this.HaveMessagesReceived * this.TorrentManager.Torrent.PieceLength) / timeElapsed);
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
                return downloadRate / (float)UploadRateForRecip;
            }
        }

        /// <summary>
        /// Last time we looked that this peer was choking us
        /// </summary>
        internal Stopwatch LastChokedTime { get; } = new Stopwatch ();

        /// <summary>
        /// Used to check how much upload capacity we are giving this peer
        /// </summary>
        internal RateLimiter RateLimiter { get; private set; }

        internal short RoundsChoked { get; private set; }

        internal short RoundsUnchoked { get; private set; }

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
            if (IsChoking)
            {
                RoundsChoked++;

                LastChokedTime.Restart ();
            }
            else
            {
                this.RoundsUnchoked++;

                if (AmInterested)
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
            if (!AmChoking && IsChoking && IsInterested) // only increase upload rate if he's interested, otherwise he won't request any pieces
            {
                this.UploadRateForRecip = (this.UploadRateForRecip * 12) / 10;
            }

            // we've been unchoked by this guy for a while....
            if (!IsChoking && !AmChoking
                    && LastChokedTime.Elapsed.TotalSeconds > 30
                    && LastRateReductionTime.Elapsed.TotalSeconds > 30)           // only do rate reduction every 30s
            {
                this.UploadRateForRecip = (this.UploadRateForRecip * 9) / 10;
                LastRateReductionTime.Restart ();
            }
        }


        /// <summary>
        /// Compares the actual upload rate with the upload rate that we are supposed to be limiting them to (UploadRateForRecip)
        /// </summary>
        /// <returns>True if the upload rate for recip is greater than the actual upload rate</returns>
        internal bool IsUnderUploadLimit()
        {
            return this.UploadRateForRecip > this.Monitor.UploadSpeed;
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
