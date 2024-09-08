//
// Mode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Logging;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;
using MonoTorrent.Messages.Peer.Libtorrent;
using MonoTorrent.PiecePicking;

using ReusableTasks;

namespace MonoTorrent.Client.Modes
{
    abstract class Mode : IMode
    {
        static readonly Logger logger = Logger.Create (nameof (Mode));

        bool hashingPendingFiles;
        ValueStopwatch lastSendHaveMessage;
        ValueStopwatch lastRefreshAllPeers;

        protected CancellationTokenSource Cancellation { get; }
        protected ConnectionManager ConnectionManager { get; }
        protected DiskManager DiskManager { get; }
        protected TorrentManager Manager { get; }
        protected EngineSettings Settings { get; }
        protected IUnchoker Unchoker { get; }

        public virtual bool CanAcceptConnections => true;
        public virtual bool CanHandleMessages => true;
        public virtual bool CanHashCheck => false;
        public abstract TorrentState State { get; }
        public CancellationToken Token => Cancellation.Token;

        protected Mode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, IUnchoker? unchoker = null)
        {
            Cancellation = new CancellationTokenSource ();
            ConnectionManager = connectionManager ?? throw new ArgumentNullException (nameof (connectionManager));
            DiskManager = diskManager ?? throw new ArgumentNullException (nameof (DiskManager));
            Manager = manager ?? throw new ArgumentNullException (nameof (manager));
            Settings = settings ?? throw new ArgumentNullException (nameof (settings));

            Unchoker = unchoker ?? new ChokeUnchokeManager (new TorrentManagerUnchokeable (manager));
        }

        public virtual void HandleFilePriorityChanged (ITorrentManagerFile file, Priority oldPriority)
        {

        }

        public void HandleMessage (PeerId id, PeerMessage message, PeerMessage.Releaser releaser)
        {
            if (!CanHandleMessages)
                return;

            if (message is IFastPeerMessage && !id.SupportsFastPeer)
                throw new MessageException ("Peer shouldn't support fast peer messages");

            if (message is ExtensionMessage && !id.SupportsLTMessages && !(message is ExtendedHandshakeMessage))
                throw new MessageException ("Peer shouldn't support extension messages");

            if (message is HaveMessage have)
                HandleHaveMessage (id, have);
            else if (message is RequestMessage request)
                HandleRequestMessage (id, request);
            else if (message is PortMessage port)
                HandlePortMessage (id, port);
            else if (message is PieceMessage piece)
                HandlePieceMessage (id, piece, releaser);
            else if (message is NotInterestedMessage notinterested)
                HandleNotInterested (id, notinterested);
            else if (message is KeepAliveMessage keepalive)
                HandleKeepAliveMessage (id, keepalive);
            else if (message is InterestedMessage interested)
                HandleInterestedMessage (id, interested);
            else if (message is ChokeMessage choke)
                HandleChokeMessage (id, choke);
            else if (message is CancelMessage cancel)
                HandleCancelMessage (id, cancel);
            else if (message is BitfieldMessage bitfield)
                HandleBitfieldMessage (id, bitfield);
            else if (message is UnchokeMessage unchoke)
                HandleUnchokeMessage (id, unchoke);
            else if (message is HashRejectMessage hashReject)
                HandleHashRejectMessage (id, hashReject);
            else if (message is HashesMessage hashes)
                HandleHashesMessage (id, hashes);
            else if (message is HashRequestMessage hashRequest)
                HandleHashRequestMessage (id, hashRequest);
            else if (message is HaveAllMessage haveall)
                HandleHaveAllMessage (id, haveall);
            else if (message is HaveNoneMessage havenone)
                HandleHaveNoneMessage (id, havenone);
            else if (message is RejectRequestMessage rejectrequest)
                HandleRejectRequestMessage (id, rejectrequest);
            else if (message is SuggestPieceMessage suggestpiece)
                HandleSuggestedPieceMessage (id, suggestpiece);
            else if (message is AllowedFastMessage allowedfast)
                HandleAllowedFastMessage (id, allowedfast);
            else if (message is ExtendedHandshakeMessage extendedhandshake)
                HandleExtendedHandshakeMessage (id, extendedhandshake);
            else if (message is LTMetadata metadata)
                HandleLtMetadataMessage (id, metadata);
            else if (message is LTChat chat)
                HandleLtChat (id, chat);
            else if (message is PeerExchangeMessage peerexchange)
                HandlePeerExchangeMessage (id, peerexchange);
            else if (message is HandshakeMessage handshake)
                HandleHandshakeMessage (id, handshake);
            else if (message is ExtensionMessage extension)
                HandleGenericExtensionMessage (id, extension);
            else
                throw new MessageException ($"Unsupported message found: {message.GetType ().Name}");

            if (!(message is PieceMessage))
                releaser.Dispose ();
            ConnectionManager.TryProcessQueue (Manager, id);
        }

