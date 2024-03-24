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
using System.Diagnostics.CodeAnalysis;
using System.Threading;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Logging;
using MonoTorrent.Messages.Peer;

using ReusableTasks;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Main controller class for all incoming and outgoing connections
    /// </summary>
    public class ConnectionManager
    {
        static readonly Logger logger = Logger.Create (nameof (ConnectionManager));

        struct AsyncConnectState : IEquatable<AsyncConnectState>
        {
            public AsyncConnectState (TorrentManager manager, IPeerConnection connection, ValueStopwatch timer)
            {
                Manager = manager;
                Connection = connection;
                Timer = timer;
            }

            public readonly IPeerConnection Connection;
            public readonly TorrentManager Manager;
            public readonly ValueStopwatch Timer;

            public bool Equals (AsyncConnectState other)
                => Connection == other.Connection;

            public override bool Equals ([NotNullWhen (true)] object? obj)
                => obj is AsyncConnectState other && Equals (other);

            public override int GetHashCode ()
                => Connection.GetHashCode ();
        }

        public event EventHandler<AttemptConnectionEventArgs>? BanPeer;

        internal static readonly int ChunkLength = 2096 + 64;   // Download in 2kB chunks to allow for better rate limiting

        internal int openConnections;

        internal DiskManager DiskManager { get; }

        Factories Factories { get; }

        Dictionary<BEncodedString, int> LocalPeerIds { get; } = new Dictionary<BEncodedString, int> ();

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
        /// The number of open connections
        /// </summary>
        public int OpenConnections => openConnections;

        List<AsyncConnectState> PendingConnects { get; }

        internal EngineSettings Settings { get; set; }
        internal List<TorrentManager> Torrents { get; set; }

        internal ConnectionManager (BEncodedString localPeerId, EngineSettings settings, Factories factories, DiskManager diskManager)
        {
            DiskManager = diskManager ?? throw new ArgumentNullException (nameof (diskManager));
            LocalPeerId = localPeerId ?? throw new ArgumentNullException (nameof (localPeerId));
            LocalPeerIds.Add (localPeerId, 1);
            Settings = settings ?? throw new ArgumentNullException (nameof (settings));
            Factories = factories ?? throw new ArgumentNullException (nameof (factories));

            PendingConnects = new List<AsyncConnectState> ();
            Torrents = new List<TorrentManager> ();
        }

        internal void Add (TorrentManager manager)
        {
            Torrents.Add (manager);
        }

        internal void Remove (TorrentManager manager)
        {
            Torrents.Remove (manager);
        }

        async void ConnectToPeer (TorrentManager manager, Peer peer)
        {
            // Connect to the peer.
            var connection = Factories.CreatePeerConnection (peer.Info.ConnectionUri);
            if (connection == null || peer.AllowedEncryption.Count == 0)
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
            if (manager.Disposed ||
                !manager.Mode.CanAcceptConnections ||
                OpenConnections > Settings.MaximumConnections ||
                manager.OpenConnections > manager.Settings.MaximumConnections) {
                manager.Peers.AvailablePeers.Add (peer);
                connection.Dispose ();
                return;
            }

            try {
                if (!succeeded) {
                    peer.FailedConnectionAttempts++;
                    connection.Dispose ();
                    manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (peer.Info, ConnectionFailureReason.Unreachable, manager));
                } else {
                    var id = new PeerId (peer, connection, new BitField (manager.Bitfield.Length).SetAll (false));
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
            if (OpenConnections > Settings.MaximumConnections) {
                CleanupSocket (manager, id);
                return;
            }

            BEncodedString connectAs = await Factories.CreateTemporaryLocalPeerIdAsync (LocalPeerId, id.ExpectedInfoHash, id.Uri);
            if (connectAs is null || connectAs.Span.Length != 20) {
                logger.Exception (
                    new ArgumentException ("Peer ID must be exactly 20 bytes long", paramName: "temporaryLocalPeerId"),
                    "Generated temporary peer ID was not 20 bytes long");
                CleanupSocket (manager, id);
                return;
            }
            if (!LocalPeerId.Equals (connectAs))
                lock (LocalPeerIds)
                    LocalPeerIds[connectAs] = LocalPeerIds.TryGetValue (connectAs, out int repeats) ? repeats + 1 : 1;

            manager.Peers.ActivePeers.Add (id.Peer);
            manager.Peers.ConnectedPeers.Add (id);
            Interlocked.Increment (ref openConnections);

            try {
                // Create a handshake message to send to the peer
                var handshake = new HandshakeMessage (id.ExpectedInfoHash.Truncate (), connectAs, Constants.ProtocolStringV100);
                logger.InfoFormatted (id.Connection, "[outgoing] Sending handshake message with peer id '{0}'", connectAs);

                var preferredEncryption = EncryptionTypes.GetPreferredEncryption (id.Peer.AllowedEncryption, Settings.AllowedEncryption);
                if (preferredEncryption.Count == 0)
                    throw new NotSupportedException ("The peer and the engine do not agree on any encryption methods");
                EncryptorFactory.EncryptorResult result = await EncryptorFactory.CheckOutgoingConnectionAsync (id.Connection, preferredEncryption, id.ExpectedInfoHash.Truncate (), handshake, manager.Engine!.Factories);
                id.Decryptor = result.Decryptor;
                id.Encryptor = result.Encryptor;
            } catch {
                if (!LocalPeerId.Equals (connectAs))
                    lock (LocalPeerIds)
                        LocalPeerIds[connectAs] = LocalPeerIds[connectAs] - 1;

                // If an exception is thrown it's because we tried to establish an encrypted connection and something went wrong
                if (id.Peer.AllowedEncryption.Contains (EncryptionType.PlainText))
                    id.Peer.AllowedEncryption = EncryptionTypes.PlainText;
                else
                    id.Peer.AllowedEncryption = EncryptionTypes.None;

                manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (id.Peer.Info, ConnectionFailureReason.EncryptionNegiotiationFailed, manager));
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
                logger.InfoFormatted (id.Connection, "[outgoing] Received handshake message with peer id '{0}'", handshake.PeerId);
                manager.Mode.HandleMessage (id, handshake, default);
            } catch {
                if (!LocalPeerId.Equals (connectAs))
                    lock (LocalPeerIds)
                        LocalPeerIds[connectAs] = LocalPeerIds[connectAs] - 1;

                // If we choose plaintext and it resulted in the connection being closed, remove it from the list.
                id.Peer.AllowedEncryption = EncryptionTypes.Remove (id.Peer.AllowedEncryption, id.EncryptionType);

                manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (id.Peer.Info, ConnectionFailureReason.HandshakeFailed, manager));
                CleanupSocket (manager, id);

                // CleanupSocket will contain the peer only if AllowedEncryption is not set to None. If
                // the peer was re-added, then we should try to reconnect to it immediately to try an
                // encrypted connection, assuming the previous connection was unencrypted and it failed.
                if (manager.Peers.AvailablePeers.Remove (id.Peer))
                    ConnectToPeer (manager, id.Peer);

                return;
            }

            if (!LocalPeerId.Equals (connectAs))
                lock (LocalPeerIds)
                    LocalPeerIds[connectAs] = LocalPeerIds[connectAs] - 1;

            try {
                if (id.BitField.Length != manager.Bitfield.Length)
                    throw new TorrentException ($"The peer's bitfield was of length {id.BitField.Length} but the TorrentManager's bitfield was of length {manager.Bitfield.Length}.");

                manager.Mode.HandlePeerConnected (id);
                id.MessageQueue.SetReady ();
                TryProcessQueue (manager, id);

                ReceiveMessagesAsync (id.Connection, id.Decryptor, manager.DownloadLimiters, id.Monitor, manager, id);

                id.WhenConnected.Restart ();
                id.LastBlockReceived.Reset ();
            } catch {
                manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (id.Peer.Info, ConnectionFailureReason.Unknown, manager));
                CleanupSocket (manager, id);
                return;
            }
        }

        internal async void ReceiveMessagesAsync (IPeerConnection connection, IEncryption decryptor, RateLimiterGroup downloadLimiter, ConnectionMonitor monitor, TorrentManager torrentManager, PeerId id)
        {
            await MainLoop.SwitchToThreadpool ();

            Memory<byte> currentBuffer = default;

            Memory<byte> smallBuffer = default;
            ByteBufferPool.Releaser smallReleaser = default;

            Memory<byte> largeBuffer = default;
            ByteBufferPool.Releaser largeReleaser = default;
            try {
                while (true) {
                    if (id.AmRequestingPiecesCount == 0) {
                        if (!largeBuffer.IsEmpty) {
                            largeReleaser.Dispose ();
                            largeReleaser = default;
                            largeBuffer = currentBuffer = default;
                        }
                        if (smallBuffer.IsEmpty) {
                            smallReleaser = NetworkIO.BufferPool.Rent (ByteBufferPool.SmallMessageBufferSize, out smallBuffer);
                            currentBuffer = smallBuffer;
                        }
                    } else {
                        if (!smallBuffer.IsEmpty) {
                            smallReleaser.Dispose ();
                            smallReleaser = default;
                            smallBuffer = currentBuffer = default;
                        }
                        if (largeBuffer.IsEmpty) {
                            largeReleaser = NetworkIO.BufferPool.Rent (ByteBufferPool.LargeMessageBufferSize, out largeBuffer);
                            currentBuffer = largeBuffer;
                        }
                    }

                    (PeerMessage message, PeerMessage.Releaser releaser) = await PeerIO.ReceiveMessageAsync (connection, decryptor, downloadLimiter, monitor, torrentManager.Monitor, torrentManager, currentBuffer).ConfigureAwait (false);
                    HandleReceivedMessage (id, torrentManager, message, releaser);
                }
            } catch {
                await ClientEngine.MainLoop;
                CleanupSocket (torrentManager, id);
            } finally {
                smallReleaser.Dispose ();
                largeReleaser.Dispose ();
            }
        }

        static async void HandleReceivedMessage (PeerId id, TorrentManager torrentManager, PeerMessage message, PeerMessage.Releaser releaser = default)
        {
            await ClientEngine.MainLoop;

            if (!id.Disposed) {
                id.LastMessageReceived.Restart ();
                try {
                    torrentManager.Mode.HandleMessage (id, message, releaser);
                } catch (Exception ex) {
                    logger.Exception (ex, "Unexpected error handling a message from a peer");
                    torrentManager.Engine!.ConnectionManager.CleanupSocket (torrentManager, id);
                }
            } else {
                releaser.Dispose ();
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
                    && id.Peer.AllowedEncryption.Count > 0
                    && !manager.Engine!.PeerId.Equals (id.PeerID);

                manager.PieceManager.CancelRequests (id);
                if (!id.AmChoking)
                    manager.UploadingTo--;

                if (manager.Peers.ConnectedPeers.Remove (id))
                    Interlocked.Decrement (ref openConnections);
                manager.Peers.ActivePeers.Remove (id.Peer);

                id.Peer.CleanedUpCount++;

                // If we get our own details, this check makes sure we don't try connecting to ourselves again
                if (canReuse && !IsSelf (id.Peer.Info.PeerId)) {
                    if (!manager.Peers.AvailablePeers.Contains (id.Peer) && id.Peer.CleanedUpCount < 5)
                        manager.Peers.AvailablePeers.Insert (0, id.Peer);
                    else if (manager.Peers.BannedPeers.Contains (id.Peer) && id.Peer.CleanedUpCount >= 5)
                        manager.Peers.BannedPeers.Add (id.Peer);
                }
            } catch (Exception ex) {
                logger.Exception (ex, "An unexpected error occured cleaning up a connection");
            } finally {
                try {
                    manager.Mode.HandlePeerDisconnected (id);
                } catch (Exception ex) {
                    logger.Exception (ex, "An unexpected error occured calling HandlePeerDisconnected");
                }
            }

            id.Dispose ();
        }

        bool IsSelf(BEncodedString peerId)
        {
            lock (LocalPeerIds)
                return LocalPeerIds.ContainsKey(peerId);
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
        internal void CancelPendingConnects (TorrentManager? manager)
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
                bool maxAlreadyOpen = OpenConnections >= Settings.MaximumConnections
                    || manager.OpenConnections >= manager.Settings.MaximumConnections;

                if (IsSelf (id.Peer.Info.PeerId)) {
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
                id.LastBlockReceived.Reset ();

                // Send our handshake now that we've decided to keep the connection
                var handshake = new HandshakeMessage (id.ExpectedInfoHash.Truncate (), manager.Engine!.PeerId, Constants.ProtocolStringV100);
                await PeerIO.SendMessageAsync (id.Connection, id.Encryptor, handshake, manager.UploadLimiters, id.Monitor, manager.Monitor);

                manager.Mode.HandlePeerConnected (id);
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

            ByteBufferPool.Releaser socketMemoryReleaser = default;
            Memory<byte> socketMemory = default;

            try {
                while (id.MessageQueue.TryDequeue (out PeerMessage? msg, out PeerMessage.Releaser msgReleaser)) {
                    using var autorelease = msgReleaser;

                    if (socketMemory.IsEmpty || socketMemory.Length < msg.ByteLength) {
                        socketMemoryReleaser.Dispose ();
                        socketMemoryReleaser = NetworkIO.BufferPool.Rent (msg.ByteLength, out socketMemory);
                    }

                    var buffer = socketMemory.Slice (0, msg.ByteLength);
                    if (msg is PieceMessage pm) {
                        pm.SetData ((default, buffer.Slice (buffer.Length - pm.RequestLength)));
                        try {
                            var request = new BlockInfo (pm.PieceIndex, pm.StartOffset, pm.RequestLength);
                            await DiskManager.ReadAsync (manager, request, pm.Data).ConfigureAwait (false);
                        } catch (Exception ex) {
                            await ClientEngine.MainLoop;
                            manager.TrySetError (Reason.ReadFailure, ex);
                            return;
                        }
                        Interlocked.Increment (ref id.piecesSent);
                    }

                    await PeerIO.SendMessageAsync (id.Connection, id.Encryptor, msg, manager.UploadLimiters, id.Monitor, manager.Monitor, buffer).ConfigureAwait (false);
                    if (msg is PieceMessage)
                        Interlocked.Decrement (ref id.isRequestingPiecesCount);

                    id.LastMessageSent.Restart ();
                }
            } catch {
                await ClientEngine.MainLoop;
                CleanupSocket (manager, id);
            } finally {
                socketMemoryReleaser.Dispose ();
            }
        }

        internal bool ShouldBanPeer (PeerInfo peer)
        {
            if (BanPeer == null)
                return false;

            var e = new AttemptConnectionEventArgs (peer);
            BanPeer (this, e);
            return e.BanPeer;
        }

        static readonly Comparison<TorrentManager> ActiveConnectionsComparer = (left, right)
            => (left.Peers.ConnectedPeers.Count + left.Peers.ConnectingToPeers.Count).CompareTo (right.Peers.ConnectedPeers.Count + right.Peers.ConnectingToPeers.Count);

        internal void TryConnect ()
        {
            // If we have already reached our max connections globally, don't try to connect to a new peer
            while (OpenConnections <= Settings.MaximumConnections && PendingConnects.Count <= MaxHalfOpenConnections) {
                Torrents.Sort (ActiveConnectionsComparer);

                bool connected = false;
                for (int i = 0; i < Torrents.Count; i++) {
                    // If we successfully connect, then break out of this loop and restart our
                    // connection process from the first node in the list again.
                    if (TryConnect (Torrents[i])) {
                        connected = true;
                        break;
                    }
                }

                // If we failed to connect to anyone after walking the entire list, give up for now.
                if (!connected)
                    break;
            }
        }

        bool TryConnect (TorrentManager manager)
        {
            int i;
            if (!manager.Mode.CanAcceptConnections)
                return false;

            // If we have reached the max peers allowed for this torrent, don't connect to a new peer for this torrent
            if ((manager.Peers.ConnectedPeers.Count + manager.Peers.ConnectingToPeers.Count) >= manager.Settings.MaximumConnections)
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

            if (ShouldBanPeer (peer.Info))
                return false;

            // Connect to the peer
            logger.InfoFormatted ("Trying to connect to {0}", peer.Info.ConnectionUri);
            ConnectToPeer (manager, peer);
            return true;
        }
    }
}
