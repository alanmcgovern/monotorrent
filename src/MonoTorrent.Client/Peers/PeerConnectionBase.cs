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



using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Client.PeerMessages;
using System;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Holds the data for a connection to another peer
    /// </summary>
    public abstract class PeerConnectionBase : IDisposable
    {
        #region Member Variables
        /// <summary>
        /// The size of the send and recieve buffers. Defaults to 32bytes more than my default request size.
        /// </summary>
        private static readonly int BufferSize = ((1 << 14) + 32);
        public DateTime LastMessageSent
        {
            get { return this.lastMessageSent; }
            internal set { this.lastMessageSent = value; }
        }
        private DateTime lastMessageSent;


        public DateTime LastMessageRecieved
        {
            get { return this.lastMessageRecieved; }
            internal set { this.lastMessageRecieved = value; }
        }
        private DateTime lastMessageRecieved;
        /// <summary>
        /// The peers bitfield
        /// </summary>
        public BitField BitField
        {
            get { return this.bitField; }
            set { this.bitField = value; }
        }
        private BitField bitField;


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
        /// 
        /// </summary>
        public int IsRequestingPiecesCount
        {
            get { return this.isRequestingPiecesCount; }
            set { this.isRequestingPiecesCount = value; }
        }
        private int isRequestingPiecesCount;


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
        /// True if we are currently processing the peers message queue
        /// </summary>
        public bool ProcessingQueue
        {
            get { return this.processingQueue; }
            set { this.processingQueue = value; }
        }
        private bool processingQueue;

        /// <summary>
        /// The total bytes to send from the buffer
        /// </summary>
        public int BytesToSend
        {
            get { return this.bytesToSend; }
            set { this.bytesToSend = value; }
        }
        private int bytesToSend;


        /// <summary>
        /// The total number of bytes sent from the current send buffer
        /// </summary>
        public int BytesSent
        {
            get { return this.bytesSent; }
            set { this.bytesSent = value; }
        }
        private int bytesSent;


        /// <summary>
        /// The total number of bytes recieved into the current recieve buffer
        /// </summary>
        public int BytesRecieved
        {
            get { return this.bytesRecieved; }
            set { this.bytesRecieved = value; }
        }
        private int bytesRecieved;


        /// <summary>
        /// The total number of bytes to receive
        /// </summary>
        public int BytesToRecieve
        {
            get { return this.bytesToRecieve; }
            set { this.bytesToRecieve = value; }
        }
        private int bytesToRecieve;


        /// <summary>
        /// This holds the peermessages waiting to be sent
        /// </summary>
        private Queue<IPeerMessage> sendQueue;


        internal byte[] recieveBuffer;
        internal byte[] sendBuffer;


        /// <summary>
        /// The connection Monitor for this peer
        /// </summary>
        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }
        private ConnectionMonitor monitor;


        public bool AmInterested
        {
            get { return this.amInterested; }
            internal set { this.amInterested = value; }
        }
        private bool amInterested;


        public bool AmChoking
        {
            get { return this.amChoking; }
            internal set { this.amChoking = value; }
        }
        private bool amChoking;


        public bool IsChoking
        {
            get { return this.isChoking; }
            internal set { this.isChoking = value; }
        }
        private bool isChoking;


        public bool IsInterested
        {
            get { return this.isInterested; }
            internal set { this.isInterested = value; }
        }
        private bool isInterested;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new connection to the peer at the specified IPEndpoint
        /// </summary>
        /// <param name="peerEndpoint">The IPEndpoint to connect to</param>
        public PeerConnectionBase(int bitfieldLength)
        {
            this.amChoking = true;
            this.isChoking = true;
            this.bitField = new BitField(bitfieldLength);
            this.monitor = new ConnectionMonitor();
            this.sendQueue = new Queue<IPeerMessage>(4);
            this.sendBuffer = new byte[BufferSize];
            this.recieveBuffer = new byte[BufferSize];
        }
        #endregion


        #region Methods
        /// <summary>
        /// Queues a PeerMessage up to be sent to the remote host
        /// </summary>
        /// <param name="msg"></param>
        public void EnQueue(IPeerMessage msg)
        {
            sendQueue.Enqueue(msg);
        }


        /// <summary>
        /// Returns the PeerMessage at the head of the queue
        /// </summary>
        /// <returns></returns>
        public IPeerMessage DeQueue()
        {
            return sendQueue.Dequeue();
        }


        /// <summary>
        /// The length of the Message queue
        /// </summary>
        public int QueueLength
        {
            get { return this.sendQueue.Count; }
        }
        #endregion


        #region Async Methods
        internal abstract void BeginConnect(System.AsyncCallback peerEndCreateConnection, PeerConnectionID id);
        
        internal abstract void BeginReceive(byte[] buffer, int offset, int count, SocketFlags socketFlags, System.AsyncCallback asyncCallback, PeerConnectionID id);
        
        internal abstract void BeginSend(byte[] buffer, int offset, int count, SocketFlags socketFlags, System.AsyncCallback asyncCallback, PeerConnectionID id);

        internal abstract void EndConnect(System.IAsyncResult result);
        
        internal abstract int EndReceive(System.IAsyncResult result);

        internal abstract int EndSend(System.IAsyncResult result);

        public abstract void Dispose();
        #endregion

    }
}
