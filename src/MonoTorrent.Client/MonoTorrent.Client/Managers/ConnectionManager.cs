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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Modes;
using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Logging;
using MonoTorrent.Messages;
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
            peer.WaitUntilNextConnectionAttempt.Restart ();

            // If the connection attempt failed, decide what to do next. Drop the peer or retry it later.
            if (failureReason.HasValue) {
                peer.FailedConnectionAttempts++;

                // If we have not exhausted all retry attempts, add the peer back for subsequent retry
                if (failureReason.Value != ConnectionFailureReason.ConnectedToSelf &&
                    Settings.GetConnectionRetryDelay (peer.FailedConnectionAttempts).HasValue)
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

                // If the error is *not* a retryable error, then bail out and return the failure.
                // Otherwise loop and try again. A failure to send/receive a handshake is considered to be
                // an encryption negiotiation failure as for outgoing connections the local client may send a
                // plaintext handshake and the remote client may discard it as it only accepts encrypted ones.
                if (latestResult != ConnectionFailureReason.EncryptionNegiotiationFailed)
                    return latestResult;
            }

            // if we got non-null failure reasons, return the most recent one here.
            return latestResult;
        }

        async ReusableTask<ConnectionFailureReason?> DoConnectToPeer (TorrentManager manager, Peer peer, IPeerConnection connection, IList<EncryptionType> allowedEncryption)
        {
            try {
                if (!await NetworkIO.ConnectAsync (connection))
                    return ConnectionFailureReason.Unreachable;
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

            IEncryption decryptor;
            IEncryption encryptor;

            using var releaser = MemoryPool.Default.Rent (HandshakeMessage.HandshakeLength, out var handshakeBuffer);
            try {
                // If this is a hybrid torrent and a connection is being made with the v1 infohash, then
                // set the bit which tells the peer the connection can be upgraded to a bittorrent v2 (BEP52) connection.
                var canUpgradeToV2 = manager.InfoHashes.IsHybrid;

                // Create a handshake message to send to the peer
                MessageEncoder.WriteHandshake (handshakeBuffer.Span, manager.InfoHashes.V1OrV2.Span.Slice (0, 20), LocalPeerId.Span, enableFastPeer: true, enableExtended: true, supportUpgradeToV2: canUpgradeToV2);
                logger.InfoFormatted (connection, "Sending handshake message with peer id '{0}' and infohash: {1}", LocalPeerId, manager.InfoHashes.V1OrV2);

                EncryptorFactory.EncryptorResult result = await EncryptorFactory.CheckOutgoingConnectionAsync (connection, allowedEncryption, manager.InfoHashes.V1OrV2.Truncate (), handshakeBuffer, Factories, Settings.ConnectionTimeout);
                decryptor = result.Decryptor;
                encryptor = result.Encryptor;

                // If plaintext encryption is used, we need to *receive* the remote handshake before we can confirm
                // that negotiation has completed successfully.
                await PeerIO.ReceiveHandshakeAsync (connection, decryptor, handshakeBuffer);
                if (!new HandshakeMessage (handshakeBuffer).ProtocolString.SequenceEqual (Constants.ProtocolStringV100UTF8))
                    logger.Info (connection, "Received handshake but protocol was unsupported");
            } catch (Exception) {
                logger.Info (connection, "Could not receive a handshake from the peer using: " + string.Join (",", allowedEncryption.Select (t => t.ToString ())));
                return ConnectionFailureReason.EncryptionNegiotiationFailed;
            }

            PeerId id;
            try {
                // Receive their handshake. NOTE: For hybrid torrents the standard is to send the V1 infohash
                // and if the peer responds with the V2 infohash, treat the connection as a V2 connection. The
                // biggest (only?) difference is that it means we can request the merkle tree layer hashes from
                // peers who support v2.
                id = CreatePeerIdFromHandshake (new HandshakeMessage (handshakeBuffer), peer, connection, manager, encryptor: encryptor, decryptor: decryptor);
                logger.InfoFormatted (id.Connection, "Received handshake message with peer id '{0}' for hash: {1}. Upgradeable: {2}", new HandshakeMessage (handshakeBuffer).PeerId, new HandshakeMessage (handshakeBuffer).InfoHash, new HandshakeMessage (handshakeBuffer).SupportsUpgradeToV2);

                if (LocalPeerId.Span.SequenceEqual (new HandshakeMessage (handshakeBuffer).PeerId))
                    return ConnectionFailureReason.ConnectedToSelf;

                // CreatePeerIdFromHandshake files in the peerid, which is important context for whether or not
                // the peer connection should be closed.
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
                Interlocked.Increment (ref openConnections);

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

        internal static PeerId CreatePeerIdFromHandshake (HandshakeMessage handshake, Peer peer, IPeerConnection connection, TorrentManager manager, IEncryption encryptor, IEncryption decryptor)
        {
            if (!handshake.ProtocolString.SequenceEqual (Constants.ProtocolStringV100UTF8)) {
                logger.InfoFormatted (connection, "Invalid protocol in handshake: {0}", System.Text.Encoding.UTF8.GetString (handshake.ProtocolString));
                throw new ProtocolException ("Invalid protocol string");
            }

            // If the infohash doesn't match, dump the connection
            if (!manager.InfoHashes.Contains (handshake.InfoHash)) {
                logger.Info (connection, "HandShake.Handle - Invalid infohash");
                throw new TorrentException ("Invalid infohash. Not tracking this torrent");
            }

            // If we got the peer as a "compact" peer, then the peerid will be empty. In this case
            // we just copy the one that is in the handshake.
            if (BEncodedString.IsNullOrEmpty (peer.Info.PeerId))
                peer.UpdatePeerId (new BEncodedString (handshake.PeerId));

            // If this is a hybrid torrent, and the other peer announced with the v1 hash *and* set the bit which indicates
            // they can upgrade to a V2 connection, respond with the V2 hash to upgrade the connection to V2 mode.
            var infoHash = handshake.SupportsUpgradeToV2 && manager.InfoHashes.IsHybrid ? manager.InfoHashes.V2! : manager.InfoHashes.Expand (handshake.InfoHash);

            // Create the peerid now that everything is established.
            var id = new PeerId (peer, connection, new BitField (manager.Bitfield.Length), infoHash, encryptor: encryptor, decryptor: decryptor, new Software (new BEncodedString (handshake.PeerId)));

            // If the peer id's don't match, dump the connection. This is due to peers faking usually
            if (!id.Peer.Info.PeerId.Span.SequenceEqual (handshake.PeerId)) {
                if (manager.Settings.RequirePeerIdToMatch) {
                    // Several prominent clients randomise peer ids (at the least, everything based on libtorrent)
                    // so closing connections when the peer id does not match risks blocking compatibility with many
                    // clients. Additionally, MonoTorrent has long been configured to default to compact tracker responses
                    // so the odds of having the peer ID are slim.
                    logger.InfoFormatted (id.Connection, "HandShake.Handle - Invalid peerid. Expected '{0}' but received '{1}'", id.Peer.Info.PeerId, Encoding.UTF8.GetString (handshake.PeerId));
                    throw new TorrentException ("Supplied PeerID didn't match the one the tracker gave us");
                } else {
                    // We don't care about the mismatch for public torrents. uTorrent randomizes its PeerId, as do other clients.
                    id.Peer.UpdatePeerId (new BEncodedString (handshake.PeerId));
                }
            }
            // Copy over the capability bits
            id.SupportsFastPeer = handshake.EnableFastPeer;
            id.SupportsLTMessages = handshake.EnableExtended;

            // reset the timers so the connection isn't closed early due to inactivity
            id.LastMessageReceived.Restart ();
            id.LastMessageSent.Restart ();


            // If they support fast peers, create their list of allowed pieces that they can request off me
            if (id.SupportsFastPeer && id.AddressBytes.Length > 0 && manager != null && manager.HasMetadata) {
                lock (AllowedFastHasher)
                    id.AmAllowedFastPieces = AllowedFastAlgorithm.Calculate (AllowedFastHasher, id.AddressBytes.Span, manager.InfoHashes, (uint) manager.Torrent!.PieceCount);
            }
            return id;
        }
        static readonly SHA1 AllowedFastHasher = SHA1.Create ();

        internal async void ReceiveMessagesAsync (IPeerConnection connection, IEncryption decryptor, RateLimiterGroup downloadLimiter, ConnectionMonitor monitor, TorrentManager torrentManager, PeerId id)
        {
            await MainLoop.SwitchToThreadpool ();

            try {
                using var headerReleaser = MemoryPool.Default.Rent (4, out var headerBuffer);
                while (true) {
                    (ReadOnlyMemory<byte> message, ByteBufferPool.Releaser messageReleaser) = await PeerIO.ReceiveMessageAsync (connection, decryptor, downloadLimiter, monitor, torrentManager.Monitor, torrentManager, headerBuffer).ConfigureAwait (false);
                    HandleReceivedMessage (id, torrentManager, message, messageReleaser);
                }
            } catch (Exception) {
                await ClientEngine.MainLoop;
                CleanupSocket (torrentManager, id);
            }
        }

        static async void HandleReceivedMessage (PeerId id, TorrentManager torrentManager, ReadOnlyMemory<byte> message, ByteBufferPool.Releaser releaser)
        {
            await ClientEngine.MainLoop;

            if (!id.Disposed) {
                try {
                    if (torrentManager.Mode is IMessageHandler handler) {
                        Dispatch (handler, id, torrentManager, message);
                        id.LastMessageReceived.Restart ();
                        torrentManager.Engine!.ConnectionManager.TryProcessQueue (torrentManager, id);
                    } else {
                        torrentManager.Engine!.ConnectionManager.CleanupSocket (torrentManager, id);
                    }
                } catch (Exception ex) {
                    logger.Exception (ex, "Unexpected error handling a message from a peer");
                    torrentManager.Engine!.ConnectionManager.CleanupSocket (torrentManager, id);
                } finally {
                    releaser.Dispose ();
                }
            }

            static void Dispatch (IMessageHandler handler, PeerId peer, TorrentManager manager, ReadOnlyMemory<byte> message)
            {
                var span = message.Span;
                var length = BinaryPrimitives.ReadInt32BigEndian (span);

                // if length is zero this was a keepalive, and all we need to do is reset the last message received timer
                if (length == 0) {
                    handler.HandleMessage (peer, new KeepAliveMessage ());
                    return;
                }

                var type = MessageDispatcher.GetType (span);
                if (type == MessageType.RejectRequest) {
                    // Reject messages are supported by BEP52 and BEP6.
                    if (manager.InfoHashes.V2 is null && !peer.SupportsFastPeer)
                        throw new MessageException ("Peer shouldn't support fast peer messages");
                } else if (type.IsFastExtension () && !peer.SupportsFastPeer) {
                    throw new MessageException ("Peer shouldn't support fast peer messages");
                } else if (type == MessageType.Extended && !peer.SupportsLTMessages && MessageDispatcher.GetExtendedMessageType (span) != ExtendedMessageType.Handshake) {
                    throw new MessageException ("Peer shouldn't support extension messages");
                }

                // Ensure the buffer is trimmed to match the message length
                span = span.Slice (0, 4 + length);

                // BitTorrent v1 (bep3)
                switch (type) {
                    case MessageType.Choke:
                        handler.HandleMessage (peer, new ChokeMessage ());
                        break;
                    case MessageType.Unchoke:
                        handler.HandleMessage (peer, new UnchokeMessage ());
                        break;
                    case MessageType.Interested:
                        handler.HandleMessage (peer, new InterestedMessage ());
                        break;
                    case MessageType.NotInterested:
                        handler.HandleMessage (peer, new NotInterestedMessage ());
                        break;
                    case MessageType.Have:
                        handler.HandleMessage (peer, new HaveMessage (message));
                        break;
                    case MessageType.Bitfield:
                        handler.HandleMessage (peer, new BitfieldMessage (message));
                        break;
                    case MessageType.Request:
                        handler.HandleMessage (peer, new RequestMessage (message));
                        break;
                    case MessageType.Piece:
                        handler.HandleMessage (peer, new PieceMessage (message));
                        break;
                    case MessageType.Cancel:
                        handler.HandleMessage (peer, new CancelMessage (message));
                        break;

                    // DHT (bep5)
                    case MessageType.Port:
                        handler.HandleMessage (peer, new PortMessage (message));
                        break;

                    // Fast Extensions (bep6)
                    case MessageType.Suggest:
                        handler.HandleMessage (peer, new SuggestMessage (message));
                        break;
                    case MessageType.HaveAll:
                        handler.HandleMessage (peer, new HaveAllMessage ());
                        break;
                    case MessageType.HaveNone:
                        handler.HandleMessage (peer, new HaveNoneMessage ());
                        break;
                    case MessageType.RejectRequest:
                        handler.HandleMessage (peer, new RejectRequestMessage (message));
                        break;
                    case MessageType.AllowedFast:
                        handler.HandleMessage (peer, new AllowedFastMessage (message));
                        break;

                    // BitTorrent v2 (bep52)
                    case MessageType.HashRequest:
                        handler.HandleMessage (peer, new HashRequestMessage (message));
                        break;
                    case MessageType.Hashes:
                        handler.HandleMessage (peer, new HashesMessage (message));
                        break;
                    case MessageType.HashReject:
                        handler.HandleMessage (peer, new HashRejectMessage (message));
                        break;

                    // LibTorrent extension protocol (bep 10)
                    case MessageType.Extended:
                        switch (MessageDispatcher.GetExtendedMessageType (message)) {
                            case ExtendedMessageType.Handshake:
                                handler.HandleMessage (peer, new Extended.HandshakeMessage (message));
                                break;
                            case ExtendedMessageType.Metadata:
                                handler.HandleMessage (peer, new Extended.MetadataMessage (message));
                                break;
                            case ExtendedMessageType.PeerExchange:
                                handler.HandleMessage (peer, new Extended.PeerExchangeMessage (message));
                                break;
                            default:
                                logger.ErrorFormatted ("Unsupported extended message '{0}' received, closing the connection", (int) MessageDispatcher.GetExtendedMessageType (message));
                                throw new NotSupportedException ("Extended message not supported");
                        }
                        break;
                    default:
                        logger.ErrorFormatted ("Unsupported message '{0}' received, closing the connection", (int) type);
                        throw new NotSupportedException ("Message not support");
                }
            }
        }

        internal void CleanupSocket (TorrentManager manager, PeerId id)
        {
            // We might dispose the socket from an async send *and* an async receive call.
            if (id.Disposed)
                return;

            try {
                manager.PieceManager.CancelRequests (id);
                if (!id.AmChoking)
                    manager.UploadingTo--;
                if (manager.Peers.ConnectedPeers.Remove (id))
                    Interlocked.Decrement (ref openConnections);
                id.Peer.CleanedUpCount++;
                id.Peer.WaitUntilNextConnectionAttempt.Restart ();

                logger.Info (id.Connection, "Closing connection");
                // We can reuse this peer if the connection says so and it's not marked as inactive
                bool canReuse = (id.Connection.CanReconnect)
                    && !manager.InactivePeerManager.InactivePeerList.Contains (id.Peer.Info.ConnectionUri)
                    && !manager.Engine!.PeerId.Equals (id.Peer.Info.PeerId)
                    && Settings.GetConnectionRetryDelay (id.Peer.FailedConnectionAttempts).HasValue;

                manager.Peers.ActivePeers.Remove (id.Peer);

                // If we get our own details, this check makes sure we don't try connecting to ourselves again
                if (canReuse && !LocalPeerId.Equals (id.Peer.Info.PeerId)) {
                    if (!manager.Peers.AvailablePeers.Contains (id.Peer) && id.Peer.CleanedUpCount < 5)
                        manager.Peers.AvailablePeers.Add (id.Peer);
                    else if (id.Peer.CleanedUpCount >= 5)
                        BannedPeerIPAddresses.Add (id.Peer.Info.ConnectionUri.Host);
                }
            } catch (Exception ex) {
                logger.Exception (ex, "An unexpected error occured cleaning up a connection");
            } finally {
                id.Dispose ();
            }
            try {
                manager.Mode.HandlePeerDisconnected (id);
            } catch (Exception ex) {
                logger.Exception (ex, "An unexpected error occured calling HandlePeerDisconnected");
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


                // Send our handshake first, then decide if we've connected to ourselves or not.
                using (var releaser = MemoryPool.Default.Rent (68, out var buffer)) {
                    bool canUpgradeToV2 = manager.InfoHashes.IsHybrid && id.ExpectedInfoHash == manager.InfoHashes.V1;
                    MessageEncoder.WriteHandshake (buffer.Span, id.ExpectedInfoHash.Span.Slice (0, 20), LocalPeerId.Span, true, true, canUpgradeToV2);

                    logger.InfoFormatted (id.Connection, "Responding with infohash: {0}. Upgradeable: {1}", id.ExpectedInfoHash.Span, canUpgradeToV2);

                    await PeerIO.SendMessageAsync (id.Connection, id.Encryptor, buffer, manager.UploadLimiters, id.Monitor, manager.Monitor);
                }

                if (LocalPeerId.Equals (id.PeerID)) {
                    logger.Info ("Connected to self - disconnecting");
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
            static (BlockInfo, int) PrepareRead(Memory<byte> msg)
            {
                var pm = new PieceMessage (msg);
                return (new BlockInfo (pm.PieceIndex, pm.StartOffset, pm.RequestLength), pm.DataOffset);
            }

            if (!id.MessageQueue.BeginProcessing ())
                return;

            await MainLoop.SwitchToThreadpool ();

            try {
                while (id.MessageQueue.TryDequeue (out var msg, out var msgReleaser)) {
                    using var autorelease = msgReleaser;

                    var type = MessageDispatcher.GetType (msg);
                    if (type == MessageType.Piece) {
                        try {
                            // Populate the data now - write directly to the send buffer.
                            (var blockInfo, var dataOffset) = PrepareRead (msg);
                            await DiskManager.ReadAsync (manager, blockInfo, msg.Slice(dataOffset)).ConfigureAwait (false);
                        } catch (Exception ex) {
                            await ClientEngine.MainLoop;
                            manager.TrySetError (Reason.ReadFailure, ex);
                            return;
                        }
                        Interlocked.Increment (ref id.piecesSent);
                    }

                    await PeerIO.SendMessageAsync (id.Connection, id.Encryptor, msg, manager.UploadLimiters, id.Monitor, manager.Monitor).ConfigureAwait (false);
                    if (type == MessageType.Piece)
                        Interlocked.Decrement (ref id.isRequestingPiecesCount);

                    id.LastMessageSent.Restart ();
                }
            } catch {
                await ClientEngine.MainLoop;
                CleanupSocket (manager, id);
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
