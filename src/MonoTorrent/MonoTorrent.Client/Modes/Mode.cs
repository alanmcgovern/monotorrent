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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Modes
{
    abstract class Mode
    {
        bool hashingPendingFiles;

        protected CancellationTokenSource Cancellation { get; }
        protected ConnectionManager ConnectionManager { get; }
        protected DiskManager DiskManager { get; }
        protected TorrentManager Manager { get; }
        protected EngineSettings Settings { get; }

        public virtual bool CanAcceptConnections => true;
        public virtual bool CanHandleMessages => true;
        public virtual bool CanHashCheck => false;
        public abstract TorrentState State { get; }
        public CancellationToken Token => Cancellation.Token;

        protected Mode(TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings)
        {
            Cancellation = new CancellationTokenSource ();
            ConnectionManager = connectionManager;
            DiskManager = diskManager;
            Manager = manager;
            Settings = settings;

            manager.chokeUnchoker = new ChokeUnchokeManager(manager, manager.Settings.MinimumTimeBetweenReviews, manager.Settings.PercentOfMaxRateToSkipReview);
        }

        public void HandleMessage(PeerId id, PeerMessage message)
        {
            if (!CanHandleMessages)
                return;

            if (message is IFastPeerMessage && !id.SupportsFastPeer)
                throw new MessageException("Peer shouldn't support fast peer messages");

            if (message is ExtensionMessage && !id.SupportsLTMessages && !(message is ExtendedHandshakeMessage))
                throw new MessageException("Peer shouldn't support extension messages");

            if (message is HaveMessage)
                HandleHaveMessage(id, (HaveMessage)message);
            else if (message is RequestMessage)
                HandleRequestMessage(id, (RequestMessage)message);
            else if (message is PortMessage)
                HandlePortMessage(id, (PortMessage)message);
            else if (message is PieceMessage)
                HandlePieceMessage(id, (PieceMessage)message);
            else if (message is NotInterestedMessage)
                HandleNotInterested(id, (NotInterestedMessage)message);
            else if (message is KeepAliveMessage)
                HandleKeepAliveMessage(id, (KeepAliveMessage)message);
            else if (message is InterestedMessage)
                HandleInterestedMessage(id, (InterestedMessage)message);
            else if (message is ChokeMessage)
                HandleChokeMessage(id, (ChokeMessage)message);
            else if (message is CancelMessage)
                HandleCancelMessage(id, (CancelMessage)message);
            else if (message is BitfieldMessage)
                HandleBitfieldMessage(id, (BitfieldMessage)message);
            else if (message is UnchokeMessage)
                HandleUnchokeMessage(id, (UnchokeMessage)message);
            else if (message is HaveAllMessage)
                HandleHaveAllMessage(id, (HaveAllMessage)message);
            else if (message is HaveNoneMessage)
                HandleHaveNoneMessage(id, (HaveNoneMessage)message);
            else if (message is RejectRequestMessage)
                HandleRejectRequestMessage(id, (RejectRequestMessage)message);
            else if (message is SuggestPieceMessage)
                HandleSuggestedPieceMessage(id, (SuggestPieceMessage)message);
            else if (message is AllowedFastMessage)
                HandleAllowedFastMessage(id, (AllowedFastMessage)message);
            else if (message is ExtendedHandshakeMessage)
                HandleExtendedHandshakeMessage(id, (ExtendedHandshakeMessage)message);
            else if (message is LTMetadata)
                HandleLtMetadataMessage(id, (LTMetadata)message);
            else if (message is LTChat)
                HandleLtChat(id, (LTChat)message);
            else if (message is PeerExchangeMessage)
                HandlePeerExchangeMessage(id, (PeerExchangeMessage)message);
            else if (message is HandshakeMessage)
                HandleHandshakeMessage(id, (HandshakeMessage)message);
            else if (message is ExtensionMessage)
                HandleGenericExtensionMessage(id, (ExtensionMessage)message);
            else
                throw new MessageException(string.Format("Unsupported message found: {0}", message.GetType().Name));

            if (id.QueueLength > 0 && !id.ProcessingQueue) {
                id.ProcessingQueue = true;
                ConnectionManager.ProcessQueue (Manager, id);
            }
        }

        public bool ShouldConnect(PeerId peer)
        {
            return ShouldConnect(peer.Peer);
        }

        public virtual bool ShouldConnect(Peer peer)
        {
            return true;
        }

        protected virtual void HandleGenericExtensionMessage(PeerId id, ExtensionMessage extensionMessage)
        {
            // Do nothing
        }

        protected virtual void HandleHandshakeMessage(PeerId id, HandshakeMessage message)
        {
            if (!message.ProtocolString.Equals(VersionInfo.ProtocolStringV100))
            {
                Logger.Log(id.Connection, "HandShake.Handle - Invalid protocol in handshake: {0}", message.ProtocolString);
                throw new ProtocolException("Invalid protocol string");
            }

            // If we got the peer as a "compact" peer, then the peerid will be empty. In this case
            // we just copy the one that is in the handshake. 
            if (BEncodedString.IsNullOrEmpty(id.Peer.PeerId))
                id.Peer.PeerId = message.PeerId;

            // If the infohash doesn't match, dump the connection
            if (message.InfoHash != Manager.InfoHash)
            {
                Logger.Log(id.Connection, "HandShake.Handle - Invalid infohash");
                throw new TorrentException("Invalid infohash. Not tracking this torrent");
            }

            // If the peer id's don't match, dump the connection. This is due to peers faking usually
            if (!id.Peer.PeerId.Equals (message.PeerId))
            {
                Logger.Log(id.Connection, "HandShake.Handle - Invalid peerid");
                throw new TorrentException("Supplied PeerID didn't match the one the tracker gave us");
            }

            // Attempt to parse the application that the peer is using
            id.ClientApp = new Software(message.PeerId);
            id.SupportsFastPeer = message.SupportsFastPeer;
            id.SupportsLTMessages = message.SupportsExtendedMessaging;

            // If they support fast peers, create their list of allowed pieces that they can request off me
            if (id.SupportsFastPeer && Manager != null && Manager.HasMetadata)
                id.AmAllowedFastPieces = AllowedFastAlgorithm.Calculate(id.AddressBytes, Manager.InfoHash, (uint)Manager.Torrent.Pieces.Count);
        }

        protected virtual async void HandlePeerExchangeMessage(PeerId id, PeerExchangeMessage message)
        {
            // Ignore peer exchange messages on private toirrents
            if ((Manager.Torrent != null && Manager.Torrent.IsPrivate) || !Manager.Settings.AllowPeerExchange) {
                Manager.RaisePeersFound(new PeerExchangePeersAdded (Manager, 0, 0, id));
            } else {
                // If we already have lots of peers, don't process the messages anymore.
                if ((Manager.Peers.Available + Manager.OpenConnections) >= Manager.Settings.MaximumConnections)
                    return;

                var newPeers = Peer.Decode((BEncodedString)message.Added);
                for (int i = 0; i < newPeers.Count && i < message.AddedDotF.Length; i++) {
                    newPeers[i].IsSeeder = (message.AddedDotF [i] & 0x2) == 0x2;
                }
                int count = await Manager.AddPeersAsync(newPeers);
                Manager.RaisePeersFound(new PeerExchangePeersAdded (Manager, count, newPeers.Count, id));
            }
        }

        protected virtual void HandleLtChat(PeerId id, LTChat message)
        {

        }

        protected virtual void HandleLtMetadataMessage(PeerId id, LTMetadata message)
        {
            if (message.MetadataMessageType == LTMetadata.eMessageType.Request)
            {
                if (Manager.HasMetadata)
                    id.Enqueue(new LTMetadata(id, LTMetadata.eMessageType.Data, message.Piece, Manager.Torrent.Metadata));
                else
                    id.Enqueue(new LTMetadata(id, LTMetadata.eMessageType.Reject, message.Piece));
            }
        }

        protected virtual void HandleAllowedFastMessage(PeerId id, AllowedFastMessage message)
        {
            if (!Manager.Bitfield[message.PieceIndex])
                id.IsAllowedFastPieces.Add(message.PieceIndex);
        }

        protected virtual void HandleSuggestedPieceMessage(PeerId id, SuggestPieceMessage message)
        {
            id.SuggestedPieces.Add(message.PieceIndex);
        }

        protected virtual void HandleRejectRequestMessage(PeerId id, RejectRequestMessage message)
        {
            Manager.PieceManager.Picker.CancelRequest(id, message.PieceIndex, message.StartOffset, message.RequestLength);
        }

        protected virtual void HandleHaveNoneMessage(PeerId id, HaveNoneMessage message)
        {
            id.BitField.SetAll(false);
            id.Peer.IsSeeder = false;
            SetAmInterestedStatus(id, false);
        }

        protected virtual void HandleHaveAllMessage(PeerId id, HaveAllMessage message)
        {
            id.BitField.SetAll(true);
            id.Peer.IsSeeder = true;
            SetAmInterestedStatus(id, Manager.PieceManager.IsInteresting(id));
        }

        protected virtual void HandleUnchokeMessage(PeerId id, UnchokeMessage message)
        {
            id.IsChoking = false;

            // Add requests to the peers message queue
            Manager.PieceManager.AddPieceRequests(id);
        }

        protected virtual void HandleBitfieldMessage(PeerId id, BitfieldMessage message)
        {
            id.BitField = message.BitField;
            id.Peer.IsSeeder = (id.BitField.AllTrue);

            SetAmInterestedStatus(id, Manager.PieceManager.IsInteresting(id));
        }

        protected virtual void HandleCancelMessage(PeerId id, CancelMessage message)
        {
            PeerMessage msg;
            for (int i = 0; i < id.QueueLength; i++)
            {
                msg = id.Dequeue();
                if (!(msg is PieceMessage))
                {
                    id.Enqueue(msg);
                    continue;
                }

                PieceMessage piece = msg as PieceMessage;
                if (!(piece.PieceIndex == message.PieceIndex && piece.StartOffset == message.StartOffset && piece.RequestLength == message.RequestLength))
                {
                    id.Enqueue(msg);
                }
                else
                {
                    id.IsRequestingPiecesCount--;
                }
            }
        }

        protected virtual void HandleChokeMessage(PeerId id, ChokeMessage message)
        {
            id.IsChoking = true;
            if (!id.SupportsFastPeer)
                Manager.PieceManager.Picker.CancelRequests(id);
        }

        protected virtual void HandleInterestedMessage(PeerId id, InterestedMessage message)
        {
            id.IsInterested = true;
        }

        protected virtual void HandleExtendedHandshakeMessage(PeerId id, ExtendedHandshakeMessage message)
        {
            // FIXME: Use the 'version' information
            // FIXME: Recreate the uri? Give warning?
            if (message.LocalPort > 0)
                id.Peer.LocalPort = message.LocalPort;
            id.MaxSupportedPendingRequests = Math.Max(1, message.MaxRequests);
            id.ExtensionSupports = message.Supports;

            if (id.ExtensionSupports.Supports(PeerExchangeMessage.Support.Name))
            {
                if (Manager.HasMetadata && !Manager.Torrent.IsPrivate)
                    id.PeerExchangeManager = new PeerExchangeManager(Manager, id);
            }
        }
        
        protected virtual void HandleKeepAliveMessage(PeerId id, KeepAliveMessage message)
        {
            id.LastMessageReceived.Restart ();
        }

        protected virtual void HandleNotInterested(PeerId id, NotInterestedMessage message)
        {
            id.IsInterested = false;
        }

        protected virtual void HandlePieceMessage(PeerId id, PieceMessage message)
        {
            id.PiecesReceived++;
            var piece = Manager.PieceManager.PieceDataReceived(id, message);
            if (piece != null)
                WritePieceAsync (message, piece);
            else
                ClientEngine.BufferPool.Return(message.Data);
            // Keep adding new piece requests to this peers queue until we reach the max pieces we're allowed queue
            Manager.PieceManager.AddPieceRequests(id);
        }

        HashSet<PeerId> peers = new HashSet<PeerId>();
        async void WritePieceAsync (PieceMessage message, Piece piece)
        {
            long offset = (long) message.PieceIndex * Manager.Torrent.PieceLength + message.StartOffset;

            try {
                await DiskManager.WriteAsync(Manager.Torrent, offset, message.Data, message.RequestLength);
                if (Cancellation.IsCancellationRequested)
                    return;
            } catch (Exception ex) {
                Manager.TrySetError (Reason.WriteFailure, ex);
                return;
            } finally {
                ClientEngine.BufferPool.Return(message.Data);
            }

            piece.TotalWritten++;

            // If we haven't received all the pieces to disk, there's no point in hash checking
            if (!piece.AllBlocksWritten)
                return;

            // Hashcheck the piece as we now have all the blocks.
            byte[] hash;
            try {
                hash = await DiskManager.GetHashAsync(Manager.Torrent, piece.Index);
                if (Cancellation.IsCancellationRequested)
                    return;
            } catch (Exception ex) {
                Manager.TrySetError (Reason.ReadFailure, ex);
                return;
            }

            bool result = hash != null && Manager.Torrent.Pieces.IsValid(hash, piece.Index);
            Manager.OnPieceHashed(piece.Index, result);
            Manager.PieceManager.UnhashedPieces[piece.Index] = false;
            if (!result)
                Manager.HashFails++;

            for (int i = 0; i < piece.Blocks.Length; i++)
                if (piece.Blocks[i].RequestedOff != null)
                    peers.Add((PeerId)piece.Blocks[i].RequestedOff);

            foreach (var peer in peers) {
                peer.Peer.HashedPiece(result);
                if (peer.Peer.TotalHashFails == 5)
                    ConnectionManager.CleanupSocket (Manager, peer);
            }
            peers.Clear ();

            // If the piece was successfully hashed, enqueue a new "have" message to be sent out
            if (result)
                Manager.finishedPieces.Enqueue(new HaveMessage (piece.Index));
        }

        protected virtual void HandlePortMessage(PeerId id, PortMessage message)
        {
            id.Port = (ushort)message.Port;
        }

        protected virtual void HandleRequestMessage(PeerId id, RequestMessage message)
        {
            // If we are not on the last piece and the user requested a stupidly big/small amount of data
            // we will close the connection
            if (Manager.Torrent.Pieces.Count != (message.PieceIndex + 1))
                if (message.RequestLength > RequestMessage.MaxSize || message.RequestLength < RequestMessage.MinSize)
                    throw new MessageException("Illegal piece request received. Peer requested " + message.RequestLength.ToString() + " byte");

            PieceMessage m = new PieceMessage(message.PieceIndex, message.StartOffset, message.RequestLength);

            // If we're not choking the peer, enqueue the message right away
            if (!id.AmChoking)
            {
                id.IsRequestingPiecesCount++;
                id.Enqueue(m);
            }

            // If the peer supports fast peer and the requested piece is one of the allowed pieces, enqueue it
            // otherwise send back a reject request message
            else if (id.SupportsFastPeer && ClientEngine.SupportsFastPeer)
            {
                if (id.AmAllowedFastPieces.Contains(message.PieceIndex))
                {
                    id.IsRequestingPiecesCount++;
                    id.Enqueue(m);
                }
                else
                    id.Enqueue(new RejectRequestMessage(m));
            }
        }

        protected virtual void HandleHaveMessage(PeerId id, HaveMessage message)
        {
            id.HaveMessageEstimatedDownloadedBytes += Manager.Torrent.PieceLength;

            // First set the peers bitfield to true for that piece
            id.BitField[message.PieceIndex] = true;

            // Fastcheck to see if a peer is a seeder or not
            id.Peer.IsSeeder = id.BitField.AllTrue;

            // We can do a fast check to see if the peer is interesting or not when we receive a Have Message.
            // If the peer just received a piece we don't have, he's interesting. Otherwise his state is unchanged
            if (!Manager.Bitfield[message.PieceIndex])
                SetAmInterestedStatus(id, true);
        }

        public virtual void HandlePeerConnected(PeerId id)
        {
            if (CanAcceptConnections) {
                MessageBundle bundle = new MessageBundle();

                AppendBitfieldMessage(id, bundle);
                AppendExtendedHandshake(id, bundle);
                AppendFastPieces(id, bundle);

                id.Enqueue(bundle);
                if (!id.ProcessingQueue) {
                    id.ProcessingQueue = true;
                    ConnectionManager.ProcessQueue (Manager, id);
                }
            } else {
               ConnectionManager.CleanupSocket (Manager, id);
            }
        }

        public virtual void HandlePeerDisconnected(PeerId id)
        {

        }

        protected virtual void AppendExtendedHandshake(PeerId id, MessageBundle bundle)
        {
            if (id.SupportsLTMessages && ClientEngine.SupportsExtended)
                bundle.Messages.Add(new ExtendedHandshakeMessage(Manager.Torrent?.IsPrivate ?? false, Manager.HasMetadata ? Manager.Torrent.Metadata.Length : 0, Settings.ListenPort));
        }

        protected virtual void AppendFastPieces(PeerId id, MessageBundle bundle)
        {
            // Now we will enqueue a FastPiece message for each piece we will allow the peer to download
            // even if they are choked
            if (ClientEngine.SupportsFastPeer && id.SupportsFastPeer)
                for (int i = 0; i < id.AmAllowedFastPieces.Count; i++)
                    bundle.Messages.Add(new AllowedFastMessage(id.AmAllowedFastPieces[i]));

        }

        protected virtual void AppendBitfieldMessage(PeerId id, MessageBundle bundle)
        {
            if (id.SupportsFastPeer && ClientEngine.SupportsFastPeer)
            {
                if (Manager.Bitfield.AllFalse)
                    bundle.Messages.Add(new HaveNoneMessage());

                else if (Manager.Bitfield.AllTrue)
                    bundle.Messages.Add(new HaveAllMessage());

                else
                    bundle.Messages.Add(new BitfieldMessage(Manager.Bitfield));
            }
            else
            {
                bundle.Messages.Add(new BitfieldMessage(Manager.Bitfield));
            }
        }

        public virtual void Tick(int counter)
        {
            PreLogicTick(counter);
            if (Manager.State == TorrentState.Downloading)
                DownloadLogic(counter);
            else if (Manager.State == TorrentState.Seeding)
                SeedingLogic(counter);
            PostLogicTick(counter);
            
        }

        void PreLogicTick(int counter)
        {
            PeerId id;

            // If any files were changed from DoNotDownload -> Any other priority, then we should hash them if they
            // had been skipped in the original hashcheck.
            _ = TryHashPendingFilesAsync ();

            if (Manager.CanUseLocalPeerDiscovery && (!Manager.LastLocalPeerAnnounceTimer.IsRunning || Manager.LastLocalPeerAnnounceTimer.Elapsed > LocalPeerDiscovery.AnnounceInternal)) {
                _ = Manager.LocalPeerAnnounceAsync ();
            }

            if (Manager.CanUseDht && (!Manager.LastDhtAnnounceTimer.IsRunning || Manager.LastDhtAnnounceTimer.Elapsed > Dht.DhtEngine.AnnounceInternal)) {
                Manager.DhtAnnounce ();
            }

            //Execute iniitial logic for individual peers
            if (counter % (1000 / ClientEngine.TickLength) == 0) {   // Call it every second... ish
                Manager.Monitor.Tick();
                Manager.UpdateLimiters ();
            }

            Manager.PieceManager.Picker.CancelTimedOutRequests();
            Manager.Peers.UpdatePeerCounts();

            if (Manager.finishedPieces.Count > 0)
                SendHaveMessagesToAll();

            for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++)
            {
                id = Manager.Peers.ConnectedPeers[i];
                if (id.Connection == null)
                    continue;

                if (!id.LastPeerExchangeReview.IsRunning || id.LastPeerExchangeReview.Elapsed > TimeSpan.FromMinutes (1)) {
                    id.PeerExchangeManager?.OnTick ();
                    id.LastPeerExchangeReview.Restart ();
                }

                int maxRequests = PieceManager.NormalRequestAmount + (int)(id.Monitor.DownloadSpeed / 1024.0 / PieceManager.BonusRequestPerKb);
                maxRequests = Math.Min(id.AmRequestingPiecesCount + 2, maxRequests);
                maxRequests = Math.Min(id.MaxSupportedPendingRequests, maxRequests);
                maxRequests = Math.Max(2, maxRequests);
                id.MaxPendingRequests = maxRequests;

                id.Monitor.Tick();
            }
        }

        void PostLogicTick(int counter)
        {
            PeerId id;
            var thirtySeconds = TimeSpan.FromSeconds (30);
            var nintySeconds = TimeSpan.FromSeconds (90);
            var onhundredAndEightySeconds = TimeSpan.FromSeconds (180);

            for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++)
            {
                id = Manager.Peers.ConnectedPeers[i];
                if (id.Connection == null)
                    continue;

                if (id.QueueLength > 0 && !id.ProcessingQueue)
                {
                    id.ProcessingQueue = true;
                    ConnectionManager.ProcessQueue(Manager, id);
                }

                if (id.LastMessageSent.Elapsed > nintySeconds)
                {
                    id.LastMessageSent.Restart ();
                    id.Enqueue(new KeepAliveMessage());
                }

                if (id.LastMessageReceived.Elapsed > onhundredAndEightySeconds)
                {
                    ConnectionManager.CleanupSocket(Manager, id);
                    continue;
                }

                if (id.LastMessageReceived.Elapsed > thirtySeconds && id.AmRequestingPiecesCount > 0)
                {
                    ConnectionManager.CleanupSocket(Manager, id);
                    continue;
                }
            }

            Tracker.ITracker tracker = Manager.TrackerManager.CurrentTracker;
            if (tracker != null && (Manager.State == TorrentState.Seeding || Manager.State == TorrentState.Downloading))
            {
                // If the last connection succeeded, then update at the regular interval
                if (Manager.TrackerManager.LastAnnounceSucceeded)
                {
                    if (Manager.TrackerManager.TimeSinceLastAnnounce > tracker.UpdateInterval)
                    {
                        _ = Manager.TrackerManager.Announce(TorrentEvent.None);
                    }
                }
                // Otherwise update at the min interval
                else if (Manager.TrackerManager.TimeSinceLastAnnounce > tracker.MinUpdateInterval)
                {
                    _ = Manager.TrackerManager.Announce(TorrentEvent.None);
                }
            }
        }

        void DownloadLogic(int counter)
        {
            if ((DateTime.Now - Manager.StartTime) > Manager.Settings.WebSeedDelay && Manager.Monitor.DownloadSpeed < Manager.Settings.WebSeedSpeedTrigger)
            {
                foreach (var seedUri in Manager.Torrent.GetRightHttpSeeds)
                {
                    var peerId = HttpConnection.CreatePeerId ();

                    var uri = new Uri(seedUri);
                    var peer = new Peer(peerId, uri);

                    var connection = (HttpConnection) ConnectionFactory.Create (uri);
                    connection.Manager = Manager;

                    var id = new PeerId (peer, connection, Manager.Bitfield.Clone ().SetAll (true));
                    id.BitField.SetAll(true);
                    id.Encryptor = PlainTextEncryption.Instance;
                    id.Decryptor = PlainTextEncryption.Instance;
                    id.IsChoking = false;
                    id.AmInterested = !Manager.Complete;
                    id.ClientApp = new Software(id.PeerID);
                    Manager.Peers.ConnectedPeers.Add(id);
                    Manager.RaisePeerConnected(new PeerConnectedEventArgs (Manager, id));
                    ConnectionManager.ReceiveMessagesAsync (id.Connection, id.Decryptor, Manager.DownloadLimiters, id.Monitor, Manager, id);
                }

                // FIXME: In future, don't clear out this list. It may be useful to keep the list of HTTP seeds
                // Add a boolean or something so that we don't add them twice.
                Manager.Torrent.GetRightHttpSeeds.Clear();
            }

            // Remove inactive peers we haven't heard from if we're downloading
            if (Manager.State == TorrentState.Downloading && Manager.lastCalledInactivePeerManager + TimeSpan.FromSeconds(5) < DateTime.Now)
            {
                Manager.InactivePeerManager.TimePassed();
                Manager.lastCalledInactivePeerManager = DateTime.Now;
            }

            // Now choke/unchoke peers; first instantiate the choke/unchoke manager if we haven't done so already
            if (Manager.chokeUnchoker == null)
                Manager.chokeUnchoker = new ChokeUnchokeManager(Manager, Manager.Settings.MinimumTimeBetweenReviews, Manager.Settings.PercentOfMaxRateToSkipReview);
            Manager.chokeUnchoker.UnchokeReview();
        }

        void SeedingLogic(int counter)
        {
            //Choke/unchoke peers; first instantiate the choke/unchoke manager if we haven't done so already
            if (Manager.chokeUnchoker == null)
                Manager.chokeUnchoker = new ChokeUnchokeManager(Manager, Manager.Settings.MinimumTimeBetweenReviews, Manager.Settings.PercentOfMaxRateToSkipReview);

            Manager.chokeUnchoker.UnchokeReview();
        }

        protected virtual void SetAmInterestedStatus(PeerId id, bool interesting)
        {
            if (interesting && !id.AmInterested)
            {
                id.AmInterested = true;
                id.Enqueue(new InterestedMessage());

                // He's interesting, so attempt to queue up any FastPieces (if that's possible)
                Manager.PieceManager.AddPieceRequests(id);
            }
            else if (!interesting && id.AmInterested)
            {
                id.AmInterested = false;
                id.Enqueue(new NotInterestedMessage());
            }
        }

        internal async Task TryHashPendingFilesAsync ()
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
                foreach (var file in Manager.Torrent.Files) {
                    // If the start piece *and* end piece have been hashed, then every piece in between must've been hashed!
                    if (file.Priority != Priority.DoNotDownload && (Manager.UnhashedPieces[file.StartPieceIndex] || Manager.UnhashedPieces[file.EndPieceIndex])) {
                        for (int index = file.StartPieceIndex; index <= file.EndPieceIndex; index ++) {
                            if (Manager.UnhashedPieces [index]) {
                                var hash = await DiskManager.GetHashAsync(Manager.Torrent, index);
                                Cancellation.Token.ThrowIfCancellationRequested ();

                                var hashPassed = hash != null && Manager.Torrent.Pieces.IsValid(hash, index);
                                Manager.OnPieceHashed (index, hashPassed);

                                if (hashPassed)
                                    Manager.finishedPieces.Enqueue(new HaveMessage (index));
                            }
                        }
                    }
                }
            } finally {
                hashingPendingFiles = false;
            }
        }

        void SendHaveMessagesToAll()
        {
            if (Manager.finishedPieces.Count == 0)
                return;

            if (Settings.AllowHaveSuppression) {
                for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++) {
                    var bundle = new MessageBundle();
                    foreach (var haveMessage in Manager.finishedPieces)  {
                        // If the peer has the piece already, we need to recalculate his "interesting" status.
                        bool hasPiece = Manager.Peers.ConnectedPeers[i].BitField[haveMessage.PieceIndex];
                        if (!hasPiece)
                            bundle.Messages.Add(haveMessage);
                    }

                    Manager.Peers.ConnectedPeers[i].Enqueue(bundle);
                }
            } else {
                var bundle = new MessageBundle (Manager.finishedPieces.Count);
                foreach (var haveMessage in Manager.finishedPieces)
                    bundle.Messages.Add (haveMessage);

                foreach (var peer in Manager.Peers.ConnectedPeers)
                    peer.Enqueue (bundle);
            }

            foreach (var peer in Manager.Peers.ConnectedPeers) {
                bool isInteresting = Manager.PieceManager.IsInteresting(peer);
                SetAmInterestedStatus(peer, isInteresting);
            }
            Manager.finishedPieces.Clear();
        }

        public void Dispose ()
        {
            Cancellation.Cancel ();
        }
    }
}
