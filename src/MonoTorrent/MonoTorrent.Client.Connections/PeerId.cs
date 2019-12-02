//
// PeerId.cs
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

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Libtorrent;

namespace MonoTorrent.Client
{
    public partial class PeerId
    {
        /// <summary>
        /// Creates a PeerID with a null TorrentManager and IConnection2. This is used for unit testing purposes.
        /// The peer will have <see cref="ProcessingQueue"/>, <see cref="IsChoking"/> and <see cref="AmChoking"/>
        /// set to true. A bitfield with all pieces set to <see langword="false"/> will be created too.
        /// </summary>
        /// <param name="bitfieldLength"></param>
        /// <returns></returns>
        internal static PeerId CreateNull (int bitfieldLength)
            => CreateNull (bitfieldLength, false, true, false);

        /// <summary>
        /// Creates a PeerID with a null TorrentManager and IConnection2. This is used for unit testing purposes.
        /// The peer will have <see cref="ProcessingQueue"/>, <see cref="IsChoking"/> and <see cref="AmChoking"/>
        /// set to true. A bitfield with all pieces set to <see langword="false"/> will be created too.
        /// </summary>
        /// <param name="bitfieldLength"></param>
        /// <param name="seeder">True if the returned peer should be treated as a seeder (the bitfield will have all pieces set to 'true')</param>
        /// <param name="isChoking"></param>
        /// <param name="amInterested"></param>
        /// <returns></returns>
        internal static PeerId CreateNull(int bitfieldLength, bool seeder, bool isChoking, bool amInterested)
        {
            return new PeerId (new Peer ("null", new Uri ("ipv4://hardcodedvalue:12345"))) {
                IsChoking = isChoking,
                AmChoking = true,
                AmInterested = amInterested,
                BitField = new BitField (bitfieldLength).SetAll (seeder),
                ProcessingQueue = true
            };
        }

        #region Choke/Unchoke

        internal long BytesDownloadedAtLastReview { get; set; } = 0;
        internal long BytesUploadedAtLastReview { get; set; } = 0;
        internal IConnection2 Connection { get; }
        internal double LastReviewDownloadRate { get; set; } = 0;
        internal double LastReviewUploadRate { get; set; } = 0;
        internal bool FirstReviewPeriod { get; set; }
        internal ValueStopwatch LastBlockReceived;
        internal ValueStopwatch LastPeerExchangeReview;
        internal ValueStopwatch LastUnchoked;

        #endregion

        #region Properties

        public bool AmChoking { get; internal set; }
        public bool AmInterested { get; internal set; }
        public int AmRequestingPiecesCount { get; internal set; }
        public BitField BitField { get; internal set; }
        public Software ClientApp { get; internal set; }
        public EncryptionTypes EncryptionType {
            get {
                if (Encryptor is RC4)
                    return EncryptionTypes.RC4Full;
                if (Encryptor is RC4Header)
                    return EncryptionTypes.RC4Header;
                if (Encryptor is PlainTextEncryption || Encryptor == null)
                    return EncryptionTypes.PlainText;
                return EncryptionTypes.None;
            }
        }
        public bool IsChoking { get; internal set; }
        public bool IsConnected => !Disposed;
        public bool IsInterested { get; internal set; }
        public int IsRequestingPiecesCount { get; internal set; }
        public bool IsSeeder => BitField.AllTrue || Peer.IsSeeder;
        public ConnectionMonitor Monitor { get; }
        public BEncodedString PeerID => Peer.PeerId;
        public int PiecesSent { get; internal set; }
        public int PiecesReceived { get; internal set; }
        public bool SupportsFastPeer { get; internal set; }
        public bool SupportsLTMessages { get; internal set; }
        public Uri Uri => Peer.ConnectionUri;

        internal byte [] AddressBytes => Connection.AddressBytes;

