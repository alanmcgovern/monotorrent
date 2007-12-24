 //
// ConnectionListener.cs
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
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Accepts incoming connections and passes them off to the right TorrentManager
    /// </summary>
    public class SocketListener : ConnectionListenerBase
    {
        #region Member Variables

        private bool disposed;
        private AsyncCallback endAcceptCallback;
        private IPEndPoint listenEndPoint;
        private Socket listener;

        #endregion


        #region Properties

        public bool Disposed
        {
            get { return disposed; }
            private set
            {
                disposed = value;
                if (disposed)
                    IsListening = false;
            }
        }

        /// <summary>
        /// The Endpoint the listener should listen for connections on
        /// </summary>
        public IPEndPoint ListenEndPoint
        {
            get { return listenEndPoint; }
        }

        #endregion


        #region Constructors

        public SocketListener(IPEndPoint endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            this.listenEndPoint = endpoint;
            this.endAcceptCallback = new AsyncCallback(EndAccept);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Begin listening for incoming connections
        /// </summary>
        public override void Start()
        {
            if (engine == null)
                throw new ListenerException("This listener hasn't been registered with a torrent manager");

            if (Disposed)
                throw new ObjectDisposedException(ToString());

            if (IsListening)
                return;

            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(listenEndPoint);
            listener.Listen(6);
            listener.BeginAccept(endAcceptCallback, this.listener);
            IsListening = true;
        }


        /// <summary>
        /// Stop listening for incoming connections
        /// </summary>
        public override void Stop()
        {
            if (!IsListening)
                return;

            IsListening = false;
            this.listener.Close();
        }

        #endregion


        #region Private/Internal Methods
        public override void Dispose()
        {
            // Already disposed?
            if (Disposed)
                return;

            listener.Close();
            Disposed = true;
        }

        private void EndAccept(IAsyncResult result)
        {
            Socket peerSocket = null;
            try
            {
                // If disposed, don't call EndAccept
                if (Disposed || !IsListening)
                    return;

                peerSocket = listener.EndAccept(result);

                Peer peer = new Peer(string.Empty, peerSocket.RemoteEndPoint.ToString());
                TCPConnection connection = new TCPConnection(peerSocket, 0, new NoEncryption());


                RaiseConnectionReceived(peer, connection);
            }
            catch (SocketException)
            {
                // Just dump the connection
                if (peerSocket != null)
                    peerSocket.Close();
            }
            catch (ObjectDisposedException)
            {
                // We've stopped listening
            }
            finally
            {
                try
                {
                    if (!Disposed && IsListening)
                        listener.BeginAccept(endAcceptCallback, null);
                }
                catch(ObjectDisposedException)
                {
                }
            }
        }

        #endregion Private/Internal Methods
    }
}