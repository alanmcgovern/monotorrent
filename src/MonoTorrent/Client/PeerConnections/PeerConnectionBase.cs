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
    internal abstract class PeerConnectionBase : IDisposable
    {
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
        private IPeerMessageInternal currentlySendingMessage;
        private IEncryptorInternal encryptor;
        private MonoTorrentCollection<int> isAllowedFastPieces;
        private bool isChoking;
        private bool isInterested;
        private bool isinterestingtoMe;
        private int isRequestingPiecesCount;
        private DateTime lastMessageReceived;
        private DateTime lastMessageSent;
        private MessagingCallback messageReceivedCallback;
        private MessagingCallback messageSentCallback;
        private ConnectionMonitor monitor;
        private int piecesSent;
        private ushort port;
        private bool processingQueue;
        internal ArraySegment<byte> recieveBuffer = BufferManager.EmptyBuffer;      // The byte array used to buffer data while it's being received
        internal ArraySegment<byte> sendBuffer = BufferManager.EmptyBuffer;         // The byte array used to buffer data before it's sent
        private Queue<IPeerMessageInternal> sendQueue;                  // This holds the peermessages waiting to be sent
        private MonoTorrentCollection<int> suggestedPieces;
        private bool supportsFastPeer;

        #endregion Member Variables


        #region Properties

        internal abstract byte[] AddressBytes { get; }


        // FIXME Use these to request pieces
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
        internal IPeerMessageInternal CurrentlySendingMessage
        {
            get { return this.currentlySendingMessage; }
            set { this.currentlySendingMessage = value; }
        }


        /// <summary>
        /// The current encryption method being used to encrypt connections
        /// </summary>
        public IEncryptorInternal Encryptor
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


        /// <summary>
        /// A list of pieces that this peer has suggested we download. These should be downloaded
        /// with higher priority than standard pieces.
        /// </summary>
        internal MonoTorrentCollection<int> SuggestedPieces
        {
            get { return this.suggestedPieces; }
        }

        #endregion Properties


        #region Constructors

        /// <summary>
        /// Creates a new connection to the peer at the specified IPEndpoint
        /// </summary>
        /// <param name="peerEndpoint">The IPEndpoint to connect to</param>
        protected PeerConnectionBase(int bitfieldLength, IEncryptorInternal encryptor)
        {
            this.suggestedPieces = new MonoTorrentCollection<int>();
            this.encryptor = encryptor;
            this.amChoking = true;
            this.isChoking = true;
            this.bitField = new BitField(bitfieldLength);
            this.monitor = new ConnectionMonitor();
            this.sendQueue = new Queue<IPeerMessageInternal>(12);
            this.isAllowedFastPieces = new MonoTorrentCollection<int>();
            this.amAllowedFastPieces = new MonoTorrentCollection<int>();
        }

        #endregion


        #region Methods

        /// <summary>
        /// Returns the PeerMessage at the head of the queue
        /// </summary>
        /// <returns></returns>
        internal IPeerMessageInternal Dequeue()
        {
            return sendQueue.Dequeue();
        }


        /// <summary>
        /// Queues a PeerMessage up to be sent to the remote host
        /// </summary>
        /// <param name="msg"></param>
        internal void Enqueue(IPeerMessageInternal msg)
        {
            sendQueue.Enqueue(msg);
        }


        /// <summary>
        /// Enqueues a peer message at the specified index
        /// </summary>
        /// <param name="message"></param>
        /// <param name="index"></param>
        internal void EnqueueAt(IPeerMessageInternal message, int index)
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


        /// <summary>
        /// 
        /// </summary>
        internal virtual void StartEncryption()
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="initialBuffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        internal virtual void StartEncryption(ArraySegment<byte> initialBuffer, int offset, int count)
        {
        }

        #endregion


        #region Async Methods

        internal abstract void BeginConnect(System.AsyncCallback peerEndCreateConnection, PeerIdInternal id);

        internal abstract void BeginReceive(ArraySegment<byte> buffer, int offset, int count, SocketFlags socketFlags, AsyncCallback asyncCallback, PeerIdInternal id, out SocketError errorCode);

        internal abstract void BeginSend(ArraySegment<byte> buffer, int offset, int count, SocketFlags socketFlags, AsyncCallback asyncCallback, PeerIdInternal id, out SocketError errorCode);

        internal abstract void Dispose();

        void IDisposable.Dispose()
        {
            Dispose();
        }

        internal abstract void EndConnect(System.IAsyncResult result);

        internal abstract int EndReceive(System.IAsyncResult result, out SocketError errorCode);

        internal abstract int EndSend(System.IAsyncResult result, out SocketError errorCode);

        #endregion
    }
}