        protected virtual void HandleHashRequestMessage (PeerId id, HashRequestMessage hashRequest)
        {
            // Validate we're only requesting between 1 and 512 piece hashes to avoid being DDOS'ed by someone
            // requesting a few GB worth of hashes. The spec says that clients 'should not' request more than 512.
            // I'm choosing to treat that as 'must not'.
            bool successful = hashRequest.Index >= 0
                && hashRequest.Index <= Manager.Torrent!.PieceCount * (Manager.Torrent!.PieceLength / Constants.BlockSize)
                && hashRequest.BaseLayer >= 0;

            // Length MUST be equal-to-or-greater-than two and a power of two
            // Length SHOULD NOT be greater than 512.
            //      NOTE: The spec says 'should', I say 'must'. There's no real benefit to supporting larger requests.
            if (hashRequest.Length < 2 || hashRequest.Length > 512 || BitOps.PopCount ((uint) hashRequest.Length) != 1) {
                logger.InfoFormatted (id.Connection, "Received invalid hash request message. Length was not between 2 and 512 and a power of 2. Received length {0}", hashRequest.Length);
                successful = false;
            }

            // There's a reasonable limit to the requested piece layers too - don't request ones that don't exist?
            // Estimate an upper bound and ignore any requests who want more than that
            if (hashRequest.ProofLayers > BitOps.CeilLog2 (Manager.Torrent!.PieceCount)) {
                logger.InfoFormatted (id.Connection, "Received invalid hash request message. Upper bound on expected piece layer request is {0}. Requested value was: {1}", BitOps.CeilLog2 (Manager.Torrent!.PieceCount), hashRequest.ProofLayers);
                successful = false;
            }

            // Index MUST be a multiple of length, this includes zero
            if (hashRequest.Index % hashRequest.Length != 0) {
                logger.InfoFormatted (id.Connection, "Received invalid hash request message. Index was not an even multiple of length. Index was: {0}, Length was {1}", hashRequest.Index, hashRequest.Length);
                successful = false;
            }

            Memory<byte> buffer = default;
            ByteBufferPool.Releaser bufferReleaser = default;
            if (successful) {
                bufferReleaser = MemoryPool.Default.Rent ((hashRequest.Length + hashRequest.ProofLayers) * 32, out buffer);
                successful = Manager.PieceHashes.TryGetV2Hashes (hashRequest.PiecesRoot, hashRequest.BaseLayer, hashRequest.Index, hashRequest.Length, hashRequest.ProofLayers, buffer.Span, out int bytesWritten);
                buffer = buffer.Slice (0, bytesWritten);
            }

            if (successful) {
                (var message, var releaser) = PeerMessage.Rent<HashesMessage> ();
                message.Initialize (hashRequest.PiecesRoot, hashRequest.BaseLayer, hashRequest.Index, hashRequest.Length, hashRequest.ProofLayers, buffer, bufferReleaser);
                id.MessageQueue.Enqueue (message, releaser);
            } else {
                bufferReleaser.Dispose ();

                (var message, var releaser) = PeerMessage.Rent<HashRejectMessage> ();
                message.Initialize (hashRequest.PiecesRoot, hashRequest.BaseLayer, hashRequest.Index, hashRequest.Length, hashRequest.ProofLayers);
                id.MessageQueue.Enqueue (message, releaser);
            }
        }

        protected virtual void HandleHashesMessage (PeerId id, HashesMessage hashesMessage)
        {
        }

        protected virtual void HandleHashRejectMessage (PeerId id, HashRejectMessage hashRejectMessage)
        {
        }

        public virtual bool ShouldConnect (Peer peer)
        {
            return peer.WaitUntilNextConnectionAttempt.Elapsed >= Settings.GetConnectionRetryDelay (peer.FailedConnectionAttempts)
                && peer.WaitUntilNextConnectionAttempt.Elapsed >= Settings.GetConnectionRetryDelay (peer.CleanedUpCount);
        }

        protected virtual void HandleGenericExtensionMessage (PeerId id, ExtensionMessage extensionMessage)
        {
            // Do nothing
        }

