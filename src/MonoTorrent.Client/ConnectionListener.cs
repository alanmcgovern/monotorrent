//
// IConnectionListener.cs
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

namespace MonoTorrent.Client
{
    /// <summary>
    /// Accepts incoming connections and passes them off to the right TorrentManager
    /// </summary>
    internal class ConnectionListener : IDisposable
    {
        #region Member Variables
        private Socket socket;

        /// <summary>
        /// The Endpoint the listener should listen for connections on
        /// </summary>
        public static IPEndPoint ListenEndPoint
        {
            get { return listenEndPoint; }
        }
        private static IPEndPoint listenEndPoint;

        /// <summary>
        /// Returns True if the listener is listening for incoming connections.
        /// </summary>
        public bool IsListening
        {
            get { return this.isListening; }
        }
        private bool isListening;

        /// <summary>
        /// The AsyncCallback to invoke when a new connection is Received
        /// </summary>
        public AsyncCallback NewConnectionCallback
        {
            get { return this.newConnectionCallback; }
        }
        private AsyncCallback newConnectionCallback;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new connection listener listening at the specified port on any IP address
        /// </summary>
        /// <param name="listenPort">The port to listen on</param>
        /// <param name="newConnectionCallback">The callback to invoke when a connection is Received</param>
        public ConnectionListener(int listenPort, AsyncCallback newConnectionCallback)
            : this(listenPort, newConnectionCallback, IPAddress.Any)
        {
        }


        /// <summary>
        /// Creates a new connection listener listening at the specified port and IPAddress
        /// </summary>
        /// <param name="listenPort">The port to listen on</param>
        /// <param name="newConnectionCallback">The callback to invoke when a connection is Received</param>
        /// <param name="listenAddress">The address to listen on</param>
        public ConnectionListener(int listenPort, AsyncCallback newConnectionCallback, IPAddress listenAddress)
            : this(new IPEndPoint(listenAddress, listenPort), newConnectionCallback)
        {
        }


        /// <summary>
        /// Creates a new connection listener listening at the specified IPEndpoint
        /// </summary>
        /// <param name="endPoint">The IPEndpoint to listen at</param>
        /// <param name="newConnectionCallback">The callback to invoke when a new connection is Received</param>
        public ConnectionListener(IPEndPoint endPoint, AsyncCallback newConnectionCallback)
        {
            listenEndPoint = endPoint;
            this.newConnectionCallback = newConnectionCallback;
            this.isListening = false;
        }
        #endregion


        #region Methods
        /// <summary>
        /// Begin listening for incoming connections
        /// </summary>
        public void Start()
        {
            if (this.isListening)
                throw new ListenerException("The Listener is already listening");

            if (this.newConnectionCallback == null)
                throw new ArgumentNullException("newConnectionCallback");

            this.isListening = true;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.socket.Bind(listenEndPoint);
            this.socket.Listen(10);             // FIXME: Will this break on windows XP systems?
            this.socket.BeginAccept(newConnectionCallback, this.socket);
        }


        /// <summary>
        /// Stop listening for incoming connections
        /// </summary>
        public void Stop()
        {
            this.socket.Close();
            this.socket = null;
            this.isListening = false;
        }


        public void BeginAccept()
        {
            this.socket.BeginAccept(this.newConnectionCallback, this.socket);
        }


        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        public void Dispose(bool disposing)
        {
            if (disposed)
                throw new ObjectDisposedException(this.ToString());

            this.socket.Close();
            this.disposed = true;
        }


        internal bool Disposed
        {
            get { return this.disposed; }
        }
        private bool disposed;
        #endregion
    }
}