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

        internal int openConnections;

        HashSet<string> BannedPeerIPAddresses = new HashSet<string> ();

        internal DiskManager DiskManager { get; }

        Factories Factories { get; }

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
            // Whenever we try to connect to a peer, we may try multiple times.
            //  1. If we cannot establish a connection, we bail out. A retry will occur later
            //  2. If we can establish a connection but the connection closes, retry with a different
            //     encryption method immediately. The odds are high this will succeed.
            ConnectionFailureReason? failureReason;
            try {
                manager.Peers.ConnectingToPeers.Add (peer);
                failureReason = await DoConnectToPeer (manager, peer);
            } catch {
                failureReason = ConnectionFailureReason.Unknown;
            } finally {
                manager.Peers.ConnectingToPeers.Remove (peer);
            }

            // Always restart the the timer after the connection attempt completes
            peer.LastConnectionAttempt.Restart ();

            // If the connection attempt failed, decide what to do next. Drop the peer or retry it later.
            if (failureReason.HasValue) {
                peer.FailedConnectionAttempts++;

                // If we have not exhausted all retry attempts, add the peer back for subsequent retry
                if (Settings.GetConnectionRetryDelay (peer.FailedConnectionAttempts).HasValue)
                    manager.Peers.AvailablePeers.Add (peer);

                manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (peer.Info, failureReason.Value, manager));
            }

            // Always try to connect to a new peer. If there are no active torrents, the call will just bail out.
            TryConnect ();
        }

        async ReusableTask<ConnectionFailureReason?> DoConnectToPeer (TorrentManager manager, Peer peer)
        {
            ConnectionFailureReason? latestResult = ConnectionFailureReason.Unknown;
            foreach (var allowedEncryption in Settings.OutgoingConnectionEncryptionTiers) {
                // Bail out if the manager can no longer accept connections (i.e. is in the Stopping or Stopped mode now)
                if (!manager.Mode.CanAcceptConnections)
                    return ConnectionFailureReason.Unknown;

                // Create a new IPeerConnection object for each connection attempt.
                var connection = Factories.CreatePeerConnection (peer.Info.ConnectionUri);
                if (connection == null)
                    return ConnectionFailureReason.UnknownUriSchema;

                var state = new AsyncConnectState (manager, connection, ValueStopwatch.StartNew ());
                try {
                    PendingConnects.Add (state);

                    // A return value of 'null' means connection succeeded
                    latestResult = await DoConnectToPeer (manager, peer, connection, allowedEncryption);
                    if (latestResult == null)
                        return null;
                } catch {
                    latestResult = ConnectionFailureReason.Unknown;
                } finally {
                    PendingConnects.Remove (state);
                }

                // If the connection did not succeed, dispose the object and try again with a different encryption tier.
                connection.SafeDispose ();
            }

            // if we got non-null failure reasons, return the most recent one here.
            return latestResult;
        }

        async ReusableTask<ConnectionFailureReason?> DoConnectToPeer (TorrentManager manager, Peer peer, IPeerConnection connection, IList<EncryptionType> allowedEncryption)
        {
            try {
                await NetworkIO.ConnectAsync (connection);
            } catch {
                // A failure to connect is unlikely to be fixed by retrying a different encryption method, so bail out immediately.
                return ConnectionFailureReason.Unreachable;
            }

            // If the torrent is no longer downloading/seeding etc, bail out.
            if (manager.Disposed || !manager.Mode.CanAcceptConnections)
                return ConnectionFailureReason.Unknown;

            // If too many connections are open, bail out.
            if (OpenConnections > Settings.MaximumConnections || manager.OpenConnections > manager.Settings.MaximumConnections)
                return ConnectionFailureReason.TooManyOpenConnections;

            // Reset the connection timer so there's a little bit of extra time for the handshake.
            // Otherwise, if this fails we should probably retry with a different encryption type.
            try {
                return await ProcessNewOutgoingConnection (manager, peer, connection, allowedEncryption);
            } catch {
                return ConnectionFailureReason.Unknown;
            }
        }

        internal bool Contains (TorrentManager manager)
        {
            return Torrents.Contains (manager);
        }

        internal async ReusableTask<ConnectionFailureReason?> ProcessNewOutgoingConnection (TorrentManager manager, Peer peer, IPeerConnection connection, IList<EncryptionType> allowedEncryption)
        {
            var bitfield = new BitField (manager.Bitfield.Length);
            Interlocked.Increment (ref openConnections);

            IEncryption decryptor;
            IEncryption encryptor;

            try {
                // If this is a hybrid torrent and a connection is being made with the v1 infohash, then
                // set the bit which tells the peer the connection can be upgraded to a bittorrent v2 (BEP52) connection.
                var canUpgradeToV2 = manager.InfoHashes.IsHybrid;

                // Create a handshake message to send to the peer
                var handshake = new HandshakeMessage (manager.InfoHashes.V1OrV2.Truncate (), LocalPeerId, Constants.ProtocolStringV100, enableFastPeer: true, enableExtended: true, supportsUpgradeToV2: canUpgradeToV2);
                logger.InfoFormatted (connection, "[outgoing] Sending handshake message with peer id '{0}'", LocalPeerId);

                EncryptorFactory.EncryptorResult result = await EncryptorFactory.CheckOutgoingConnectionAsync (connection, allowedEncryption, manager.InfoHashes.V1OrV2.Truncate (), handshake, Factories, Settings.ConnectionTimeout);
                decryptor = result.Decryptor;
                encryptor = result.Encryptor;
            } catch {
                return ConnectionFailureReason.EncryptionNegiotiationFailed;
            }

            PeerId? id = null;
            try {
                // Receive their handshake. NOTE: For hybrid torrents the standard is to send the V1 infohash
                // and if the peer responds with the V2 infohash, treat the connection as a V2 connection. The
                // biggest (only?) difference is that it means we can request the merkle tree layer hashes from
                // peers who support v2.
                HandshakeMessage handshake = await PeerIO.ReceiveHandshakeAsync (connection, decryptor);
                id = new PeerId (peer, connection, new BitField (manager.Bitfield.Length), manager.InfoHashes.Expand (handshake.InfoHash)) {
                    Decryptor = decryptor,
                    Encryptor = encryptor
                };
                id.LastMessageReceived.Restart ();
                id.LastMessageSent.Restart ();

                logger.InfoFormatted (id.Connection, "[outgoing] Received handshake message with peer id '{0}'", handshake.PeerId);
                manager.Mode.HandleMessage (id, handshake, default);

                if (ShouldBanPeer (peer.Info, AttemptConnectionStage.HandshakeComplete))
                    return ConnectionFailureReason.Banned;
            } catch {
                return ConnectionFailureReason.HandshakeFailed;
            }

            try {
                if (id.BitField.Length != manager.Bitfield.Length)
                    throw new TorrentException ($"The peer's bitfield was of length {id.BitField.Length} but the TorrentManager's bitfield was of length {manager.Bitfield.Length}.");

                manager.Peers.ActivePeers.Add (peer);
                manager.Peers.ConnectedPeers.Add (id);

                manager.Mode.HandlePeerConnected (id);
                id.MessageQueue.SetReady ();
                TryProcessQueue (manager, id);

                ReceiveMessagesAsync (id.Connection, id.Decryptor, manager.DownloadLimiters, id.Monitor, manager, id);

                id.WhenConnected.Restart ();
                id.LastBlockReceived.Reset ();
                return null;
            } catch {
                manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (id.Peer.Info, ConnectionFailureReason.Unknown, manager));
                CleanupSocket (manager, id);
                return ConnectionFailureReason.Unknown;
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

            manager.PieceManager.CancelRequests (id);
            if (!id.AmChoking)
                manager.UploadingTo--;
            if (manager.Peers.ConnectedPeers.Remove (id))
                Interlocked.Decrement (ref openConnections);
            id.Peer.CleanedUpCount++;

            CleanupSocket (manager, id.Peer, id.Connection);

            try {
                manager.Mode.HandlePeerDisconnected (id);
            } catch (Exception ex) {
                logger.Exception (ex, "An unexpected error occured calling HandlePeerDisconnected");
            }
            id.Dispose ();
        }

        internal void CleanupSocket (TorrentManager manager, Peer peer, IPeerConnection connection)
        {
            try {
                // We can reuse this peer if the connection says so and it's not marked as inactive
                bool canReuse = (connection?.CanReconnect ?? false)
                    && !manager.InactivePeerManager.InactivePeerList.Contains (peer.Info.ConnectionUri)
                    && !manager.Engine!.PeerId.Equals (peer.Info.PeerId)
                    && Settings.GetConnectionRetryDelay(peer.FailedConnectionAttempts).HasValue;

                manager.Peers.ActivePeers.Remove (peer);

                // If we get our own details, this check makes sure we don't try connecting to ourselves again
                if (canReuse && !LocalPeerId.Equals (peer.Info.PeerId)) {
                    if (!manager.Peers.AvailablePeers.Contains (peer) && peer.CleanedUpCount < 5)
                        manager.Peers.AvailablePeers.Add (peer);
                    else if (peer.CleanedUpCount >= 5)
                        BannedPeerIPAddresses.Add (peer.Info.ConnectionUri.Host);
                }
            } catch (Exception ex) {
                logger.Exception (ex, "An unexpected error occured cleaning up a connection");
            } finally {
            }
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

                if (LocalPeerId.Equals (id.Peer.Info.PeerId)) {
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
                if (ShouldBanPeer (id.Peer.Info, AttemptConnectionStage.HandshakeComplete)) {
                    logger.Info (id.Connection, "Peer was banned");
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


        internal bool ShouldBanPeer (PeerInfo peer, AttemptConnectionStage stage)
        {
            if (BannedPeerIPAddresses.Count > 0 && BannedPeerIPAddresses.Contains (peer.ConnectionUri.Host))
                return true;

            if (BanPeer == null)
                return false;

            var e = new AttemptConnectionEventArgs (peer, stage);
            BanPeer (this, e);
            if (e.BanPeer)
                BannedPeerIPAddresses.Add (peer.ConnectionUri.Host);
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
            // If the torrent isn't active, don't connect to a peer for it
            if (!manager.Mode.CanAcceptConnections)
                return false;

            // If we have reached the max peers allowed for this torrent, don't connect to a new peer for this torrent
            if ((manager.Peers.ConnectedPeers.Count + manager.Peers.ConnectingToPeers.Count) >= manager.Settings.MaximumConnections)
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

            if (ShouldBanPeer (peer.Info, AttemptConnectionStage.BeforeConnectionEstablished))
                return false;

            // Connect to the peer
            logger.InfoFormatted ("Trying to connect to {0}", peer.Info.ConnectionUri);
            ConnectToPeer (manager, peer);
            return true;
        }
    }
}