        void HandleHandshakeMessage (PeerId id, HandshakeMessage message)
        {
            throw new NotSupportedException ("The handshake message should be the first message received.");
        }

        protected virtual async void HandlePeerExchangeMessage (PeerId id, PeerExchangeMessage message)
        {
            // Ignore peer exchange messages on private toirrents
            if ((Manager.Torrent != null && Manager.Torrent.IsPrivate) || !Manager.Settings.AllowPeerExchange) {
                Manager.RaisePeersFound (new PeerExchangePeersAdded (Manager, 0, 0, id));
            } else {
                // If we already have lots of peers, don't process the messages anymore.
                if ((Manager.Peers.Available + Manager.OpenConnections) >= Manager.Settings.MaximumConnections)
                    return;

                var newPeers = PeerInfo.FromCompact (message.Added.Span, AddressFamily.InterNetwork);
                for (int i = 0; i < newPeers.Count && i < message.AddedDotF.Length; i++)
                    newPeers[i] = new PeerInfo (newPeers[i].ConnectionUri, newPeers[i].PeerId, (message.AddedDotF.Span[i] & 0x2) == 0x2);

                var newPeers2 = PeerInfo.FromCompact (message.Added6.Span, AddressFamily.InterNetworkV6);
                for (int i = 0; i < newPeers2.Count && i < message.Added6DotF.Length; i++)
                    newPeers2[i] = new PeerInfo (newPeers2[i].ConnectionUri, newPeers2[i].PeerId, (message.Added6DotF.Span[i] & 0x2) == 0x2);

                int count = await Manager.AddPeersAsync (newPeers) + await Manager.AddPeersAsync (newPeers2);
                Manager.RaisePeersFound (new PeerExchangePeersAdded (Manager, count, newPeers.Count + newPeers2.Count, id));
            }
        }

        protected virtual void HandleLtChat (PeerId id, LTChat message)
        {

        }

        protected virtual void HandleLtMetadataMessage (PeerId id, LTMetadata message)
        {
            if (message.MetadataMessageType == LTMetadata.MessageType.Request) {
                id.MessageQueue.Enqueue (Manager.HasMetadata
                    ? new LTMetadata (id.ExtensionSupports, LTMetadata.MessageType.Data, message.Piece, Manager.Torrent!.InfoMetadata)
                    : new LTMetadata (id.ExtensionSupports, LTMetadata.MessageType.Reject, message.Piece));
            }
        }

        protected virtual void HandleAllowedFastMessage (PeerId id, AllowedFastMessage message)
        {
            if (!Manager.Bitfield[message.PieceIndex])
                id.IsAllowedFastPieces.Add (message.PieceIndex);
        }

        protected virtual void HandleSuggestedPieceMessage (PeerId id, SuggestPieceMessage message)
        {
            id.SuggestedPieces.Add (message.PieceIndex);
        }

        protected virtual void HandleRejectRequestMessage (PeerId id, RejectRequestMessage message)
        {
            Manager.PieceManager.RequestRejected (id, new BlockInfo (message.PieceIndex, message.StartOffset, message.RequestLength));
        }

        protected virtual void HandleHaveNoneMessage (PeerId id, HaveNoneMessage message)
        {
            id.MutableBitField.SetAll (false);
            id.Peer.IsSeeder = false;
            SetAmInterestedStatus (id, false);
        }

        protected virtual void HandleHaveAllMessage (PeerId id, HaveAllMessage message)
        {
            id.MutableBitField.SetAll (true);
            id.Peer.IsSeeder = true;
            SetAmInterestedStatus (id, Manager.PieceManager.IsInteresting (id));
        }

        protected virtual void HandleUnchokeMessage (PeerId id, UnchokeMessage message)
        {
            id.IsChoking = false;

            // Add requests to the peers message queue
            Manager.PieceManager.AddPieceRequests (id);
        }

        protected virtual void HandleBitfieldMessage (PeerId id, BitfieldMessage message)
        {
            id.MutableBitField.From (message.BitField);
            id.Peer.IsSeeder = (id.BitField.AllTrue);

            SetAmInterestedStatus (id, Manager.PieceManager.IsInteresting (id));
        }

        protected virtual void HandleCancelMessage (PeerId id, CancelMessage message)
        {
            if (id.MessageQueue.TryCancelRequest (message.PieceIndex, message.StartOffset, message.RequestLength))
                Interlocked.Decrement (ref id.isRequestingPiecesCount);
        }

