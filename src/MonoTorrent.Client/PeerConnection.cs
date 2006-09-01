//
// PeerConnection.cs
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
    public class PeerConnection : IDisposable
    {
        #region Member Variables
        /// <summary>
        /// The size of the send and recieve buffers. Defaults to 32bytes more than my default request size.
        /// </summary>
        private static readonly int BufferSize = ((1 << 14) + 32);


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
        /// 
        /// </summary>
        private Socket peerSocket;


        /// <summary>
        /// This holds the peermessages waiting to be sent
        /// </summary>
        private Queue<IPeerMessage> sendQueue;


        internal byte[] recieveBuffer;
        internal byte[] sendBuffer;


        /// <summary>
        /// The endpoint where the remote host is
        /// </summary>
        public IPEndPoint PeerEndpoint
        {
            get { return this.peerEndpoint; }
        }
        private IPEndPoint peerEndpoint;


        /// <summary>
        /// The connection Monitor for this peer
        /// </summary>
        public ConnectionMonitor Monitor
        {
            get { return this.monitor; }
        }
        private ConnectionMonitor monitor;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new connection to the peer at the specificed IP address and port
        /// </summary>
        /// <param name="ip">The IPAddress to connect to</param>
        /// <param name="port">The Port to connect to</param>
        public PeerConnection(string ip, int port)
            : this(new IPEndPoint(IPAddress.Parse(ip), port))
        {
        }

        /// <summary>
        /// Creates a new connection to the peer at the specified IPEndpoint
        /// </summary>
        /// <param name="peerEndpoint">The IPEndpoint to connect to</param>
        public PeerConnection(IPEndPoint peerEndpoint)
        {
            this.monitor = new ConnectionMonitor();
            this.sendQueue = new Queue<IPeerMessage>(4);
            this.sendBuffer = new byte[BufferSize];
            this.recieveBuffer = new byte[BufferSize];
            this.peerEndpoint = peerEndpoint;
            this.peerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// Creates a new connection to the peer based on the peerSocket
        /// </summary>
        /// <param name="peerSocket">The socket that the remote host connected to</param>
        public PeerConnection(Socket peerSocket)
        {
            this.monitor = new ConnectionMonitor();
            this.sendQueue = new Queue<IPeerMessage>(4);
            this.sendBuffer = new byte[BufferSize];
            this.recieveBuffer = new byte[BufferSize];
            this.peerEndpoint = (IPEndPoint)peerSocket.RemoteEndPoint;
            this.peerSocket = peerSocket;
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


        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.peerSocket.Close();
        }

        internal void Shutdown(SocketShutdown socketShutdown)
        {
            this.peerSocket.Shutdown(socketShutdown);
        }

        #endregion


        #region Async Methods
        internal void BeginSend(byte[] buffer, int offset, int count, SocketFlags socketFlags, System.AsyncCallback asyncCallback, PeerConnectionID id)
        {
            this.peerSocket.BeginSend(buffer, offset, count, socketFlags, asyncCallback, id);
        }

        internal int EndSend(System.IAsyncResult result)
        {
            return this.peerSocket.EndSend(result);
        }

        internal void BeginReceive(byte[] buffer, int offset, int count, SocketFlags socketFlags, System.AsyncCallback asyncCallback, PeerConnectionID id)
        {
            this.peerSocket.BeginReceive(buffer, offset, count, socketFlags, asyncCallback, id);
        }

        internal int EndReceive(System.IAsyncResult result)
        {
            return this.peerSocket.EndReceive(result);
        }

        internal void BeginConnect(IPEndPoint iPEndPoint, System.AsyncCallback peerEndCreateConnection, PeerConnectionID id)
        {
            this.peerSocket.BeginConnect(iPEndPoint, peerEndCreateConnection, id);
        }

        internal void EndConnect(System.IAsyncResult result)
        {
            this.peerSocket.EndConnect(result);
        }

        internal void Close()
        {
            this.peerSocket.Close();
        }
        #endregion

    }
}
