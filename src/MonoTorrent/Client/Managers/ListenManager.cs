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
        #region Member Variables

        private ClientEngine engine;
        private MonoTorrentCollection<PeerListener> listeners;
        private AsyncCallback endCheckEncryptionCallback;
        private AsyncMessageReceivedCallback handshakeReceivedCallback;

        #endregion Member Variables


        #region Properties

        public MonoTorrentCollection<PeerListener> Listeners
        {
            get { return listeners; }
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
            listeners = new MonoTorrentCollection<PeerListener>();
            endCheckEncryptionCallback = ClientEngine.MainLoop.Wrap(EndCheckEncryption);
            handshakeReceivedCallback = (a, b, c) => ClientEngine.MainLoop.Queue(() => onPeerHandshakeReceived(a, b, c));
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
                List<InfoHash> skeys = new List<InfoHash>();

                ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                    for (int i = 0; i < engine.Torrents.Count; i++)
                        skeys.Add(engine.Torrents[i].InfoHash);
                });

                EncryptorFactory.BeginCheckEncryption(id, HandshakeMessage.HandshakeLength, endCheckEncryptionCallback, id, skeys.ToArray());
            }
            else
            {
                ClientEngine.MainLoop.Queue(delegate { engine.ConnectionManager.ProcessFreshConnection(id); });
            }
        }

        private void EndCheckEncryption(IAsyncResult result)
        {
            PeerId id = (PeerId)result.AsyncState;
            try
            {
                byte[] initialData;
                EncryptorFactory.EndCheckEncryption(result, out initialData);

                if (initialData != null && initialData.Length == HandshakeMessage.HandshakeLength) {
                    HandshakeMessage message = new HandshakeMessage ();
                    message.Decode (initialData, 0, initialData.Length);
                    handleHandshake(id, message);
                } else if (initialData.Length > 0) {
                    throw new Exception ("Argh. I can't handle this scenario. It also shouldn't happen. Ever.");
                } else {
                    PeerIO.EnqueueReceiveHandshake (id.Connection, id.Decryptor, handshakeReceivedCallback, id);
                }
            }
            catch
            {
                id.Connection.Dispose ();
            }
        }


        private void handleHandshake(PeerId id, HandshakeMessage message)
        {
            TorrentManager man = null;
            try
            {
                if (message.ProtocolString != VersionInfo.ProtocolStringV100)
                    throw new ProtocolException("Invalid protocol string in handshake");
            }
            catch(Exception ex)
            {
                Logger.Log(id.Connection, ex.Message);
                id.Connection.Dispose ();
                return;
            }

            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                for (int i = 0; i < engine.Torrents.Count; i++)
                    if (message.infoHash == engine.Torrents[i].InfoHash)
                        man = engine.Torrents[i];
            });

            //FIXME: #warning FIXME: Don't stop the message loop until Dispose() and track all incoming connections
            if (man == null)        // We're not hosting that torrent
            {
                Logger.Log(id.Connection, "ListenManager - Handshake requested nonexistant torrent");
                id.Connection.Dispose ();
                return;
            }
			if (man.State == TorrentState.Stopped)
			{
				Logger.Log(id.Connection, "ListenManager - Handshake requested for torrent which is not running");
				id.Connection.Dispose ();
				return;
			}
            if (!man.Mode.CanAcceptConnections)
            {
                Logger.Log(id.Connection, "ListenManager - Current mode does not support connections");
                id.Connection.Dispose ();
                return;
            }

            id.Peer.PeerId = message.PeerId;
            id.TorrentManager = man;

            // If the handshake was parsed properly without encryption, then it definitely was not encrypted. If this is not allowed, abort
            if ((id.Encryptor is PlainTextEncryption && !Toolbox.HasEncryption(engine.Settings.AllowedEncryption, EncryptionTypes.PlainText)) && ClientEngine.SupportsEncryption)
            {
                Logger.Log(id.Connection, "ListenManager - Encryption is required but was not active");
                id.Connection.Dispose ();
                return;
            }

            message.Handle(id);
            Logger.Log(id.Connection, "ListenManager - Handshake successful handled");

            id.ClientApp = new Software(message.PeerId);

            message = new HandshakeMessage(id.TorrentManager.InfoHash, engine.PeerId, VersionInfo.ProtocolStringV100);
            var callback = engine.ConnectionManager.incomingConnectionAcceptedCallback;
            PeerIO.EnqueueSendMessage (id.Connection, id.Encryptor, message, id.TorrentManager.UploadLimiter,
                                    id.Monitor, id.TorrentManager.Monitor, callback, id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(bool succeeded, PeerMessage message, object state)
        {
            PeerId id = (PeerId)state;

            try
            {
                if (succeeded)
                    handleHandshake(id, (HandshakeMessage) message);
                else
                    id.Connection.Dispose ();
            }
            catch (Exception)
            {
                Logger.Log(id.Connection, "ListenManager - Socket exception receiving handshake");
                id.Connection.Dispose ();
            }
        }
    }
}