        protected virtual void HandleChokeMessage (PeerId id, ChokeMessage message)
        {
            id.IsChoking = true;
            if (!id.SupportsFastPeer)
                Manager.PieceManager.CancelRequests (id);

            // Try to run an unchoke review.
            if (Manager.UploadingTo < Manager.Settings.UploadSlots)
                Unchoker.UnchokeReview ();
        }

        protected virtual void HandleInterestedMessage (PeerId id, InterestedMessage message)
        {
            id.IsInterested = true;
            // Try to run an unchoke review.
            if (Manager.UploadingTo < Manager.Settings.UploadSlots)
                Unchoker.UnchokeReview ();
        }

        protected virtual void HandleExtendedHandshakeMessage (PeerId id, ExtendedHandshakeMessage message)
        {
            // FIXME: Use the 'version' information
            // FIXME: Recreate the uri? Give warning?
            if (message.LocalPort > 0)
                id.Peer.LocalPort = message.LocalPort;

            // If MaxRequests is zero, or negative, ignore it.
            if (message.MaxRequests > 0)
                id.MaxSupportedPendingRequests = message.MaxRequests;
            else
                logger.InfoFormatted (id.Connection, "Invalid value for libtorrent extension handshake 'MaxRequests' {0}", message.MaxRequests);

            // Bugfix for MonoTorrent older than 1.0.19
            if (id.ClientApp.Client == ClientApp.MonoTorrent)
                id.MaxSupportedPendingRequests = Math.Max (id.MaxSupportedPendingRequests, 192);

            id.ExtensionSupports = message.Supports;

            if (id.ExtensionSupports.Supports (PeerExchangeMessage.Support.Name)) {
                if (Manager.HasMetadata && !Manager.Torrent!.IsPrivate)
                    id.PeerExchangeManager = new PeerExchangeManager (Manager, id);
            }
        }

        protected virtual void HandleKeepAliveMessage (PeerId id, KeepAliveMessage message)
        {
            id.LastMessageReceived.Restart ();
        }

        protected virtual void HandleNotInterested (PeerId id, NotInterestedMessage message)
        {
            id.IsInterested = false;
        }

        static ICache<CacheableHashSet<IRequester>> PeersInvolvedCache = new SynchronizedCache<CacheableHashSet<IRequester>> (() => new CacheableHashSet<IRequester> ());
        class CacheableHashSet<T> : HashSet<T>, ICacheable
        {
            public void Initialise ()
                => Clear ();
        }

        protected virtual void HandlePieceMessage (PeerId id, PieceMessage message, PeerMessage.Releaser releaser)
        {
            id.PiecesReceived++;
            var peersInvolved = PeersInvolvedCache.Dequeue ();
            if (Manager.PieceManager.PieceDataReceived (id, message, out bool pieceComplete, peersInvolved)) {
                if (peersInvolved.Count == 0) {
                    PeersInvolvedCache.Enqueue (peersInvolved);
                    peersInvolved = null;
                }
                WritePieceAsync (message, releaser, pieceComplete, peersInvolved);
            } else
                releaser.Dispose ();
            // Keep adding new piece requests to this peers queue until we reach the max pieces we're allowed queue
            Manager.PieceManager.AddPieceRequests (id);
        }

