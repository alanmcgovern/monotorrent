using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;
using System.Net.Sockets;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Instance methods of this class are threadsafe
    /// </summary>
    public class ListenManager : IDisposable
    {
        #region old members

        private EncryptorReadyHandler onEncryptorReadyHandler;
        private EncryptorIOErrorHandler onEncryptorIOErrorHandler;
        private EncryptorEncryptionErrorHandler onEncryptorEncryptionErrorHandler;
        private AsyncCallback peerHandshakeReceived; // The callback to invoke when we receive a peer handshake.

        #endregion old members


        #region Member Variables

        private object locker;
        private ClientEngine engine;
        private MonoTorrentCollection<ConnectionListenerBase> listeners;

        #endregion Member Variables


        #region Properties

        public MonoTorrentCollection<ConnectionListenerBase> Listeners
        {
            get { return listeners; }
        }

        internal object Locker
        {
            get { return locker; }
            private set { locker = value; }
        }

        internal ClientEngine Engine
        {
            get { return engine; }
            private set { engine = value; }
        }

        #endregion Properties


        #region Constructors

        internal ListenManager(ClientEngine engine)
        {
            Engine = engine;
            Locker = new object();
            listeners = new MonoTorrentCollection<ConnectionListenerBase>();
            listeners.IsReadOnly = true;
            peerHandshakeReceived = new AsyncCallback(onPeerHandshakeReceived);
            this.onEncryptorReadyHandler = new EncryptorReadyHandler(onEncryptorReady);
            this.onEncryptorIOErrorHandler = new EncryptorIOErrorHandler(onEncryptorError);
            this.onEncryptorEncryptionErrorHandler = new EncryptorEncryptionErrorHandler(onEncryptorError);
            this.peerHandshakeReceived = new AsyncCallback(this.onPeerHandshakeReceived);
        }

        #endregion Constructors


        #region Public Methods

        public void Dispose()
        {
            for (int i = 0; i < listeners.Count; i++)
                listeners[i].Dispose();
        }

        public void Register(ConnectionListenerBase listener)
        {
            lock (Locker)
            {
                if (listener.Engine != null)
                    throw new ListenerException("This listener is registered to a different engine");

                if (listeners.Contains(listener))
                    throw new ListenerException("This listener has already been registered with the manager");

                listener.Engine = engine;
                listeners.Add(listener, true);
                listener.ConnectionReceived += new EventHandler<NewConnectionEventArgs>(ConnectionReceived);
            }
        }

        public void Unregister(ConnectionListenerBase listener)
        {
            lock (Locker)
            {
                if (listener.Engine != this.engine)
                    throw new ListenerException("This listener is registered to a different engine");

                if (!listeners.Contains(listener))
                    throw new ListenerException("This listener has not been registered with the manager");

                listener.Engine = null;
                listeners.Remove(listener, true);
                listener.ConnectionReceived -= new EventHandler<NewConnectionEventArgs>(ConnectionReceived);
            }
        }

        #endregion Public Methods




        private void ConnectionReceived(object sender, NewConnectionEventArgs e)
        {
            PeerIdInternal id = new PeerIdInternal(e.Peer, null);
            ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, 68);
            id.Peer.Connection.BytesReceived = 0;
            id.Peer.Connection.BytesToRecieve = 68;
            Logger.Log(id, "CE Peer incoming connection accepted");
            id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, peerHandshakeReceived, id, out id.ErrorCode);
        }


        private void handleHandshake(PeerIdInternal id)
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
                    // Maybe this was a Message Stream Encryption handshake. Parse it as such.b
                    byte[][] sKeys;
                    using (new ReaderLock(engine.torrentsLock))
                    {
                        sKeys = new byte[engine.Torrents.Count][];
                        for (int i = 0; i < engine.Torrents.Count; i++)
                            sKeys[i] = engine.Torrents[i].Torrent.infoHash;
                    }
                    id.Peer.Connection.Encryptor = new PeerBEncryption(sKeys, engine.Settings.MinEncryptionLevel);
                    id.Peer.Connection.Encryptor.SetPeerConnectionID(id);
                    id.Peer.Connection.Encryptor.EncryptorReady += onEncryptorReadyHandler;
                    id.Peer.Connection.Encryptor.EncryptorIOError += onEncryptorIOErrorHandler;
                    id.Peer.Connection.Encryptor.EncryptorEncryptionError += onEncryptorEncryptionErrorHandler;
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

#warning FIXME: Don't stop the message loop until Dispose() and track all incoming connections
            if (man == null || man.State == TorrentState.Stopped)        // We're not hosting that torrent
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

            ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, handshake.ByteLength + bf.ByteLength);
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
        private void onEncryptorReady(PeerIdInternal id)
        {
            try
            {
                id.Peer.Connection.BytesReceived = 0;
                id.Peer.Connection.BytesToRecieve = 68;
                Logger.Log(id, "CE Peer encryption handshake complete");
                int bytesReceived = 0;

                // Handshake was probably delivered as initial payload. Retrieve it if its' vailable
                if (id.Peer.Connection.Encryptor.IsInitialDataAvailable())
                    bytesReceived = id.Peer.Connection.Encryptor.GetInitialData(id.Peer.Connection.recieveBuffer.Array, id.Peer.Connection.recieveBuffer.Offset, id.Peer.Connection.BytesToRecieve);

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


        private void onEncryptorError(PeerIdInternal id)
        {
            CleanupSocket(id);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(IAsyncResult result)
        {
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;

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
        /// 
        /// </summary>
        /// <param name="id"></param>
        private void CleanupSocket(PeerIdInternal id)
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
    }
}
