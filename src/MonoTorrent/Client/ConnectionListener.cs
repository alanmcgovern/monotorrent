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
    internal class ConnectionListener : IDisposable
    {
        #region Member Variables

        private ClientEngine engine;
        private bool disposed;
        private bool isListening;
        private IPEndPoint listenEndPoint;
        private AsyncCallback newConnectionCallback;
        private EncryptorReadyHandler onEncryptorReadyHandler;
        private EncryptorIOErrorHandler onEncryptorIOErrorHandler;
        private EncryptorEncryptionErrorHandler onEncryptorEncryptionErrorHandler;
        private AsyncCallback peerHandshakeReceived; // The callback to invoke when we receive a peer handshake.
        private Socket socket;

        #endregion


        #region Properties

        internal bool Disposed
        {
            get { return this.disposed; }
        }

        
        /// <summary>
        /// Returns True if the listener is listening for incoming connections.
        /// </summary>
        public bool IsListening
        {
            get { return this.isListening; }
        }


        /// <summary>
        /// The Endpoint the listener should listen for connections on
        /// </summary>
        public IPEndPoint ListenEndPoint
        {
            get { return listenEndPoint; }
        }


        /// <summary>
        /// The AsyncCallback to invoke when a new connection is Received
        /// </summary>
        public AsyncCallback NewConnectionCallback
        {
            get { return this.newConnectionCallback; }
        }

        #endregion


        #region Constructors

        public ConnectionListener(ClientEngine engine)
        {
            this.engine = engine;
            this.onEncryptorReadyHandler = new EncryptorReadyHandler(onEncryptorReady);
            this.onEncryptorIOErrorHandler = new EncryptorIOErrorHandler(onEncryptorError);
            this.onEncryptorEncryptionErrorHandler = new EncryptorEncryptionErrorHandler(onEncryptorError);
            this.peerHandshakeReceived = new AsyncCallback(this.onPeerHandshakeReceived);
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        #endregion


        #region Methods

        private void BeginAccept()
        {
            this.socket.BeginAccept(this.newConnectionCallback, this.socket);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        private void CleanupSocket(PeerId id)
        {
            if (id == null) // Sometimes onEncryptionError fires with a null id
                return;

            lock (id)
            {
                Logger.Log(id, "***********CE Cleaning up*************");
                if (id.Peer.Connection != null)
                {
                    ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);
                    ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                    id.Peer.Connection.Dispose();
                }
                else
                {
                    Logger.Log(id, "!!!!!!!!!!CE Already null!!!!!!!!");
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                throw new ObjectDisposedException(this.ToString());

            this.socket.Close();
            this.disposed = true;
        }


        private void handleHandshake(PeerId id)
        {
            TorrentManager man = null;
            bool handshakeFailed = false;

            HandshakeMessage handshake = new HandshakeMessage();
            try
            {
                handshake.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);
#warning call handshake.Handle to do this properly
                if (handshake.ProtocolString != VersionInfo.ProtocolStringV100)
                    handshakeFailed = true;
            }
            catch
            {
                handshakeFailed = true;
            }

            if (handshakeFailed)
            {
                if (id.Peer.Connection.Encryptor is NoEncryption && ClientEngine.SupportsEncryption)
                {
                    // Maybe this was a Message Stream Encryption handshake. Parse it as such.
                    id.Peer.Connection.Encryptor = new PeerBEncryption(engine.Torrents, engine.Settings.MinEncryptionLevel);
                    id.Peer.Connection.Encryptor.SetPeerConnectionID(id);
                    id.Peer.Connection.Encryptor.onEncryptorReady += onEncryptorReadyHandler;
                    id.Peer.Connection.Encryptor.onEncryptorIOError += onEncryptorIOErrorHandler;
                    id.Peer.Connection.Encryptor.onEncryptorEncryptionError += onEncryptorEncryptionErrorHandler;
                    id.Peer.Connection.StartEncryption(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);
                    return;
                }
                else
                {
                    Logger.Log(id, "CE Invalid handshake");
                    CleanupSocket(id);
                }
                return;
            }

            using (new ReaderLock(engine.torrentsLock))
                for (int i = 0; i < engine.Torrents.Count; i++)
                    if (Toolbox.ByteMatch(handshake.infoHash, engine.Torrents[i].Torrent.InfoHash))
                        man = engine.Torrents[i];

            if (man == null)        // We're not hosting that torrent
            {
                Logger.Log(id, "CE Not tracking torrent");
                CleanupSocket(id);
                return;
            }

            id.Peer.PeerId = handshake.PeerId;
            id.TorrentManager = man;

            // If the handshake was parsed properly without encryption, then it definitely was not encrypted. If this is not allowed, abort
            if ((id.Peer.Connection.Encryptor is NoEncryption && engine.Settings.MinEncryptionLevel != EncryptionType.None) && ClientEngine.SupportsEncryption)
            {
                Logger.Log(id, "CE Require crypto");
                CleanupSocket(id);
                return;
            }

            handshake.Handle(id);
            Logger.Log(id, "CE Handshake successful");

            ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);
            id.Peer.Connection.ClientApp = new Software(handshake.PeerId);

            handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, engine.PeerId, VersionInfo.ProtocolStringV100);
            BitfieldMessage bf = new BitfieldMessage(id.TorrentManager.Bitfield);

            ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, BufferType.LargeMessageBuffer);
            id.Peer.Connection.BytesSent = 0;
            id.Peer.Connection.BytesToSend = handshake.Encode(id.Peer.Connection.sendBuffer, 0);
            id.Peer.Connection.BytesToSend += bf.Encode(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesToSend);

            Logger.Log(id, "CE Sending to torrent manager");
            id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend,
                                         SocketFlags.None, new AsyncCallback(engine.ConnectionManager.IncomingConnectionAccepted),
                                         id, out id.ErrorCode);
            id.Peer.Connection.ProcessingQueue = false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void IncomingConnectionReceived(IAsyncResult result)
        {
            PeerId id = null;
            try
            {
                Socket peerSocket = ((Socket)result.AsyncState).EndAccept(result);
                if (!peerSocket.Connected)
                    return;

                Peer peer = new Peer(string.Empty, peerSocket.RemoteEndPoint.ToString());
                peer.Connection = new TCPConnection(peerSocket, 0, new NoEncryption());
                id = new PeerId(peer);
                id.Peer.Connection.ProcessingQueue = true;
                id.Peer.Connection.LastMessageSent = DateTime.Now;
                id.Peer.Connection.LastMessageReceived = DateTime.Now;

                ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, BufferType.SmallMessageBuffer);
                id.Peer.Connection.BytesReceived = 0;
                id.Peer.Connection.BytesToRecieve = 68;
                Logger.Log(id, "CE Peer incoming connection accepted");
                id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, peerHandshakeReceived, id, out id.ErrorCode);
            }
            catch (SocketException)
            {
                if (id != null)
                    this.CleanupSocket(id);
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                if (!Disposed)
                    BeginAccept();
            }
        }


        private void onEncryptorReady(PeerId id)
        {
            try
            {
                id.Peer.Connection.BytesReceived = 0;
                id.Peer.Connection.BytesToRecieve = 68;
                Logger.Log(id, "CE Peer encryption handshake complete");
                int bytesReceived = 0;

                // Handshake was probably delivered as initial payload. Retrieve it if its' vailable
                if (id.Peer.Connection.Encryptor.IsInitialDataAvailable())
                    bytesReceived = id.Peer.Connection.Encryptor.GetInitialData(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);

                id.Peer.Connection.BytesReceived += bytesReceived;
                if (id.Peer.Connection.BytesReceived != id.Peer.Connection.BytesToRecieve)
                {
                    id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived, SocketFlags.None, peerHandshakeReceived, id, out id.ErrorCode);
                    return;
                }

                // The complete handshake was in the initial payload
                Logger.Log(id, "CE Recieved Encrypted handshake");

                handleHandshake(id);
            }

            catch (SocketException)
            {
                Logger.Log(id, "CE Exception with handshake");
                CleanupSocket(id);
            }
            catch (NullReferenceException)
            {
                CleanupSocket(id);
            }
        }


        private void onEncryptorError(PeerId id)
        {
            CleanupSocket(id);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(IAsyncResult result)
        {
            PeerId id = (PeerId)result.AsyncState;

            try
            {
                lock (id)
                {
                    int bytesReceived = id.Peer.Connection.EndReceive(result, out id.ErrorCode);
                    if (bytesReceived == 0)
                    {
                        Logger.Log(id, "CE Recieved 0 for handshake");
                        CleanupSocket(id);
                        return;
                    }

                    id.Peer.Connection.BytesReceived += bytesReceived;
                    if (id.Peer.Connection.BytesReceived != id.Peer.Connection.BytesToRecieve)
                    {
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived, SocketFlags.None, peerHandshakeReceived, id, out id.ErrorCode);
                        return;
                    }
                    Logger.Log(id, "CE Recieved handshake");

                    handleHandshake(id);
                }
            }

            catch (SocketException)
            {
                Logger.Log(id, "CE Exception with handshake");
                CleanupSocket(id);
            }
            catch (NullReferenceException)
            {
                CleanupSocket(id);
            }
        }


        /// <summary>
        /// Begin listening for incoming connections
        /// </summary>
        internal void Start()
        {
            if (this.isListening)
                throw new ListenerException("The Listener is already listening");

            this.newConnectionCallback = new AsyncCallback(IncomingConnectionReceived);
            this.listenEndPoint = new IPEndPoint(IPAddress.Any, engine.Settings.ListenPort);
            this.isListening = true;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.socket.Bind(listenEndPoint);
            this.socket.Listen(10);             // FIXME: Will this break on windows XP systems?
            this.socket.BeginAccept(newConnectionCallback, this.socket);
        }


        /// <summary>
        /// Stop listening for incoming connections
        /// </summary>
        internal void Stop()
        {
            this.disposed = true;
            this.isListening = false;
            this.socket.Close();
        }

        #endregion
    }
}