        readonly Dictionary<int, (int blocksWritten, CacheableHashSet<IRequester>? peersInvolved)> BlocksWrittenPerPiece = new Dictionary<int, (int blocksWritten, CacheableHashSet<IRequester>? peersInvolved)> ();
        async void WritePieceAsync (PieceMessage message, PeerMessage.Releaser releaser, bool pieceComplete, CacheableHashSet<IRequester>? peersInvolved)
        {
            BlockInfo block = new BlockInfo (message.PieceIndex, message.StartOffset, message.RequestLength);
            try {
                using (releaser)
                    await DiskManager.WriteAsync (Manager, block, message.Data);
                if (Cancellation.IsCancellationRequested)
                    return;
            } catch (Exception ex) {
                Manager.TrySetError (Reason.WriteFailure, ex);
                return;
            }

            if (!BlocksWrittenPerPiece.TryGetValue (block.PieceIndex, out (int blocksWritten, CacheableHashSet<IRequester>? peersInvolved) data))
                data = (0, peersInvolved);

            // Increment the number of blocks, and keep storing 'peersInvolved' until it's non-null. It will be non-null when the
            // final piece is received.
            data = (data.blocksWritten + 1, data.peersInvolved ?? peersInvolved);
            if (data.blocksWritten != Manager.Torrent!.BlocksPerPiece (block.PieceIndex)) {
                BlocksWrittenPerPiece[block.PieceIndex] = data;
                return;
            }

            // All blocks have been written for this piece have been written!
            BlocksWrittenPerPiece.Remove (block.PieceIndex);
            peersInvolved = data.peersInvolved!;

            // Hashcheck the piece as we now have all the blocks.
            // BEP52: Support validating both SHA1 *and* SHA256.
            using var byteBuffer = MemoryPool.Default.Rent (Manager.InfoHashes.GetMaxByteCount (), out Memory<byte> hashMemory);
            var hashes = new PieceHash (hashMemory);
            bool successful = false;
            try {
                successful = await DiskManager.GetHashAsync (Manager, block.PieceIndex, hashes);
                if (Cancellation.IsCancellationRequested)
                    return;
            } catch (Exception ex) {
                Manager.TrySetError (Reason.ReadFailure, ex);
                return;
            }

            bool result = successful && Manager.PieceHashes.IsValid (hashes, block.PieceIndex);
            Manager.OnPieceHashed (block.PieceIndex, result, 1, 1);
            Manager.PieceManager.PieceHashed (block.PieceIndex);
            if (!result)
                Manager.HashFails++;

            foreach (PeerId peer in peersInvolved) {
                peer.Peer.HashedPiece (result);
                if (peer.Peer.TotalHashFails == 5)
                    ConnectionManager.CleanupSocket (Manager, peer);
            }
            PeersInvolvedCache.Enqueue (peersInvolved);

            // If the piece was successfully hashed, enqueue a new "have" message to be sent out
            if (result)
                Manager.finishedPieces.Enqueue (block.PieceIndex);
        }

        protected virtual void HandlePortMessage (PeerId id, PortMessage message)
        {
            id.Port = (ushort) message.Port;
        }

        protected virtual void HandleRequestMessage (PeerId id, RequestMessage message)
        {
            // You should only be able to request pieces within range.
            if (message.PieceIndex < 0 || message.PieceIndex >= Manager.Torrent!.PieceCount)
                throw new MessageException ($"Illegal piece request received. Peer requested piece index {message.PieceIndex} but the supported range is between 0 and #{Manager.Torrent!.PieceCount - 1}.");

            // You should only be able to request data within the bounds of the requested piece.
            if (message.StartOffset < 0 || message.StartOffset >= Manager.Torrent!.PieceLength)
                throw new MessageException ($"Illegal piece request received. Peer requested start offset {message.StartOffset} but the supported range is between 0 and #{Manager.Torrent!.PieceLength}.");

            // You can only request between 1 and 16KiB of data.
            if (message.RequestLength > RequestMessage.MaxSize || message.RequestLength < RequestMessage.MinSize)
                throw new MessageException ($"Illegal piece request received. Peer requested {message.RequestLength} bytes.");

            // If we're not choking the peer, enqueue the message right away
            if (!id.AmChoking) {
                Interlocked.Increment (ref id.isRequestingPiecesCount);
                (var m, var releaser) = PeerMessage.Rent<PieceMessage> ();
                m.Initialize (message.PieceIndex, message.StartOffset, message.RequestLength);
                id.MessageQueue.Enqueue (m, releaser);
            }

            // If the peer supports fast peer and the requested piece is one of the allowed pieces, enqueue it
            // otherwise send back a reject request message
            else if (id.SupportsFastPeer) {
                if (id.AmAllowedFastPieces.Span.IndexOf (message.PieceIndex) != -1) {
                    Interlocked.Increment (ref id.isRequestingPiecesCount);
                    (var m, var releaser) = PeerMessage.Rent<PieceMessage> ();
                    m.Initialize (message.PieceIndex, message.StartOffset, message.RequestLength);
                    id.MessageQueue.Enqueue (m, releaser);
                } else {
                    (var m, var releaser) = PeerMessage.Rent<RejectRequestMessage> ();
                    m.Initialize (message.PieceIndex, message.StartOffset, message.RequestLength);
                    id.MessageQueue.Enqueue (m, releaser);
                }
            }
        }

