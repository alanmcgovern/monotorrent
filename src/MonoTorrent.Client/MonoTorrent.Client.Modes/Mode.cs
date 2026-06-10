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
using System.Text;
using System.Threading;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Logging;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.Libtorrent;
using MonoTorrent.PiecePicking;

using ReusableTasks;

namespace MonoTorrent.Client.Modes
{
    abstract class Mode : IMode, IMessageHandler
    {
        static readonly Logger logger = Logger.Create (nameof (Mode));

        bool hashingPendingFiles;
        bool shouldHashPendingFiles;
        ValueStopwatch lastSendHaveMessage;
        ValueStopwatch lastRefreshAllPeers;

        protected CancellationTokenSource Cancellation { get; }
        protected ConnectionManager ConnectionManager { get; }
        protected DiskManager DiskManager { get; }
        protected TorrentManager Manager { get; }
        protected EngineSettings Settings { get; }
        protected IUnchoker Unchoker { get; }

        public virtual bool CanAcceptConnections => true;
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

            shouldHashPendingFiles = true;
        }

        public virtual void HandleFilePriorityChanged (ITorrentManagerFile file, Priority oldPriority)
        {
            shouldHashPendingFiles = true;
        }

        public virtual void HandleMessage (PeerId id, HashRequestMessage hashRequest)
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

            /// FIXME: StructMessages: We can now preallocate the entire message buffer and write all bytes directly to it.
            Memory<byte> buffer = default;
            ByteBufferPool.Releaser bufferReleaser = default;
            var piecesRoot = new MerkleRoot (hashRequest.PiecesRoot);
            if (successful) {
                bufferReleaser = MemoryPool.Default.Rent ((hashRequest.Length + hashRequest.ProofLayers) * 32, out buffer);
                successful = Manager.PieceHashes.TryGetV2Hashes (piecesRoot, hashRequest.BaseLayer, hashRequest.Index, hashRequest.Length, hashRequest.ProofLayers, buffer.Span, out int bytesWritten);
                buffer = buffer.Slice (0, bytesWritten);
            }

