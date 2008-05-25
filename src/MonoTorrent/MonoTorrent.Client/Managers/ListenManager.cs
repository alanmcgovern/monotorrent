using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;
using System.Net.Sockets;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Instance methods of this class are threadsafe
    /// </summary>
    public class ListenManager : IDisposable
    {
        #region old members

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
            PeerIdInternal id = new PeerIdInternal(e.Peer, e.TorrentManager);
            id.Connection = new PeerConnectionBase(0);
            id.Connection.Connection = e.Connection;

            Logger.Log(id.Connection.Connection, "ListenManager - ConnectionReceived");

            if (id.Connection.Connection.IsIncoming)
            {
                ClientEngine.BufferManager.GetBuffer(ref id.Connection.recieveBuffer, 68);
                id.Connection.BytesReceived = 0;
                id.Connection.BytesToRecieve = 68;
                EncryptorFactory.BeginCheckEncryption(id, onPeerHandshakeReceived, id);
            }
            else
                id.ConnectionManager.ProcessFreshConnection(id);
        }


        private void handleHandshake(PeerIdInternal id)
        {
            TorrentManager man = null;
            HandshakeMessage handshake = new HandshakeMessage();
            try
            {
                handshake.Decode(id.Connection.recieveBuffer, 0, id.Connection.BytesToRecieve);
                if (handshake.ProtocolString != VersionInfo.ProtocolStringV100)
                    throw new ProtocolException("Invalid protocol string in handshake");
            }
            catch(Exception ex)
            {
                Logger.Log(id.Connection.Connection, ex.Message);
                CleanupSocket(id);
                return;
            }

            using (new ReaderLock(engine.torrentsLock))
                for (int i = 0; i < engine.Torrents.Count; i++)
                    if (Toolbox.ByteMatch(handshake.infoHash, engine.Torrents[i].Torrent.InfoHash))
                        man = engine.Torrents[i];

            //FIXME: #warning FIXME: Don't stop the message loop until Dispose() and track all incoming connections
            if (man == null)        // We're not hosting that torrent
            {
                Logger.Log(id.Connection.Connection, "ListenManager - Handshake requested nonexistant torrent");
                CleanupSocket(id);
                return;
            }
			if (man.State == TorrentState.Stopped)
			{
				Logger.Log(id.Connection.Connection, "ListenManager - Handshake requested for torrent which is not running");
				CleanupSocket(id);
				return;
			}

            id.Peer.PeerId = handshake.PeerId;
            id.TorrentManager = man;

            // If the handshake was parsed properly without encryption, then it definitely was not encrypted. If this is not allowed, abort
            if ((id.Connection.Encryptor is PlainTextEncryption && !Toolbox.HasEncryption(engine.Settings.MinEncryptionLevel, EncryptionTypes.None)) && ClientEngine.SupportsEncryption)
            {
                Logger.Log(id.Connection.Connection, "ListenManager - Encryption is required but was not active");
                CleanupSocket(id);
                return;
            }

            handshake.Handle(id);
            Logger.Log(id.Connection.Connection, "ListenManager - Handshake successful handled");

            ClientEngine.BufferManager.FreeBuffer(ref id.Connection.recieveBuffer);
            id.Connection.ClientApp = new Software(handshake.PeerId);

            handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, engine.PeerId, VersionInfo.ProtocolStringV100);
            BitfieldMessage bf = new BitfieldMessage(id.TorrentManager.Bitfield);

            ClientEngine.BufferManager.GetBuffer(ref id.Connection.sendBuffer, handshake.ByteLength + bf.ByteLength);
            id.Connection.BytesSent = 0;
            id.Connection.BytesToSend = handshake.Encode(id.Connection.sendBuffer, 0);
            id.Connection.BytesToSend += bf.Encode(id.Connection.sendBuffer, id.Connection.BytesToSend);

            Logger.Log(id.Connection.Connection, "ListenManager - Sending connection to torrent manager");
            id.Connection.BeginSend(id.Connection.sendBuffer, 0, id.Connection.BytesToSend,
                                         SocketFlags.None, new AsyncCallback(engine.ConnectionManager.IncomingConnectionAccepted),
                                         id, out id.ErrorCode);
            id.Connection.ProcessingQueue = false;
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
                    EncryptorFactory.EndCheckEncryption(result);
                    Logger.Log(id.Connection.Connection, "ListenManager - Recieved handshake. Beginning to handle");

                    handleHandshake(id);
                }
            }
            catch (NullReferenceException)
            {
                Logger.Log(id.Connection.Connection, "ListenManager - Null ref receiving handshake");
                CleanupSocket(id);
            }
            catch (Exception)
            {
                Logger.Log(id.Connection.Connection, "ListenManager - Socket exception receiving handshake");
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
                Logger.Log(id.Connection.Connection, "ListenManager - Cleaning up socket");
                if (id.Connection != null)
                {
                    ClientEngine.BufferManager.FreeBuffer(ref id.Connection.recieveBuffer);
                    ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
                    id.Connection.Dispose();
                }
                else
                {
                    Logger.Log(id.Connection.Connection, "!!!!!!!!!!CE Already null!!!!!!!!");
                }
            }
        }
    }
}
