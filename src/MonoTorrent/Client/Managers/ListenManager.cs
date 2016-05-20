using System;
using System.Collections.Generic;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    ///     Instance methods of this class are threadsafe
    /// </summary>
    public class ListenManager : IDisposable
    {
        #region Constructors

        internal ListenManager(ClientEngine engine)
        {
            Engine = engine;
            Listeners = new MonoTorrentCollection<PeerListener>();
            endCheckEncryptionCallback = ClientEngine.MainLoop.Wrap(EndCheckEncryption);
            handshakeReceivedCallback = (a, b, c) => ClientEngine.MainLoop.Queue(() => onPeerHandshakeReceived(a, b, c));
        }

        #endregion Constructors

        private void ConnectionReceived(object sender, NewConnectionEventArgs e)
        {
            if (Engine.ConnectionManager.ShouldBanPeer(e.Peer))
            {
                e.Connection.Dispose();
                return;
            }
            var id = new PeerId(e.Peer, e.TorrentManager);
            id.Connection = e.Connection;

            Logger.Log(id.Connection, "ListenManager - ConnectionReceived");

            if (id.Connection.IsIncoming)
            {
                var skeys = new List<InfoHash>();

                ClientEngine.MainLoop.QueueWait(delegate
                {
                    for (var i = 0; i < Engine.Torrents.Count; i++)
                        skeys.Add(Engine.Torrents[i].InfoHash);
                });

                EncryptorFactory.BeginCheckEncryption(id, HandshakeMessage.HandshakeLength, endCheckEncryptionCallback,
                    id, skeys.ToArray());
            }
            else
            {
                ClientEngine.MainLoop.Queue(delegate { Engine.ConnectionManager.ProcessFreshConnection(id); });
            }
        }

        private void EndCheckEncryption(IAsyncResult result)
        {
            var id = (PeerId) result.AsyncState;
            try
            {
                byte[] initialData;
                EncryptorFactory.EndCheckEncryption(result, out initialData);

                if (initialData != null && initialData.Length == HandshakeMessage.HandshakeLength)
                {
                    var message = new HandshakeMessage();
                    message.Decode(initialData, 0, initialData.Length);
                    handleHandshake(id, message);
                }
                else if (initialData.Length > 0)
                {
                    throw new Exception("Argh. I can't handle this scenario. It also shouldn't happen. Ever.");
                }
                else
                {
                    PeerIO.EnqueueReceiveHandshake(id.Connection, id.Decryptor, handshakeReceivedCallback, id);
                }
            }
            catch
            {
                id.Connection.Dispose();
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
            catch (Exception ex)
            {
                Logger.Log(id.Connection, ex.Message);
                id.Connection.Dispose();
                return;
            }

            ClientEngine.MainLoop.QueueWait(delegate
            {
                for (var i = 0; i < Engine.Torrents.Count; i++)
                    if (message.infoHash == Engine.Torrents[i].InfoHash)
                        man = Engine.Torrents[i];
            });

            //FIXME: #warning FIXME: Don't stop the message loop until Dispose() and track all incoming connections
            if (man == null) // We're not hosting that torrent
            {
                Logger.Log(id.Connection, "ListenManager - Handshake requested nonexistant torrent");
                id.Connection.Dispose();
                return;
            }
            if (man.State == TorrentState.Stopped)
            {
                Logger.Log(id.Connection, "ListenManager - Handshake requested for torrent which is not running");
                id.Connection.Dispose();
                return;
            }
            if (!man.Mode.CanAcceptConnections)
            {
                Logger.Log(id.Connection, "ListenManager - Current mode does not support connections");
                id.Connection.Dispose();
                return;
            }

            id.Peer.PeerId = message.PeerId;
            id.TorrentManager = man;

            // If the handshake was parsed properly without encryption, then it definitely was not encrypted. If this is not allowed, abort
            if (id.Encryptor is PlainTextEncryption &&
                !Toolbox.HasEncryption(Engine.Settings.AllowedEncryption, EncryptionTypes.PlainText) &&
                ClientEngine.SupportsEncryption)
            {
                Logger.Log(id.Connection, "ListenManager - Encryption is required but was not active");
                id.Connection.Dispose();
                return;
            }

            message.Handle(id);
            Logger.Log(id.Connection, "ListenManager - Handshake successful handled");

            id.ClientApp = new Software(message.PeerId);

            message = new HandshakeMessage(id.TorrentManager.InfoHash, Engine.PeerId, VersionInfo.ProtocolStringV100);
            var callback = Engine.ConnectionManager.incomingConnectionAcceptedCallback;
            PeerIO.EnqueueSendMessage(id.Connection, id.Encryptor, message, id.TorrentManager.UploadLimiter,
                id.Monitor, id.TorrentManager.Monitor, callback, id);
        }

        /// <summary>
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(bool succeeded, PeerMessage message, object state)
        {
            var id = (PeerId) state;

            try
            {
                if (succeeded)
                    handleHandshake(id, (HandshakeMessage) message);
                else
                    id.Connection.Dispose();
            }
            catch (Exception)
            {
                Logger.Log(id.Connection, "ListenManager - Socket exception receiving handshake");
                id.Connection.Dispose();
            }
        }

        #region Member Variables

        private readonly AsyncCallback endCheckEncryptionCallback;
        private readonly AsyncMessageReceivedCallback handshakeReceivedCallback;

        #endregion Member Variables

        #region Properties

        public MonoTorrentCollection<PeerListener> Listeners { get; }

        internal ClientEngine Engine { get; }

        #endregion Properties

        #region Public Methods

        public void Dispose()
        {
        }

        public void Register(PeerListener listener)
        {
            listener.ConnectionReceived += ConnectionReceived;
        }

        public void Unregister(PeerListener listener)
        {
            listener.ConnectionReceived -= ConnectionReceived;
        }

        #endregion Public Methods
    }
}