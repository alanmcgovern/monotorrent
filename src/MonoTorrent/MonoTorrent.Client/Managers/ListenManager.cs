using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;
using System.Net.Sockets;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Tasks;

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
        private AsyncCallback endCheckEncryptionCallback;

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
            endCheckEncryptionCallback = EndCheckEncryption;
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
                List<byte[]> skeys = new List<byte[]>();

                MainLoop.QueueWait(delegate {
                    for (int i = 0; i < engine.Torrents.Count; i++)
                        skeys.Add(engine.Torrents[i].Torrent.InfoHash);
                });

                EncryptorFactory.BeginCheckEncryption(id, endCheckEncryptionCallback, id, skeys.ToArray());
            }
            else
                MainLoop.Queue(delegate { id.ConnectionManager.ProcessFreshConnection(id); });
        }

        private void EndCheckEncryption(IAsyncResult result)
        {
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;
            try
            {
                byte[] initialData;
                EncryptorFactory.EndCheckEncryption(result, out initialData);

                if(initialData == null)
                    initialData = new byte[0];
                    
                id.Connection.BytesReceived += Message.Write(id.Connection.recieveBuffer.Array, id.Connection.recieveBuffer.Offset, initialData);

                if (id.Connection.BytesToRecieve == id.Connection.BytesReceived)
                    handleHandshake(id);
                else
                    NetworkIO.EnqueueReceive(id.Connection.Connection, id.Connection.recieveBuffer, initialData.Length, id.Connection.BytesToRecieve - id.Connection.BytesReceived, onPeerHandshakeReceived, id);
            }
            catch
            {
                CleanupSocket(id);
            }
        }


        private void handleHandshake(PeerIdInternal id)
        {
            TorrentManager man = null;
            HandshakeMessage handshake = new HandshakeMessage();
            try
            {
                // Nasty hack - If there is initial data on the connection, it's already decrypted
                // If there was no initial data, we need to decrypt it here
                handshake.Decode(id.Connection.recieveBuffer, 0, id.Connection.BytesToRecieve);
                if (handshake.ProtocolString != VersionInfo.ProtocolStringV100)
                {
                    id.Connection.Decryptor.Decrypt(id.Connection.recieveBuffer.Array, id.Connection.recieveBuffer.Offset, id.Connection.BytesToRecieve);
                    handshake.Decode(id.Connection.recieveBuffer, 0, id.Connection.BytesToRecieve);
                }

                if (handshake.ProtocolString != VersionInfo.ProtocolStringV100)
                    throw new ProtocolException("Invalid protocol string in handshake");
            }
            catch(Exception ex)
            {
                Logger.Log(id.Connection.Connection, ex.Message);
                CleanupSocket(id);
                return;
            }

            MainLoop.QueueWait(delegate {
                for (int i = 0; i < engine.Torrents.Count; i++)
                    if (Toolbox.ByteMatch(handshake.infoHash, engine.Torrents[i].Torrent.InfoHash))
                        man = engine.Torrents[i];
            });

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
            if ((id.Connection.Encryptor is PlainTextEncryption && !Toolbox.HasEncryption(engine.Settings.AllowedEncryption, EncryptionTypes.PlainText)) && ClientEngine.SupportsEncryption)
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
            id.Connection.Encryptor.Encrypt(id.Connection.sendBuffer.Array, id.Connection.sendBuffer.Offset, id.Connection.BytesToSend);

            Logger.Log(id.Connection.Connection, "ListenManager - Sending connection to torrent manager");
            NetworkIO.EnqueueSend(id.Connection.Connection, id.Connection.sendBuffer, 0, id.Connection.BytesToSend,
                                    engine.ConnectionManager.IncomingConnectionAccepted, id);
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
                int read = id.Connection.Connection.EndReceive(result);
                if (read == 0)
                {
                    CleanupSocket(id);
                    return;
                }
                id.Connection.BytesReceived += read;
                Logger.Log(id.Connection.Connection, "ListenManager - Recieved handshake. Beginning to handle");

                handleHandshake(id);
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

            Logger.Log(id.Connection.Connection, "ListenManager - Cleaning up socket");
            if (id.Connection != null)
            {
                ClientEngine.BufferManager.FreeBuffer(ref id.Connection.recieveBuffer);
                ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
                id.Connection.Connection.Dispose();
            }
            else
            {
                Logger.Log(id.Connection.Connection, "!!!!!!!!!!CE Already null!!!!!!!!");
            }
        }
    }
}
