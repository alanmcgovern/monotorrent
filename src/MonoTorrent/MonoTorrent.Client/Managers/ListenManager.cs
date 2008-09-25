using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;
using System.Net.Sockets;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Instance methods of this class are threadsafe
    /// </summary>
    public class ListenManager : IDisposable
    {
        #region old members

        private AsyncTransfer peerHandshakeReceived; // The callback to invoke when we receive a peer handshake.

        #endregion old members


        #region Member Variables

        private object locker;
        private ClientEngine engine;
        private MonoTorrentCollection<PeerListener> listeners;
        private AsyncCallback endCheckEncryptionCallback;

        #endregion Member Variables


        #region Properties

        public MonoTorrentCollection<PeerListener> Listeners
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
            listeners = new MonoTorrentCollection<PeerListener>();
            peerHandshakeReceived = onPeerHandshakeReceived;
            endCheckEncryptionCallback = EndCheckEncryption;
        }

        #endregion Constructors


        #region Public Methods

        public void Dispose()
        {
        }

        public void Register(PeerListener listener)
        {
            listener.ConnectionReceived += new EventHandler<NewConnectionEventArgs>(ConnectionReceived);
        }

        public void Unregister(PeerListener listener)
        {
            listener.ConnectionReceived -= new EventHandler<NewConnectionEventArgs>(ConnectionReceived);
        }

        #endregion Public Methods




        private void ConnectionReceived(object sender, NewConnectionEventArgs e)
        {
            if (engine.ConnectionManager.ShouldBanPeer(e.Peer))
            {
                e.Connection.Dispose();
                return;
            }
            PeerId id = new PeerId(e.Peer, e.TorrentManager);
            id.Connection = e.Connection;

            Logger.Log(id.Connection, "ListenManager - ConnectionReceived");

            if (id.Connection.IsIncoming)
            {
                ClientEngine.BufferManager.GetBuffer(ref id.recieveBuffer, 68);
                id.BytesReceived = 0;
                id.BytesToRecieve = 68;
                List<byte[]> skeys = new List<byte[]>();

                ClientEngine.MainLoop.QueueWait(delegate {
                    for (int i = 0; i < engine.Torrents.Count; i++)
                        skeys.Add(engine.Torrents[i].Torrent.InfoHash);
                });

                EncryptorFactory.BeginCheckEncryption(id, endCheckEncryptionCallback, id, skeys.ToArray());
            }
            else
                ClientEngine.MainLoop.Queue(delegate { id.ConnectionManager.ProcessFreshConnection(id); });
        }

        private void EndCheckEncryption(IAsyncResult result)
        {
            PeerId id = (PeerId)result.AsyncState;
            try
            {
                byte[] initialData;
                EncryptorFactory.EndCheckEncryption(result, out initialData);

                if(initialData == null)
                    initialData = new byte[0];
                    
                id.BytesReceived += Message.Write(id.recieveBuffer.Array, id.recieveBuffer.Offset, initialData);

                if (id.BytesToRecieve == id.BytesReceived)
                    handleHandshake(id);
                else
                    NetworkIO.EnqueueReceive(id.Connection, id.recieveBuffer, initialData.Length, id.BytesToRecieve - id.BytesReceived, onPeerHandshakeReceived, id);
            }
            catch
            {
                CleanupSocket(id);
            }
        }


        private void handleHandshake(PeerId id)
        {
            TorrentManager man = null;
            HandshakeMessage handshake = new HandshakeMessage();
            try
            {
                // Nasty hack - If there is initial data on the connection, it's already decrypted
                // If there was no initial data, we need to decrypt it here
                handshake.Decode(id.recieveBuffer, 0, id.BytesToRecieve);
                if (handshake.ProtocolString != VersionInfo.ProtocolStringV100)
                {
                    id.Decryptor.Decrypt(id.recieveBuffer.Array, id.recieveBuffer.Offset, id.BytesToRecieve);
                    handshake.Decode(id.recieveBuffer, 0, id.BytesToRecieve);
                }

                if (handshake.ProtocolString != VersionInfo.ProtocolStringV100)
                    throw new ProtocolException("Invalid protocol string in handshake");
            }
            catch(Exception ex)
            {
                Logger.Log(id.Connection, ex.Message);
                CleanupSocket(id);
                return;
            }

            ClientEngine.MainLoop.QueueWait(delegate {
                for (int i = 0; i < engine.Torrents.Count; i++)
                    if (Toolbox.ByteMatch(handshake.infoHash, engine.Torrents[i].Torrent.InfoHash))
                        man = engine.Torrents[i];
            });

            //FIXME: #warning FIXME: Don't stop the message loop until Dispose() and track all incoming connections
            if (man == null)        // We're not hosting that torrent
            {
                Logger.Log(id.Connection, "ListenManager - Handshake requested nonexistant torrent");
                CleanupSocket(id);
                return;
            }
			if (man.State == TorrentState.Stopped)
			{
				Logger.Log(id.Connection, "ListenManager - Handshake requested for torrent which is not running");
				CleanupSocket(id);
				return;
			}

            id.Peer.PeerId = handshake.PeerId;
            id.TorrentManager = man;

            // If the handshake was parsed properly without encryption, then it definitely was not encrypted. If this is not allowed, abort
            if ((id.Encryptor is PlainTextEncryption && !Toolbox.HasEncryption(engine.Settings.AllowedEncryption, EncryptionTypes.PlainText)) && ClientEngine.SupportsEncryption)
            {
                Logger.Log(id.Connection, "ListenManager - Encryption is required but was not active");
                CleanupSocket(id);
                return;
            }

            handshake.Handle(id);
            Logger.Log(id.Connection, "ListenManager - Handshake successful handled");

            ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);
            id.ClientApp = new Software(handshake.PeerId);

            handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, engine.PeerId, VersionInfo.ProtocolStringV100);
            BitfieldMessage bf = new BitfieldMessage(id.TorrentManager.Bitfield);

            ClientEngine.BufferManager.GetBuffer(ref id.sendBuffer, handshake.ByteLength + bf.ByteLength);
            id.BytesSent = 0;
            id.BytesToSend = handshake.Encode(id.sendBuffer, 0);
            id.BytesToSend += bf.Encode(id.sendBuffer, id.BytesToSend);
            id.Encryptor.Encrypt(id.sendBuffer.Array, id.sendBuffer.Offset, id.BytesToSend);

            Logger.Log(id.Connection, "ListenManager - Sending connection to torrent manager");
            AsyncTransfer callback = engine.ConnectionManager.incomingConnectionAcceptedCallback;
            NetworkIO.EnqueueSend(id.Connection, id.sendBuffer, 0, id.BytesToSend,
                                    callback, id);
            id.ProcessingQueue = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(bool succeeded, int count, object state)
        {
            PeerId id = (PeerId)state;

            try
            {
                if (!succeeded)
                {
                    CleanupSocket(id);
                    return;
                }

                int read = count;
                if (read == 0)
                {
                    CleanupSocket(id);
                    return;
                }
                id.BytesReceived += read;
                Logger.Log(id.Connection, "ListenManager - Recieved handshake. Beginning to handle");

                handleHandshake(id);
            }
            catch (NullReferenceException)
            {
                Logger.Log(id.Connection, "ListenManager - Null ref receiving handshake");
                CleanupSocket(id);
            }
            catch (Exception)
            {
                Logger.Log(id.Connection, "ListenManager - Socket exception receiving handshake");
                CleanupSocket(id);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        private void CleanupSocket(PeerId id)
        {
            if (id == null) // Sometimes onEncryptionError fires with a null id
                return;

            Logger.Log(id.Connection, "ListenManager - Cleaning up socket");
            if (id.Connection != null)
            {
                ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);
                ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);
                id.Connection.Dispose();
            }
            else
            {
                Logger.Log(id.Connection, "!!!!!!!!!!CE Already null!!!!!!!!");
            }
        }
    }
}
