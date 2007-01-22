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
using MonoTorrent.Client.PeerMessages;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Holds the data for a connection to another peer
    /// </summary>
    public abstract class PeerConnectionBase : IDisposable
    {
        #region Member Variables
#warning Use these to request pieces
        /// <summary>
        /// Contains the indexs of all the pieces we can request even if choked
        /// </summary>
        internal List<int> AllowedFastPieces
        {
            get { return this.allowedFastPieces; }
        }
        private List<int> allowedFastPieces;


        /// <summary>
        /// True if we are currently choking the peer
        /// </summary>
        public bool AmChoking
        {
            get { return this.amChoking; }
            internal set { this.amChoking = value; }
        }
        private bool amChoking;


        /// <summary>
        /// True if the peer has some pieces that we need
        /// </summary>
        public bool AmInterested
        {
            get { return this.amInterested; }
            internal set { this.amInterested = value; }
        }
        private bool amInterested;


        /// <summary>
        /// 
        /// </summary>
        public int AmRequestingPiecesCount
        {
            get { return this.amRequestingPiecesCount; }
            set { this.amRequestingPiecesCount = value; }
        }
        private int amRequestingPiecesCount;


        /// <summary>
        /// The peers bitfield
        /// </summary>
        internal BitField BitField
        {
            get { return this.bitField; }
            set { this.bitField = value; }
        }
        private BitField bitField;


        /// <summary>
        /// The total number of bytes Received into the current recieve buffer
        /// </summary>
        internal int BytesReceived
        {
            get { return this.bytesReceived; }
            set { this.bytesReceived = value; }
        }
        private int bytesReceived;


        /// <summary>
        /// The total number of bytes sent from the current send buffer
        /// </summary>
        internal int BytesSent
        {
            get { return this.bytesSent; }
            set { this.bytesSent = value; }
        }
        private int bytesSent;


        /// <summary>
        /// The total number of bytes to receive
        /// </summary>
        internal int BytesToRecieve
        {
            get { return this.bytesToRecieve; }
            set { this.bytesToRecieve = value; }
        }
        private int bytesToRecieve;


        /// <summary>
        /// The total bytes to send from the buffer
        /// </summary>
        internal int BytesToSend
        {
            get { return this.bytesToSend; }
            set { this.bytesToSend = value; }
        }
        private int bytesToSend;


        /// <summary>
        /// Contains the version and name of the application this client is using.
        /// </summary>
        public PeerID ClientApp
        {
            get { return this.clientApp; }
            internal set { this.clientApp = value; }
        }
        private PeerID clientApp;


        /// <summary>
        /// This is the message we're currently sending to a peer
        /// </summary>
        internal IPeerMessageInternal CurrentlySendingMessage
        {
            get { return this.currentlySendingMessage; }
            set { this.currentlySendingMessage = value; }
        }
        private IPeerMessageInternal currentlySendingMessage;


        /// <summary>
        /// The current encryption method being used to encrypt connections
        /// </summary>
        internal IEncryptor Encryptor
        {
            get { return this.encryptor; }
            set { this.encryptor = value; }
        }
        private IEncryptor encryptor;


        /// <summary>
        /// True if the peer is currently choking us
        /// </summary>
        public bool IsChoking
        {
            get { return this.isChoking; }
            internal set { this.isChoking = value; }
        }
        private bool isChoking;


        /// <summary>
        /// True if the peer is currently interested in us
        /// </summary>
        public bool IsInterested
        {
            get { return this.isInterested; }
            internal set { this.isInterested = value; }
        }
        private bool isInterested;


        // True if the peer has pieces that i might like to request. If he is interesting to me
        // i need to send an InterestedMessage.
        internal bool IsInterestingToMe
        {
            get { return this.isinterestingtoMe; }
            set { this.isinterestingtoMe = value; }
        }
        private bool isinterestingtoMe;


        /// <summary>
        /// 
        /// </summary>
        public int IsRequestingPiecesCount
        {
            get { return this.isRequestingPiecesCount; }
            set { this.isRequestingPiecesCount = value; }
        }
        private int isRequestingPiecesCount;


        /// <summary>
        /// The time at which the last message was received at
        /// </summary>
        internal DateTime LastMessageReceived
        {
            get { return this.lastMessageReceived; }
            set { this.lastMessageReceived = value; }
        }
        private DateTime lastMessageReceived;


        /// <summary>
        /// The time at which the last message was sent at
        /// </summary>
        internal DateTime LastMessageSent
        {
            get { return this.lastMessageSent; }
            set { this.lastMessageSent = value; }
        }
        private DateTime lastMessageSent;


        /// <summary>
        /// The connection Monitor for this peer
        /// </summary>
        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }
        private ConnectionMonitor monitor;


        /// <summary>
        /// The number of pieces that we've sent the peer.
        /// </summary>
        public int PiecesSent
        {
            get { return this.piecesSent; }
            internal set { this.piecesSent = value; }
        }
        private int piecesSent;


        /// <summary>
        /// The port the peer is listening on for DHT
        /// </summary>
        internal ushort Port
        {
            get { return this.port; }
            set { this.port = value; }
        }
        private ushort port;


        /// <summary>
        /// True if we are currently processing the peers message queue
        /// </summary>
        internal bool ProcessingQueue
        {
            get { return this.processingQueue; }
            set { this.processingQueue = value; }
        }
        private bool processingQueue;


        /// <summary>
        /// The byte array used to buffer data while it's being received
        /// </summary>
        internal byte[] recieveBuffer = BufferManager.EmptyBuffer;


        /// <summary>
        /// The byte array used to buffer data before it's sent
        /// </summary>
        internal byte[] sendBuffer = BufferManager.EmptyBuffer;


        /// <summary>
        /// This holds the peermessages waiting to be sent
        /// </summary>
        private Queue<IPeerMessageInternal> sendQueue;


        /// <summary>
        /// True if the peer supports the Fast Peers extension
        /// </summary>
        public bool SupportsFastPeer
        {
            get { return this.supportsFastPeer; }
            internal set { this.supportsFastPeer = value; }
        }
        private bool supportsFastPeer;


        /// <summary>
        /// A list of pieces that this peer has suggested we download. These should be downloaded
        /// with higher priority than standard pieces.
        /// </summary>
        internal List<int> SuggestedPieces
        {
            get { return this.suggestedPieces; }
        }
        private List<int> suggestedPieces;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new connection to the peer at the specified IPEndpoint
        /// </summary>
        /// <param name="peerEndpoint">The IPEndpoint to connect to</param>
        protected PeerConnectionBase(int bitfieldLength, IEncryptor encryptor)
        {
            this.suggestedPieces = new List<int>();
            this.encryptor = encryptor;
            this.amChoking = true;
            this.isChoking = true;
            this.allowedFastPieces = new List<int>();
            this.bitField = new BitField(bitfieldLength);
            this.monitor = new ConnectionMonitor();
            this.sendQueue = new Queue<IPeerMessageInternal>(12);
        }
        #endregion


        #region Methods
        /// <summary>
        /// Queues a PeerMessage up to be sent to the remote host
        /// </summary>
        /// <param name="msg"></param>
        internal void EnQueue(IPeerMessageInternal msg)
        {
            sendQueue.Enqueue(msg);
        }


        /// <summary>
        /// Returns the PeerMessage at the head of the queue
        /// </summary>
        /// <returns></returns>
        internal IPeerMessageInternal DeQueue()
        {
            return sendQueue.Dequeue();
        }


        /// <summary>
        /// The length of the Message queue
        /// </summary>
        internal int QueueLength
        {
            get { return this.sendQueue.Count; }
        }
        #endregion


        #region Async Methods
        internal abstract void BeginConnect(System.AsyncCallback peerEndCreateConnection, PeerConnectionID id);

        internal abstract void BeginReceive(byte[] buffer, int offset, int count, SocketFlags socketFlags, System.AsyncCallback asyncCallback, PeerConnectionID id, out SocketError errorCode);

        internal abstract void BeginSend(byte[] buffer, int offset, int count, SocketFlags socketFlags, System.AsyncCallback asyncCallback, PeerConnectionID id, out SocketError errorCode);

        internal abstract void EndConnect(System.IAsyncResult result);

        internal abstract int EndReceive(System.IAsyncResult result, out SocketError errorCode);

        internal abstract int EndSend(System.IAsyncResult result, out SocketError errorCode);

        public abstract void Dispose();
        #endregion

    }
}