//
// PeerConnectionBase.cs
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
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Libtorrent;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Holds the data for a connection to another peer
    /// </summary>
    internal class PeerConnectionBase
    {
        #region Member Variables

		public IConnection Connection;
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
        private PeerMessage currentlySendingMessage;
        private IEncryption decryptor;
        private IEncryption encryptor;
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
        private int piecesSent;
        private ushort port;
        private bool processingQueue;
        internal ArraySegment<byte> recieveBuffer = BufferManager.EmptyBuffer;      // The byte array used to buffer data while it's being received
        internal ArraySegment<byte> sendBuffer = BufferManager.EmptyBuffer;         // The byte array used to buffer data before it's sent
        private Queue<PeerMessage> sendQueue;                  // This holds the peermessages waiting to be sent
        private MonoTorrentCollection<int> suggestedPieces;
        private bool supportsFastPeer;
        private bool supportsLTMessages;


        private DateTime? lastUnchoked = null;        //When this peer was last unchoked, or null if we haven't unchoked it yet
        private long bytesDownloadedAtLastReview = 0; //Number of bytes downloaded when this peer was last reviewed - allows us to determine number of bytes
        //downloaded during a review period
        private long bytesUploadedAtLastReview = 0;   //Ditto for uploaded bytes
        private double lastReviewDownloadRate = 0;    //Download rate determined at the end of the last full review period when this peer was unchoked
        private double lastReviewUploadRate = 0;      //Ditto for upload rate
        private bool firstReviewPeriod;               //Set true if this is the first review period since this peer was last unchoked

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


        /// <summary>
        /// This is the message we're currently sending to a peer
        /// </summary>
        internal PeerMessage CurrentlySendingMessage
        {
            get { return this.currentlySendingMessage; }
            set { this.currentlySendingMessage = value; }
        }


        public IEncryption Decryptor
        {
            get { return this.decryptor; }
            set { this.decryptor = value; }
        }


        public IEncryption Encryptor
        {
            get { return this.encryptor; }
            set { this.encryptor = value; }
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

        internal MonoTorrentCollection<ExtensionSupport> ExtensionSupports
        {
            get { return extensionSupports; }
            set { extensionSupports = value; }
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
        /// The number of pieces that we've sent the peer.
        /// </summary>
        public int PiecesSent
        {
            get { return this.piecesSent; }
            internal set { this.piecesSent = value; }
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
        /// When this peer was last unchoked, or null if we haven't unchoked it yet
        /// </summary>
        public DateTime? LastUnchoked
        {
            get { return this.lastUnchoked; }
            set { this.lastUnchoked = value; }
        }


        /// <summary>
        /// Number of bytes downloaded when this peer was last reviewed
        /// </summary>
        public long BytesDownloadedAtLastReview
        {
            get { return this.bytesDownloadedAtLastReview; }
            set { this.bytesDownloadedAtLastReview = value; }
        }


        /// <summary>
        /// Number of bytes uploaded when this peer was last reviewed
        /// </summary>
        public long BytesUploadedAtLastReview
        {
            get { return this.bytesUploadedAtLastReview; }
            set { this.bytesUploadedAtLastReview = value; }
        }


        /// <summary>
        /// Download rate determined at the end of the last full review period when this peer was unchoked
        /// </summary>
        public double LastReviewDownloadRate
        {
            get { return this.lastReviewDownloadRate; }
            set { this.lastReviewDownloadRate = value; }
        }


        /// <summary>
        /// Upload rate determined at the end of the last full review period when this peer was unchoked
        /// </summary>
        public double LastReviewUploadRate
        {
            get { return this.lastReviewUploadRate; }
            set { this.lastReviewUploadRate = value; }
        }


        /// <summary>
        /// True if this is the first review period since this peer was last unchoked
        /// </summary>
        public bool FirstReviewPeriod
        {
            get { return this.firstReviewPeriod; }
            internal set { this.firstReviewPeriod = value; }
        }
        #endregion Properties


        #region Constructors

        /// <summary>
        /// Creates a new connection to the peer at the specified IPEndpoint
        /// </summary>
        /// <param name="peerEndpoint">The IPEndpoint to connect to</param>
        public PeerConnectionBase(int bitfieldLength)
        {
            this.suggestedPieces = new MonoTorrentCollection<int>();
            this.amChoking = true;
            this.isChoking = true;
            this.bitField = new BitField(bitfieldLength);
            this.monitor = new ConnectionMonitor();
            this.sendQueue = new Queue<PeerMessage>(12);
            this.isAllowedFastPieces = new MonoTorrentCollection<int>();
            this.amAllowedFastPieces = new MonoTorrentCollection<int>();
            this.lastMessageReceived = DateTime.Now;
            this.lastMessageSent = DateTime.Now;
        }

        #endregion


        #region Methods

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

        #endregion


        #region Async Methods

        internal void BeginConnect(AsyncCallback peerEndCreateConnection, PeerIdInternal id)
		{
			Connection.BeginConnect(peerEndCreateConnection, id);
		}

        internal IAsyncResult BeginReceive(ArraySegment<byte> buffer, int offset, int count, SocketFlags socketFlags, AsyncCallback asyncCallback, PeerIdInternal id)
        {
            return Connection.BeginReceive(buffer.Array, buffer.Offset + offset, count, asyncCallback, id);
        }

        internal IAsyncResult BeginSend(ArraySegment<byte> buffer, int offset, int count, SocketFlags socketFlags, AsyncCallback asyncCallback, PeerIdInternal id)
        {
            // Encrypt the *entire* message exactly once.
            if (offset == 0)
                Encryptor.Encrypt(buffer.Array, buffer.Offset, buffer.Array, buffer.Offset, id.Connection.BytesToSend);

            return Connection.BeginSend(buffer.Array, buffer.Offset + offset, count, asyncCallback, id);
		}

        internal void Dispose()
		{
			Connection.Dispose();
		}

        internal void EndConnect(IAsyncResult result)
		{
			Connection.EndConnect(result);
		}

        internal int EndReceive(IAsyncResult result)
		{
			int received = Connection.EndReceive(result);
			PeerIdInternal id = (PeerIdInternal)result.AsyncState;
            byte[] buffer = id.Connection.recieveBuffer.Array;
            int offset = id.Connection.recieveBuffer.Offset + id.Connection.bytesReceived;
            Decryptor.Decrypt(buffer, offset, buffer, offset, received);
			return received;
		}

        internal int EndSend(IAsyncResult result)
		{
			PeerIdInternal id = (PeerIdInternal)result.AsyncState;
			return Connection.EndSend(result);
		}

        #endregion
    }
}