        protected virtual void HandleHaveMessage (PeerId id, HaveMessage message)
        {
            id.HaveMessageEstimatedDownloadedBytes += Manager.Torrent!.PieceLength;

            // First set the peers bitfield to true for that piece
            id.MutableBitField[message.PieceIndex] = true;

            // Fastcheck to see if a peer is a seeder or not
            id.Peer.IsSeeder = id.BitField.AllTrue;

            // We can do a fast check to see if the peer is interesting or not when we receive a Have Message.
            // If the peer just received a piece we don't have, he's interesting. Otherwise his state is unchanged
            if (!Manager.Bitfield[message.PieceIndex])
                SetAmInterestedStatus (id, true);
        }

        public virtual void HandlePeerConnected (PeerId id)
        {
            Manager.RaisePeerConnected (id);

            if (CanAcceptConnections && ShouldConnect (id.Peer)) {
                (var bundle, var releaser) = PeerMessage.Rent<MessageBundle> ();

                AppendBitfieldMessage (id, bundle);
                AppendExtendedHandshake (id, bundle);
                AppendFastPieces (id, bundle);

                id.MessageQueue.Enqueue (bundle, releaser);

                foreach (var peer in Manager.Peers.ConnectedPeers)
                    if (peer != id && peer.PeerExchangeManager != null)
                        peer.PeerExchangeManager.OnAdd (id);
            } else {
                ConnectionManager.CleanupSocket (Manager, id);
            }
        }

        public virtual void HandlePeerDisconnected (PeerId id)
        {
            foreach (var peer in Manager.Peers.ConnectedPeers)
                if (peer != id && peer.PeerExchangeManager != null)
                    peer.PeerExchangeManager.OnDrop (id);

            Manager.RaisePeerDisconnected (id);
        }

        protected virtual void AppendExtendedHandshake (PeerId id, MessageBundle bundle)
        {
            if (id.SupportsLTMessages)
                bundle.Add (new ExtendedHandshakeMessage (Manager.Torrent?.IsPrivate ?? false, Manager.Torrent != null ? Manager.Torrent.InfoMetadata.Length : (int?) null, Manager.Engine!.GetOverrideOrActualListenPort (id.Connection.Uri.Scheme) ?? -1), default);
        }

        protected virtual void AppendFastPieces (PeerId id, MessageBundle bundle)
        {
            // Now we will enqueue a FastPiece message for each piece we will allow the peer to download
            // even if they are choked
            if (id.SupportsFastPeer) {
                (var msg, var releaser) = PeerMessage.Rent<AllowedFastBundle> ();
                msg.Initialize (id.AmAllowedFastPieces.Span);
                bundle.Add (msg, releaser);
            }
        }

        protected virtual void AppendBitfieldMessage (PeerId id, MessageBundle bundle)
        {
            if (id.SupportsFastPeer) {
                if (Manager.Bitfield.AllFalse)
                    bundle.Add (HaveNoneMessage.Instance, default);

                else if (Manager.Bitfield.AllTrue)
                    bundle.Add (HaveAllMessage.Instance, default);

                else
                    bundle.Add (new BitfieldMessage (Manager.Bitfield), default);
            } else {
                bundle.Add (new BitfieldMessage (Manager.Bitfield), default);
            }
        }

        protected void PreLogicTick (int counter)
        {
            var ninetySeconds = TimeSpan.FromSeconds (90);
            var onhundredAndEightySeconds = TimeSpan.FromSeconds (180);

            SendAnnounces ();

            // The 'AmInterested' status is dependent on whether or not the set of IPiecePicker's
            // associated with the TorrentManager determine if any pieces are ready to be requested.
            // There's no event which will be raised each time this occurs, so just periodically
            // refresh peers.
            if (!lastRefreshAllPeers.IsRunning || lastRefreshAllPeers.Elapsed > TimeSpan.FromSeconds (5)) {
                lastRefreshAllPeers = ValueStopwatch.StartNew ();
                RefreshAmInterestedStatusForAllPeers ();
                CloseConnectionsForStalePeers ();
            }
            Manager.Peers.UpdatePeerCounts ();

            for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++) {
                var id = Manager.Peers.ConnectedPeers[i];

                // Close connections if no messages have been received.
                if (id.LastMessageReceived.Elapsed > onhundredAndEightySeconds) {
                    ConnectionManager.CleanupSocket (Manager, id);
                    i--;
                    continue;
                }

                // Send keepalives if needed.
                if (id.LastMessageSent.Elapsed > ninetySeconds) {
                    id.LastMessageSent.Restart ();
                    id.MessageQueue.Enqueue (KeepAliveMessage.Instance, default);
                }

                // Process any pending queues.
                ConnectionManager.TryProcessQueue (Manager, id);
            }