        /// <summary>
        /// The remote peer can request these and we'll fulfill the request if we're choking them
        /// </summary>
        internal List<int> AmAllowedFastPieces { get; set; }
        internal IEncryption Decryptor { get; set; }
        internal bool Disposed { get; private set; }
        internal IEncryption Encryptor { get; set; }
        internal ExtensionSupports ExtensionSupports { get; set; }
        internal List<int> IsAllowedFastPieces { get; set; }
        internal ValueStopwatch LastMessageReceived;
        internal ValueStopwatch LastMessageSent;
        internal ValueStopwatch WhenConnected;
        internal int MaxPendingRequests { get; set; }
        internal int MaxSupportedPendingRequests { get; set; }
        internal Peer Peer { get; }
        internal PeerExchangeManager PeerExchangeManager { get; set; }
        internal ushort Port { get; set; }
        internal bool ProcessingQueue { get; set; }
        internal int QueueLength => SendQueue.Count;
        internal List<int> SuggestedPieces { get; }

        List<PeerMessage> SendQueue { get; }

        #endregion Properties

        #region Constructors

        PeerId (Peer peer)
        {
            Peer = peer;

            AmChoking = true;
            IsChoking = true;

            LastMessageReceived = new ValueStopwatch ();
            LastMessageSent = new ValueStopwatch ();
            WhenConnected = new ValueStopwatch ();

            AmAllowedFastPieces = new List<int>();
            IsAllowedFastPieces = new List<int>();
            SuggestedPieces = new List<int>();

            MaxPendingRequests = 2;
            MaxSupportedPendingRequests = 50;

            ExtensionSupports = new ExtensionSupports();
            Monitor = new ConnectionMonitor();
            SendQueue = new List<PeerMessage>(12);

            InitializeTyrant();
        }

        internal PeerId(Peer peer, IConnection connection, BitField bitfield)
            : this (peer)
        {
            if (connection == null)
                throw new ArgumentNullException (nameof (connection));
            Connection = ConnectionConverter.Convert (connection);
            Peer = peer ?? throw new ArgumentNullException (nameof (peer));
            BitField = bitfield;
        }

        #endregion

        #region Methods

        internal void Dispose ()
        {
            if (Disposed)
                return;

            Disposed = true;
            SendQueue.Clear ();
            Connection.SafeDispose ();
        }

        internal PeerMessage Dequeue()
        {
            var message = SendQueue[0];
            SendQueue.RemoveAt (0);
            return message;
        }

        internal void Enqueue(PeerMessage message)
            => EnqueueAt (message, SendQueue.Count);

        internal void EnqueueAt(PeerMessage message, int index)
        {
            if (SendQueue.Count == 0 || index >= SendQueue.Count)
                SendQueue.Add (message);
            else
                SendQueue.Insert(index, message);
        }

        public override string ToString()
        {
            return Peer.ConnectionUri.ToString();
        }

        #endregion

        #region BitTyrantasaurus implementation

        private const int MARKET_RATE = 7000;                                   // taken from reference BitTyrant implementation
        ValueStopwatch LastRateReductionTime;                   // last time we reduced rate of this peer
        long lastMeasuredDownloadRate;                                   // last download rate measured
        ValueStopwatch TyrantStartTime;

        // stats
        long maxObservedDownloadSpeed;

        private void InitializeTyrant()
        {
            HaveMessageEstimatedDownloadedBytes = 0;
            TyrantStartTime.Restart ();

            UploadRateForRecip = MARKET_RATE;
            LastRateReductionTime.Restart ();
            lastMeasuredDownloadRate = 0;

            maxObservedDownloadSpeed = 0;
            RoundsChoked = 0;
            RoundsUnchoked = 0;
        }

        internal long HaveMessageEstimatedDownloadedBytes { get; set; }

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
                return (int) (timeElapsed == 0 ? 0 : ((long) HaveMessageEstimatedDownloadedBytes) / timeElapsed);
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
        internal ValueStopwatch LastChokedTime;

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
        internal long GetDownloadRate()
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
