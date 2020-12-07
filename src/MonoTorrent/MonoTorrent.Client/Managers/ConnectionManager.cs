//
// ConnectionManager.cs
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
using System.Collections.Generic;
using System.Threading;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Logging;

using ReusableTasks;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Main controller class for all incoming and outgoing connections
    /// </summary>
    public class ConnectionManager
    {
        static readonly Logger logger = Logger.Create ();

        struct AsyncConnectState
        {
            public AsyncConnectState (TorrentManager manager, IConnection connection, ValueStopwatch timer)
            {
                Manager = manager;
                Connection = connection;
                Timer = timer;
            }

            public readonly IConnection Connection;
            public readonly TorrentManager Manager;
            public readonly ValueStopwatch Timer;
        }

        public event EventHandler<AttemptConnectionEventArgs> BanPeer;

        internal static readonly int ChunkLength = 2096 + 64;   // Download in 2kB chunks to allow for better rate limiting

        internal int openConnections;

        internal DiskManager DiskManager { get; }

        internal BEncodedString LocalPeerId { get; }

        /// <summary>
        /// The number of concurrent connection attempts
        /// </summary>
        public int HalfOpenConnections => PendingConnects.Count;

        /// <summary>
        /// The maximum number of concurrent connection attempts
        /// </summary>
        internal int MaxHalfOpenConnections => Settings.MaximumHalfOpenConnections;

        /// <summary>
        /// The maximum number of open connections
        /// </summary>
        internal int MaxOpenConnections => Settings.MaximumConnections;

        /// <summary>
        /// The number of open connections
        /// </summary>
        public int OpenConnections => openConnections;

        List<AsyncConnectState> PendingConnects { get; }

        internal EngineSettings Settings { get; set; }

        LinkedList<TorrentManager> Torrents { get; set; }

        internal ConnectionManager (BEncodedString localPeerId, EngineSettings settings, DiskManager diskManager)
        {
            DiskManager = diskManager ?? throw new ArgumentNullException (nameof (diskManager));
            LocalPeerId = localPeerId ?? throw new ArgumentNullException (nameof (localPeerId));
            Settings = settings ?? throw new ArgumentNullException (nameof (settings));

            PendingConnects = new List<AsyncConnectState> ();
            Torrents = new LinkedList<TorrentManager> ();
        }

        internal void Add (TorrentManager manager)
        {
            Torrents.AddLast (manager);
        }

        internal void Remove (TorrentManager manager)
        {
            Torrents.Remove (manager);
        }

        async void ConnectToPeer (TorrentManager manager, Peer peer)
        {
            // Connect to the peer.
            IConnection connection = ConnectionFactory.Create (peer.ConnectionUri);
            if (connection == null)
                return;

            var state = new AsyncConnectState (manager, connection, ValueStopwatch.StartNew ());
            PendingConnects.Add (state);
            manager.Peers.ConnectingToPeers.Add (peer);

            bool succeeded;
            try {
                await NetworkIO.ConnectAsync (connection);
                succeeded = true;
            } catch {
                succeeded = false;
            }

            PendingConnects.Remove (state);
            manager.Peers.ConnectingToPeers.Remove (peer);
            if (manager.Engine == null || !manager.Mode.CanAcceptConnections) {
                manager.Peers.AvailablePeers.Add (peer);
                connection.Dispose ();
                return;
            }

            try {
                if (!succeeded) {
                    peer.FailedConnectionAttempts++;
                    connection.Dispose ();
                    manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (peer, ConnectionFailureReason.Unreachable, manager));
                } else {
                    var id = new PeerId (peer, connection, manager.Bitfield?.Clone ().SetAll (false));
                    id.LastMessageReceived.Restart ();
                    id.LastMessageSent.Restart ();

                    logger.Info (id.Connection, "Connection opened");

                    ProcessNewOutgoingConnection (manager, id);
                }
            } catch {
                // FIXME: Do nothing now?
            } finally {
                // Try to connect to another peer
                TryConnect ();
            }
        }

        internal bool Contains (TorrentManager manager)
        {
            return Torrents.Contains (manager);
        }

        internal async void ProcessNewOutgoingConnection (TorrentManager manager, PeerId id)
        {
            // If we have too many open connections, close the connection
            if (OpenConnections > MaxOpenConnections) {
                CleanupSocket (manager, id);
                return;
            }

            manager.Peers.ActivePeers.Add (id.Peer);
            manager.Peers.ConnectedPeers.Add (id);
            Interlocked.Increment (ref openConnections);

            try {
                // Create a handshake message to send to the peer
                var handshake = new HandshakeMessage (manager.InfoHash, LocalPeerId, VersionInfo.ProtocolStringV100);
                EncryptorFactory.EncryptorResult result = await EncryptorFactory.CheckOutgoingConnectionAsync (id.Connection, id.Peer.AllowedEncryption, Settings, manager.InfoHash, handshake);
                id.Decryptor = result.Decryptor;
                id.Encryptor = result.Encryptor;
            } catch {
                // If an exception is thrown it's because we tried to establish an encrypted connection and something went wrong
                id.Peer.AllowedEncryption &= ~(EncryptionTypes.RC4Full | EncryptionTypes.RC4Header);

                manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (id.Peer, ConnectionFailureReason.EncryptionNegiotiationFailed, manager));
                CleanupSocket (manager, id);

                // CleanupSocket will contain the peer only if AllowedEncryption is not set to None. If
                // the peer was re-added, then we should try to reconnect to it immediately to try an
                // unencrypted connection.
                if (manager.Peers.AvailablePeers.Remove (id.Peer))
                    ConnectToPeer (manager, id.Peer);
                return;
            }

            try {
                // Receive their handshake
                HandshakeMessage handshake = await PeerIO.ReceiveHandshakeAsync (id.Connection, id.Decryptor);
                manager.Mode.HandleMessage (id, handshake);
            } catch {
                // If we choose plaintext and it resulted in the connection being closed, remove it from the list.
                id.Peer.AllowedEncryption &= ~id.EncryptionType;

                manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (id.Peer, ConnectionFailureReason.HandshakeFailed, manager));
                CleanupSocket (manager, id);

                // CleanupSocket will contain the peer only if AllowedEncryption is not set to None. If
                // the peer was re-added, then we should try to reconnect to it immediately to try an
                // encrypted connection, assuming the previous connection was unencrypted and it failed.
                if (manager.Peers.AvailablePeers.Remove (id.Peer))
                    ConnectToPeer (manager, id.Peer);

                return;
            }

            try {
                if (id.BitField.Length != manager.Bitfield.Length)
                    throw new TorrentException ($"The peer's bitfield was of length {id.BitField.Length} but the TorrentManager's bitfield was of length {manager.Bitfield.Length}.");

                manager.HandlePeerConnected (id);
                id.MessageQueue.SetReady ();
                TryProcessQueue (manager, id);

                ReceiveMessagesAsync (id.Connection, id.Decryptor, manager.DownloadLimiters, id.Monitor, manager, id);

                id.WhenConnected.Restart ();
                id.LastBlockReceived.Restart ();
            } catch {
                manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (id.Peer, ConnectionFailureReason.Unknown, manager));
                CleanupSocket (manager, id);
                return;
            }
        }

        internal async void ReceiveMessagesAsync (IConnection connection, IEncryption decryptor, RateLimiterGroup downloadLimiter, ConnectionMonitor monitor, TorrentManager torrentManager, PeerId id)
        {
            await MainLoop.SwitchToThreadpool ();

            ByteBufferPool.Releaser releaser = default;
            try {
                while (true) {
                    if (id.AmRequestingPiecesCount == 0 && releaser.Buffer != null) {
                        releaser.Dispose ();
                        releaser = NetworkIO.BufferPool.Rent (1, out ByteBuffer _);
                    } else if (id.AmRequestingPiecesCount > 0 && releaser.Buffer == null) {
                        releaser.Dispose ();
                        releaser = NetworkIO.BufferPool.Rent (Piece.BlockSize, out ByteBuffer _);
                    }
                    PeerMessage message = await PeerIO.ReceiveMessageAsync (connection, decryptor, downloadLimiter, monitor, torrentManager.Monitor, torrentManager, releaser.Buffer).ConfigureAwait (false);
                    HandleReceivedMessage (id, torrentManager, message);
                }
            } catch {
                releaser.Dispose ();
                await ClientEngine.MainLoop;
                CleanupSocket (torrentManager, id);
            }
        }

        static async void HandleReceivedMessage (PeerId id, TorrentManager torrentManager, PeerMessage message)
        {
            await ClientEngine.MainLoop;

            if (id.Disposed) {
                if (message is PieceMessage msg)
                    msg.DataReleaser.Dispose ();
            } else {
                id.LastMessageReceived.Restart ();
                torrentManager.Mode.HandleMessage (id, message);
            }
        }

        internal void CleanupSocket (TorrentManager manager, PeerId id)
        {
            if (id == null || id.Disposed) // Sometimes onEncryptoError will fire with a null id
                return;

            try {
                // We can reuse this peer if the connection says so and it's not marked as inactive
                bool canReuse = (id.Connection?.CanReconnect ?? false)
                    && !manager.InactivePeerManager.InactivePeerList.Contains (id.Uri)
                    && id.Peer.AllowedEncryption != EncryptionTypes.None
                    && !manager.Engine.PeerId.Equals (id.PeerID);

                manager.PieceManager.Picker.CancelRequests (id);
                id.Peer.CleanedUpCount++;

                id.PeerExchangeManager?.Dispose ();

                if (!id.AmChoking)
                    manager.UploadingTo--;

                if (manager.Peers.ConnectedPeers.Remove (id))
                    Interlocked.Decrement (ref openConnections);
                manager.Peers.ActivePeers.Remove (id.Peer);

                // If we get our own details, this check makes sure we don't try connecting to ourselves again
                if (canReuse && !LocalPeerId.Equals (id.Peer.PeerId)) {
                    if (!manager.Peers.AvailablePeers.Contains (id.Peer) && id.Peer.CleanedUpCount < 5)
                        manager.Peers.AvailablePeers.Insert (0, id.Peer);
                    else if (manager.Peers.BannedPeers.Contains (id.Peer) && id.Peer.CleanedUpCount >= 5)
                        manager.Peers.BannedPeers.Add (id.Peer);
                }
            } catch (Exception ex) {
                logger.Exception (ex, "An unexpected error occured cleaning up a connection");
            } finally {
                manager.RaisePeerDisconnected (new PeerDisconnectedEventArgs (manager, id));
            }

            id.Dispose ();
        }

        /// <summary>
        /// Cancel all pending connection attempts which have exceeded <see cref="EngineSettings.ConnectionTimeout"/>
        /// </summary>
        internal void CancelPendingConnects ()
        {
            CancelPendingConnects (null);
        }

        /// <summary>
        /// Cancel all pending connection for the given <see cref="TorrentManager"/>, or which have exceeded <see cref="EngineSettings.ConnectionTimeout"/>
        /// </summary>
        internal void CancelPendingConnects (TorrentManager manager)
        {
            foreach (AsyncConnectState pending in PendingConnects)
                if (pending.Manager == manager || pending.Timer.Elapsed > Settings.ConnectionTimeout)
                    pending.Connection.Dispose ();
        }

        /// <summary>
        /// This method is called when the ClientEngine recieves a valid incoming connection
        /// </summary>
        /// <param name="manager">The torrent which the peer is associated with.</param>
        /// <param name="id">The peer who just conencted</param>
        internal async ReusableTask<bool> IncomingConnectionAcceptedAsync (TorrentManager manager, PeerId id)
        {
            try {
                bool maxAlreadyOpen = OpenConnections >= Math.Min (MaxOpenConnections, Settings.MaximumConnections);
                if (LocalPeerId.Equals (id.Peer.PeerId)) {
                    logger.Info ("Connected to self - disconnecting");
                    CleanupSocket (manager, id);
                    return false;
                }
                if (manager.Peers.ActivePeers.Contains (id.Peer)) {
                    logger.Info (id.Connection, "Already connected to peer");
                    id.Connection.Dispose ();
                    return false;
                }
                if (maxAlreadyOpen) {
                    logger.Info ("Connected to too many peers - disconnecting");
                    CleanupSocket (manager, id);
                    return false;
                }

                // Add the PeerId to the lists *before* doing anything asynchronous. This ensures that
                // all PeerIds are tracked in 'ConnectedPeers' as soon as they're created.
                logger.Info (id.Connection, "Incoming connection fully accepted");
                manager.Peers.AvailablePeers.Remove (id.Peer);
                manager.Peers.ActivePeers.Add (id.Peer);
                manager.Peers.ConnectedPeers.Add (id);
                Interlocked.Increment (ref openConnections);

                id.WhenConnected.Restart ();
                // Baseline the time the last block was received
                id.LastBlockReceived.Restart ();

                // Send our handshake now that we've decided to keep the connection
                var handshake = new HandshakeMessage (manager.InfoHash, manager.Engine.PeerId, VersionInfo.ProtocolStringV100);
                await PeerIO.SendMessageAsync (id.Connection, id.Encryptor, handshake, manager.UploadLimiters, id.Monitor, manager.Monitor);

                manager.HandlePeerConnected (id);
                id.MessageQueue.SetReady ();
                TryProcessQueue (manager, id);

                // We've sent our handshake so begin our looping to receive incoming message
                ReceiveMessagesAsync (id.Connection, id.Decryptor, manager.DownloadLimiters, id.Monitor, manager, id);
                logger.InfoFormatted ("Incoming connection fully accepted", id.Uri);
                return true;
            } catch (Exception ex) {
                logger.Exception (ex, "Error handling incoming connection");
                CleanupSocket (manager, id);
                return false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="manager">The torrent which the peer is associated with.</param>
        /// <param name="id">The peer whose message queue you want to start processing</param>
        internal async void TryProcessQueue (TorrentManager manager, PeerId id)
        {
            if (!id.MessageQueue.BeginProcessing ())
                return;

            await MainLoop.SwitchToThreadpool ();

            ByteBufferPool.Releaser messageBuffer = default;
            ByteBufferPool.Releaser pieceBuffer = default;
            PeerMessage msg;
            try {
                while ((msg = id.MessageQueue.TryDequeue ()) != null) {
                    var msgLength = msg.ByteLength;

                    if (msg is PieceMessage pm) {
                        if (pieceBuffer.Buffer == null)
                            pieceBuffer = DiskManager.BufferPool.Rent (msgLength, out ByteBuffer _);
                        pm.DataReleaser = pieceBuffer;
                        try {
                            await DiskManager.ReadAsync (manager, pm.StartOffset + ((long) pm.PieceIndex * manager.Torrent.PieceLength), pm.Data, pm.RequestLength).ConfigureAwait (false);
                        } catch (Exception ex) {
                            await ClientEngine.MainLoop;
                            manager.TrySetError (Reason.ReadFailure, ex);
                            return;
                        }
                        System.Threading.Interlocked.Increment (ref id.piecesSent);
                    } else {
                        pieceBuffer.Dispose ();
                    }

                    if (messageBuffer.Buffer == null || messageBuffer.Buffer.Data.Length < msg.ByteLength) {
                        messageBuffer.Dispose ();
                        messageBuffer = NetworkIO.BufferPool.Rent (msgLength, out ByteBuffer _);
                    }
                    await PeerIO.SendMessageAsync (id.Connection, id.Encryptor, msg, manager.UploadLimiters, id.Monitor, manager.Monitor, messageBuffer.Buffer).ConfigureAwait (false);
                    if (msg is PieceMessage)
                        System.Threading.Interlocked.Decrement (ref id.isRequestingPiecesCount);

                    id.LastMessageSent.Restart ();
                }
            } catch {
                await ClientEngine.MainLoop;
                CleanupSocket (manager, id);
            } finally {
                messageBuffer.Dispose ();
                pieceBuffer.Dispose ();
            }
        }

        internal bool ShouldBanPeer (Peer peer)
        {
            if (BanPeer == null)
                return false;

            var e = new AttemptConnectionEventArgs (peer);
            BanPeer (this, e);
            return e.BanPeer;
        }

        internal void TryConnect ()
        {
            // If we have already reached our max connections globally, don't try to connect to a new peer
            while (OpenConnections <= MaxOpenConnections && PendingConnects.Count < MaxHalfOpenConnections) {
                LinkedListNode<TorrentManager> node = Torrents.First;
                while (node != null) {
                    // If we successfully connect, then break out of this loop and restart our
                    // connection process from the first node in the list again.
                    if (TryConnect (node.Value)) {
                        Torrents.Remove (node);
                        Torrents.AddLast (node);
                        break;
                    }

                    // If we did not successfully connect to a peer, then try the next torrent.
                    node = node.Next;
                }

                // If we failed to connect to anyone after walking the entire list, give up for now.
                if (node == null)
                    break;
            }
        }

        bool TryConnect (TorrentManager manager)
        {
            int i;
            if (!manager.Mode.CanAcceptConnections)
                return false;

            // If we have reached the max peers allowed for this torrent, don't connect to a new peer for this torrent
            if (manager.Peers.ConnectedPeers.Count >= Settings.MaximumConnections)
                return false;

            // If the torrent isn't active, don't connect to a peer for it
            if (!manager.Mode.CanAcceptConnections)
                return false;

            // If we are not seeding, we can connect to anyone. If we are seeding, we should only connect to a peer
            // if they are not a seeder.
            for (i = 0; i < manager.Peers.AvailablePeers.Count; i++)
                if (manager.Mode.ShouldConnect (manager.Peers.AvailablePeers[i]))
                    break;

            // If this is true, there were no peers in the available list to connect to.
            if (i == manager.Peers.AvailablePeers.Count)
                return false;

            // Remove the peer from the lists so we can start connecting to him
            Peer peer = manager.Peers.AvailablePeers[i];
            manager.Peers.AvailablePeers.RemoveAt (i);

            if (ShouldBanPeer (peer))
                return false;

            // Connect to the peer
            logger.InfoFormatted ("Trying to connect to {0}", peer.ConnectionUri);
            ConnectToPeer (manager, peer);
            return true;
        }
    }
}