            //Execute initial logic for individual peers
            if (counter % (1000 / ClientEngine.TickLength) == 0) {   // Call it every second... ish
                Manager.Monitor.Tick ();
                Manager.UpdateLimiters ();
            }
        }

        public virtual void Tick (int counter)
        {
            PreLogicTick (counter);
            if (Manager.State == TorrentState.Downloading)
                DownloadLogic (counter);
            else if (Manager.State == TorrentState.Seeding)
                SeedingLogic (counter);
            PostLogicTick (counter);
        }

        void PostLogicTick (int counter)
        {
            // If any files were changed from DoNotDownload -> Any other priority, then we should hash them if they
            // had been skipped in the original hashcheck.
            _ = TryHashPendingFilesAsync ();

            if (Manager.finishedPieces.Count > 0)
                SendHaveMessagesToAll ();

            for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++) {
                var id = Manager.Peers.ConnectedPeers[i];

                if (!id.LastPeerExchangeReview.IsRunning || id.LastPeerExchangeReview.Elapsed > TimeSpan.FromMinutes (1)) {
                    id.PeerExchangeManager?.OnTick ();
                    id.LastPeerExchangeReview.Restart ();
                }

                int maxRequests = PieceManager.NormalRequestAmount + (int) (id.Monitor.DownloadRate / 1024.0 / PieceManager.BonusRequestPerKb);
                maxRequests = Math.Min (id.MaxSupportedPendingRequests, maxRequests);
                maxRequests = Math.Max (2, maxRequests);
                id.MaxPendingRequests = maxRequests;

                id.Monitor.Tick ();
            }

            Manager.PieceManager.AddPieceRequests (Manager.Peers.ConnectedPeers);
        }

        async void SendAnnounces ()
        {
            try {
                var dhtAnnounce = Manager.DhtAnnounceAsync ();
                var localPeerAnnounce = Manager.LocalPeerAnnounceAsync ();
                var trackerAnnounce = Manager.TrackerManager.AnnounceAsync (CancellationToken.None);

                try { await dhtAnnounce; } catch (Exception ex) { logger.Exception (ex, "Error performing dht announce"); }
                try { await localPeerAnnounce; } catch (Exception ex) { logger.Exception (ex, "Error performing local peer announce"); }
                try { await trackerAnnounce; } catch (Exception ex) { logger.Exception (ex, "Error performing tracker announce"); }
            } catch (Exception ex) {
                logger.Exception (ex, "Error sending timed announces");
            }
        }

        void CloseConnectionsForStalePeers ()
        {
            for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++) {
                var id = Manager.Peers.ConnectedPeers[i];

                if (id.AmRequestingPiecesCount > 0) {
                    if (!id.LastBlockReceived.IsRunning)
                        id.LastBlockReceived.Restart ();

                    if (id.LastBlockReceived.Elapsed > Settings.StaleRequestTimeout) {
                        ConnectionManager.CleanupSocket (Manager, id);
                        i--;
                        continue;
                    }
                } else {
                    id.LastBlockReceived.Reset ();
                }
            }
        }

        void DownloadLogic (int counter)
        {
            if (ClientEngine.SupportsWebSeed && (DateTime.Now - Manager.StartTime) > Settings.WebSeedDelay && (Manager.Monitor.DownloadRate < Settings.WebSeedSpeedTrigger || Settings.WebSeedSpeedTrigger == 0)) {
                foreach (Uri uri in Manager.Torrent!.HttpSeeds) {
                    var peer = new Peer (new PeerInfo (uri, CreatePeerId ()));
                    if (Manager.Peers.Contains (peer) || Manager.Peers.ConnectedPeers.Any (p => p.Uri == uri))
                        continue;

                    var connection = new HttpPeerConnection (Manager, Settings.WebSeedConnectionTimeout, Manager.Engine!.Factories, uri);
                    // Unsupported connection type.
                    if (connection == null)
                        continue;

                    var id = new PeerId (peer, connection, new BitField (Manager.Bitfield.Length).SetAll (true), Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, new Software (peer.Info.PeerId));
                    id.IsChoking = false;
                    Manager.Peers.ConnectedPeers.Add (id);
                    Interlocked.Increment (ref ConnectionManager.openConnections);
                    Manager.RaisePeerConnected (id);
                    ConnectionManager.ReceiveMessagesAsync (id.Connection, id.Decryptor, Manager.DownloadLimiters, id.Monitor, Manager, id);
                    if (!Manager.Complete) {
                        SetAmInterestedStatus (id, true);
                        id.MessageQueue.SetReady ();
                        ConnectionManager.TryProcessQueue (Manager, id);
                    }
                }
            }

            // Remove inactive peers we haven't heard from if we're downloading
            if (Manager.State == TorrentState.Downloading && Manager.lastCalledInactivePeerManager + TimeSpan.FromSeconds (5) < DateTime.Now) {
                Manager.InactivePeerManager.TimePassed ();
                Manager.lastCalledInactivePeerManager = DateTime.Now;
            }

            Unchoker.UnchokeReview ();
        }

        void SeedingLogic (int counter)
        {
            Unchoker.UnchokeReview ();
        }

        protected virtual void SetAmInterestedStatus (PeerId id, bool interesting)
        {
            if (interesting && !id.AmInterested) {
                id.AmInterested = true;
                id.MessageQueue.Enqueue (InterestedMessage.Instance, default);

                // He's interesting, so attempt to queue up any FastPieces (if that's possible)
                Manager.PieceManager.AddPieceRequests (id);
            } else if (!interesting && id.AmInterested) {
                id.AmInterested = false;
                id.MessageQueue.Enqueue (NotInterestedMessage.Instance, default);
            }
        }

        internal async ReusableTask TryHashPendingFilesAsync ()
        {
            // If we cannot handle peer messages then we should not try to async hash.
            // This adds a little bit of a double meaning to the property (for now).
            // Any mode which doesn't allow processing peer messages also does not allow
            // partial hashing.
            if (hashingPendingFiles || !Manager.HasMetadata || !CanHandleMessages)
                return;

            // FIXME: Handle errors from DiskManager and also handle cancellation if the Mode is replaced.
            hashingPendingFiles = true;
            try {
                using var hashBuffer = MemoryPool.Default.Rent (Manager.InfoHashes.GetMaxByteCount (), out Memory<byte> hashMemory);
                var hashes = new PieceHash (hashMemory);
                foreach (var file in Manager.Files) {
                    // If the start piece *and* end piece have been hashed, then every piece in between must've been hashed!
                    if (file.Priority != Priority.DoNotDownload && (Manager.UnhashedPieces[file.StartPieceIndex] || Manager.UnhashedPieces[file.EndPieceIndex])) {
                        for (int index = file.StartPieceIndex; index <= file.EndPieceIndex; index++) {
                            if (Manager.UnhashedPieces[index]) {
                                var successful = await DiskManager.GetHashAsync (Manager, index, hashes);
                                Cancellation.Token.ThrowIfCancellationRequested ();

                                bool hashPassed = successful && Manager.PieceHashes.IsValid (hashes, index);
                                Manager.OnPieceHashed (index, hashPassed, 1, 1);

                                if (hashPassed)
                                    Manager.finishedPieces.Enqueue (index);
                            }
                        }
                    }
                }
            } finally {
                hashingPendingFiles = false;
            }
        }

        void SendHaveMessagesToAll ()
        {
            if (Manager.finishedPieces.Count == 0 || (lastSendHaveMessage.IsRunning && lastSendHaveMessage.ElapsedMilliseconds < 5000))
                return;

            lastSendHaveMessage = ValueStopwatch.StartNew ();

            foreach (PeerId peer in Manager.Peers.ConnectedPeers) {
                (var bundle, var releaser) = PeerMessage.Rent<HaveBundle> ();
                foreach (int pieceIndex in Manager.finishedPieces)
                    if (!Settings.AllowHaveSuppression || !peer.BitField[pieceIndex])
                        bundle.Add (pieceIndex);

                if (bundle.Count == 0)
                    releaser.Dispose ();
                else
                    peer.MessageQueue.Enqueue (bundle, releaser);
            }

            Manager.finishedPieces.Clear ();
        }

        protected void RefreshAmInterestedStatusForAllPeers ()
        {
            foreach (PeerId peer in Manager.Peers.ConnectedPeers) {
                bool isInteresting = Manager.PieceManager.IsInteresting (peer);
                SetAmInterestedStatus (peer, isInteresting);
            }
        }

        public void Dispose ()
        {
            Cancellation.Cancel ();
        }


        static int webSeedId;
        internal static BEncodedString CreatePeerId ()
        {
            string peerId = "-WebSeed-";
            peerId += Interlocked.Increment (ref webSeedId).ToString ().PadLeft (20 - peerId.Length, '0');
            return peerId;
        }
    }
}