            // FIXME: StructMessages - we still should be able to precompute the size of the message. Don't hardcode it.
            if (successful) {
                (var msgBuffer, var msgBufferReleaser) = MessageEncoder.WriteHashes(piecesRoot.Span, hashRequest.BaseLayer,hashRequest.Index, hashRequest.Length, hashRequest.ProofLayers, buffer.Span);
                id.MessageQueue.Enqueue (msgBuffer, msgBufferReleaser);
                bufferReleaser.Dispose ();
            } else {
                (var msgBuffer, var msgBufferReleaser) = MessageEncoder.WriteHashReject(piecesRoot.Span, hashRequest.BaseLayer, hashRequest.Index, hashRequest.Length, hashRequest.ProofLayers);
                id.MessageQueue.Enqueue (msgBuffer, msgBufferReleaser);
            }
        }

        public virtual void HandleMessage (PeerId id, KeepAliveMessage message)
        {
            // Every message automatically resets the 'last message received' timer, so do nothing here.
        }

        public virtual void HandleMessage (PeerId id, HashesMessage message)
        {
        }

        public virtual void HandleMessage (PeerId id, HashRejectMessage message)
        {
        }

        public virtual bool ShouldConnect (Peer peer)
        {
            return peer.WaitUntilNextConnectionAttempt.Elapsed >= Settings.GetConnectionRetryDelay (peer.FailedConnectionAttempts)
                && peer.WaitUntilNextConnectionAttempt.Elapsed >= Settings.GetConnectionRetryDelay (peer.CleanedUpCount);
        }

        void HandleHandshakeMessage (PeerId id, HandshakeMessage message)
        {
            throw new NotSupportedException ("The handshake message should be the first message received.");
        }

        public virtual void HandleMessage (PeerId id, Extended.PeerExchangeMessage message)
        {
            // Ignore peer exchange messages on private toirrents
            if ((Manager.Torrent != null && Manager.Torrent.IsPrivate) || !Manager.Settings.AllowPeerExchange) {
                Manager.RaisePeersFound (new PeerExchangePeersAdded (Manager, 0, 0, id));
            } else {
                // If we already have lots of peers, don't process the messages anymore.
                if ((Manager.Peers.Available + Manager.OpenConnections) >= Manager.Settings.MaximumConnections)
                    return;

                var newPeers = PeerInfo.FromCompact (message.Added, AddressFamily.InterNetwork);
                for (int i = 0; i < newPeers.Count && i < message.AddedDotF.Length; i++)
                    newPeers[i] = new PeerInfo (newPeers[i].ConnectionUri, newPeers[i].PeerId, (message.AddedDotF[i] & 0x2) == 0x2);

                var newPeers2 = PeerInfo.FromCompact (message.Added6, AddressFamily.InterNetworkV6);
                for (int i = 0; i < newPeers2.Count && i < message.Added6DotF.Length; i++)
                    newPeers2[i] = new PeerInfo (newPeers2[i].ConnectionUri, newPeers2[i].PeerId, (message.Added6DotF[i] & 0x2) == 0x2);

                int count = Manager.AddPeers (newPeers, prioritise: true, fromTracker: false) + Manager.AddPeers (newPeers2, prioritise: true, fromTracker: false);
                Manager.RaisePeersFound (new PeerExchangePeersAdded (Manager, count, newPeers.Count + newPeers2.Count, id));
            }
        }

        public virtual void HandleMessage (PeerId id, Extended.MetadataMessage message)
        {
            if (message.MessageType == Extended.MetadataMessage.MetadataMessageType.Request) {
                id.MessageQueue.Enqueue (Manager.HasMetadata
                    ? MessageEncoder.Extended.WriteMetadata (id.ExtensionSupports, Extended.MetadataMessage.MetadataMessageType.Data, message.Piece, Manager.Torrent!.InfoMetadata.Span)
                    : MessageEncoder.Extended.WriteMetadata (id.ExtensionSupports, Extended.MetadataMessage.MetadataMessageType.Reject, message.Piece, default)
                );
            }
        }

        public virtual void HandleMessage (PeerId id, AllowedFastMessage message)
        {
            if (!Manager.Bitfield[message.PieceIndex])
                id.IsAllowedFastPieces.Add (message.PieceIndex);
        }

        public virtual void HandleMessage (PeerId id, SuggestMessage message)
        {
            id.SuggestedPieces.Add (message.PieceIndex);
        }

        public virtual void HandleMessage (PeerId id, RejectRequestMessage message)
        {
            Manager.PieceManager.RequestRejected (id, new BlockInfo (message.PieceIndex, message.StartOffset, message.RequestLength));
        }

        public virtual void HandleMessage (PeerId id, HaveNoneMessage message)
        {
            id.MutableBitField.SetAll (false);
            id.Peer.IsSeeder = false;
            SetAmInterestedStatus (id, false);
        }

        public virtual void HandleMessage (PeerId id, HaveAllMessage message)
        {
            id.MutableBitField.SetAll (true);
            id.Peer.IsSeeder = true;
            SetAmInterestedStatus (id, Manager.PieceManager.IsInteresting (id));
        }

        public virtual void HandleMessage (PeerId id, UnchokeMessage message)
        {
            id.IsChoking = false;

            // Add requests to the peers message queue
            Manager.PieceManager.AddPieceRequests (id);
        }

        public virtual void HandleMessage (PeerId id, BitfieldMessage message)
        {
            id.MutableBitField.From (message.BitField);
            id.Peer.IsSeeder = (id.BitField.AllTrue);

            SetAmInterestedStatus (id, Manager.PieceManager.IsInteresting (id));
        }

        public virtual void HandleMessage (PeerId id, CancelMessage message)
        {
            if (id.MessageQueue.TryCancelRequest (message.PieceIndex, message.StartOffset, message.RequestLength))
                Interlocked.Decrement (ref id.isRequestingPiecesCount);
        }

        public virtual void HandleMessage (PeerId id, ChokeMessage message)
        {
            id.IsChoking = true;
            if (!id.SupportsFastPeer)
                Manager.PieceManager.CancelRequests (id);

            // Try to run an unchoke review.
            if (Manager.UploadingTo < Manager.Settings.UploadSlots)
                Unchoker.UnchokeReview ();
        }

        public virtual void HandleMessage (PeerId id, InterestedMessage message)
        {
            id.IsInterested = true;
            // Try to run an unchoke review.
            if (Manager.UploadingTo < Manager.Settings.UploadSlots)
                Unchoker.UnchokeReview ();
        }

        public virtual void HandleMessage (PeerId id, Extended.HandshakeMessage message)
        {
            // FIXME: Use the 'version' information
            // FIXME: Recreate the uri? Give warning?
            if (message.Port.HasValue && message.Port.Value > 0)
                id.Peer.LocalPort = message.Port.Value;

            // If MaxRequests is zero, or negative, ignore it.
            if (message.MaxRequests.HasValue && message.MaxRequests.Value > 10)
                id.MaxSupportedPendingRequests = message.MaxRequests.Value;
            else
                logger.InfoFormatted (id.Connection, "Invalid value for libtorrent extension handshake 'MaxRequests' {0}", message.MaxRequests.GetValueOrDefault (int.MinValue));

            var supports = new ExtensionSupports ();
            var reader = new BEncodeReader (message.Mappings.Span);
            reader.ExpectDictionaryBegin ();
            while (reader.TryReadKey (out var key)) {
                reader.CaptureInteger (message.Mappings);
                supports.Add (new ExtensionSupport (key, (byte) reader.Integer));
            }
            id.ExtensionSupports = supports;

            if (id.ExtensionSupports.Supports (MessageEncoder.Extended.PeerExchangeSupport.Name)) {
                if (Manager.HasMetadata && !Manager.Torrent!.IsPrivate)
                    id.PeerExchangeManager = new PeerExchangeManager (Manager, id);
            }
        }

        public virtual void HandleMessage (PeerId id, NotInterestedMessage message)
        {
            id.IsInterested = false;
        }

        static ICache<CacheableHashSet<IRequester>> PeersInvolvedCache = new SynchronizedCache<CacheableHashSet<IRequester>> (() => new CacheableHashSet<IRequester> ());
        class CacheableHashSet<T> : HashSet<T>, ICacheable
        {
            public void Initialise ()
                => Clear ();
        }

        public void HandleMessage (PeerId id, PieceMessage message)
        {
            id.PiecesReceived++;
            var peersInvolved = PeersInvolvedCache.Dequeue ();
            if (Manager.PieceManager.PieceDataReceived (id, message.PieceIndex, message.StartOffset, message.RequestLength, out bool pieceComplete, peersInvolved)) {
                if (peersInvolved.Count == 0) {
                    PeersInvolvedCache.Enqueue (peersInvolved);
                    peersInvolved = null;
                }
                var releaser = MemoryPool.Default.Rent (message.RequestLength, out Memory<byte> pieceData);
                message.Data.CopyTo (pieceData.Span);
                WritePieceAsync (message.PieceIndex, message.StartOffset, message.RequestLength, pieceData, releaser, pieceComplete, peersInvolved);
            }
            // Keep adding new piece requests to this peers queue until we reach the max pieces we're allowed queue
            Manager.PieceManager.AddPieceRequests (id);
        }

        readonly Dictionary<int, (int blocksWritten, CacheableHashSet<IRequester>? peersInvolved)> BlocksWrittenPerPiece = new Dictionary<int, (int blocksWritten, CacheableHashSet<IRequester>? peersInvolved)> ();
        async void WritePieceAsync (int pieceIndex, int startOffset, int requestLength, Memory<byte> pieceData, ByteBufferPool.Releaser releaser, bool pieceComplete, CacheableHashSet<IRequester>? peersInvolved)
        {
            BlockInfo block = new BlockInfo (pieceIndex, startOffset, requestLength);
            try {

                // FIXME: give the diskmanager ownership of the buffer until it's written it durably
                using (releaser)
                    await DiskManager.WriteAsync (Manager, block, pieceData);
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

        public virtual void HandleMessage (PeerId id, PortMessage message)
        {
            id.Port = (ushort) message.Port;
        }

        public virtual void HandleMessage (PeerId id, RequestMessage message)
        {
            // You should only be able to request pieces within range.
            if (message.PieceIndex < 0 || message.PieceIndex >= Manager.Torrent!.PieceCount)
                throw new MessageException ($"Illegal piece request received. Peer requested piece index {message.PieceIndex} but the supported range is between 0 and #{Manager.Torrent!.PieceCount - 1}.");

            // You should only be able to request data within the bounds of the requested piece.
            if (message.StartOffset < 0 || message.StartOffset >= Manager.Torrent!.PieceLength)
                throw new MessageException ($"Illegal piece request received. Peer requested start offset {message.StartOffset} but the supported range is between 0 and #{Manager.Torrent!.PieceLength}.");

            // You can only request between 1 and 16KiB of data.
            if (message.RequestLength > Constants.BlockSize || message.RequestLength <= 0)
                throw new MessageException ($"Illegal piece request received. Peer requested {message.RequestLength} bytes.");

            // If we're not choking the peer, enqueue the message right away
            if (!id.AmChoking) {
                Interlocked.Increment (ref id.isRequestingPiecesCount);
                id.MessageQueue.Enqueue (MessageEncoder.WriteSparsePiece (message.PieceIndex, message.StartOffset, message.RequestLength));
            }

            // If the peer supports fast peer and the requested piece is one of the allowed pieces, enqueue it
            // otherwise send back a reject request message
            else if (id.SupportsFastPeer) {
                if (id.AmAllowedFastPieces.Span.IndexOf (message.PieceIndex) != -1) {
                    Interlocked.Increment (ref id.isRequestingPiecesCount);
                    id.MessageQueue.Enqueue (MessageEncoder.WriteSparsePiece (message.PieceIndex, message.StartOffset, message.RequestLength));
                } else {
                    id.MessageQueue.Enqueue (MessageEncoder.WriteRejectRequest (message.PieceIndex, message.StartOffset, message.RequestLength));
                }
            }
        }

        public virtual void HandleMessage (PeerId id, HaveMessage message)
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
                AppendBitfieldMessage (id);
                AppendExtendedHandshake (id);
                AppendFastPieces (id);

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

        protected void AppendExtendedHandshake (PeerId id)
        {
            if (id.SupportsLTMessages)
                id.MessageQueue.Enqueue (MessageEncoder.Extended.WriteHandshake (GitInfoHelper.ClientVersionMemory, Manager.Torrent?.IsPrivate ?? false, Manager.Torrent != null ? Manager.Torrent.InfoMetadata.Length : (int?) null, Manager.Engine!.GetOverrideOrActualListenPort (id.Connection.Uri.Scheme)));
        }

        protected int AppendFastPieces (PeerId id)
        {
            // Now we will enqueue a FastPiece message for each piece we will allow the peer to download
            // even if they are choked
            if (id.SupportsFastPeer && id.AmAllowedFastPieces.Length > 0) {
                var releaser = MemoryPool.Default.Rent (AllowedFastMessage.EncodedLength * id.AmAllowedFastPieces.Length, out var buffer);
                var b = buffer.Span;
                foreach (var fastPiece in id.AmAllowedFastPieces.Span)
                    b = b.Slice (MessageEncoder.WriteAllowedFast (b, fastPiece));
                id.MessageQueue.Enqueue (buffer, releaser);
            }
            return 0;
        }

        protected virtual void AppendBitfieldMessage (PeerId id)
        {
            if (id.SupportsFastPeer) {
                if (Manager.Bitfield.AllFalse)
                    id.MessageQueue.Enqueue (MessageEncoder.WriteHaveNone ());

                else if (Manager.Bitfield.AllTrue)
                    id.MessageQueue.Enqueue (MessageEncoder.WriteHaveAll ());

                else
                    id.MessageQueue.Enqueue (MessageEncoder.WriteBitfield (Manager.Bitfield));
            } else {
                id.MessageQueue.Enqueue (MessageEncoder.WriteBitfield (Manager.Bitfield));
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
                    id.MessageQueue.Enqueue (MessageEncoder.WriteKeepAlive ());
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
            if (ClientEngine.SupportsWebSeed
            && (DateTime.Now - Manager.StartTime) > Settings.WebSeedDelay
            && (Manager.Monitor.DownloadRate < Settings.WebSeedSpeedTrigger || Settings.WebSeedSpeedTrigger == 0)
            && Manager.OpenConnections < Manager.Settings.MaximumConnections
            && Manager.Engine!.ConnectionManager.OpenConnections < Manager.Engine.Settings.MaximumConnections) {
                foreach (Uri uri in Manager.MagnetLink.Webseeds.Select (x => new Uri (x))) {
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
                id.MessageQueue.Enqueue (MessageEncoder.WriteInterested ());

                // He's interesting, so attempt to queue up any FastPieces (if that's possible)
                Manager.PieceManager.AddPieceRequests (id);
            } else if (!interesting && id.AmInterested) {
                id.AmInterested = false;
                id.MessageQueue.Enqueue (MessageEncoder.WriteNotInterested ());
            }
        }

        internal async ReusableTask TryHashPendingFilesAsync ()
        {
            // If we cannot handle peer messages then we should not try to async hash.
            // This adds a little bit of a double meaning to the property (for now).
            // Any mode which doesn't allow processing peer messages also does not allow
            // partial hashing.
            if (!shouldHashPendingFiles || hashingPendingFiles || !Manager.HasMetadata || Manager.Mode is not IMessageHandler)
                return;

            // We need two bools. These are two prevent double-execution, but also to ensure that if something changes
            // while we're in the middle of hashing pending files we know we need to retry during the next tick.
            shouldHashPendingFiles = false;
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
                var releaser = MemoryPool.Default.Rent (HaveMessage.EncodedLength * Manager.finishedPieces.Count, out var buffer);
                var b = buffer;
                foreach (int pieceIndex in Manager.finishedPieces)
                    if (!Settings.AllowHaveSuppression || !peer.BitField[pieceIndex])
                        b = b.Slice (MessageEncoder.WriteHave (b.Span, pieceIndex));

                buffer = buffer.Slice (0, buffer.Length - b.Length);
                if (buffer.Length > 0)
                    peer.MessageQueue.Enqueue (buffer, releaser);
                else
                    releaser.Dispose ();
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
