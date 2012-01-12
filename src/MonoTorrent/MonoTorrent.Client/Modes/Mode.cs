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
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.BEncoding;
using System.Linq;

namespace MonoTorrent.Client
{
    abstract class Mode
    {
        int webseedCount;
        private TorrentManager manager;

		public abstract TorrentState State
		{
			get;
		}

        protected TorrentManager Manager
        {
            get { return manager; }
        }

        protected Mode(TorrentManager manager)
        {
            CanAcceptConnections = true;
            this.manager = manager;
            manager.chokeUnchoker = new ChokeUnchokeManager(manager, manager.Settings.MinimumTimeBetweenReviews, manager.Settings.PercentOfMaxRateToSkipReview);
        }

        public void HandleMessage(PeerId id, PeerMessage message)
        {
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
        }

        public bool CanAcceptConnections
        {
            get; protected set;
        }

        public bool ShouldConnect(PeerId peer)
        {
            return ShouldConnect(peer.Peer);
        }

		public virtual bool ShouldConnect(Peer peer)
        {
            return true;
        }

		public virtual bool CanHashCheck
		{
			get { return false; }
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
            if (string.IsNullOrEmpty(id.Peer.PeerId))
                id.Peer.PeerId = message.PeerId;

            // If the infohash doesn't match, dump the connection
            if (message.InfoHash != id.TorrentManager.InfoHash)
            {
                Logger.Log(id.Connection, "HandShake.Handle - Invalid infohash");
                throw new TorrentException("Invalid infohash. Not tracking this torrent");
            }

            // If the peer id's don't match, dump the connection. This is due to peers faking usually
            if (id.Peer.PeerId != message.PeerId)
            {
                Logger.Log(id.Connection, "HandShake.Handle - Invalid peerid");
                throw new TorrentException("Supplied PeerID didn't match the one the tracker gave us");
            }

            // Attempt to parse the application that the peer is using
            id.ClientApp = new Software(message.PeerId);
            id.SupportsFastPeer = message.SupportsFastPeer;
            id.SupportsLTMessages = message.SupportsExtendedMessaging;

            // If they support fast peers, create their list of allowed pieces that they can request off me
            if (id.SupportsFastPeer && id.TorrentManager != null && id.TorrentManager.HasMetadata)
                id.AmAllowedFastPieces = AllowedFastAlgorithm.Calculate(id.AddressBytes, id.TorrentManager.InfoHash, (uint)id.TorrentManager.Torrent.Pieces.Count);
        }

        protected virtual void HandlePeerExchangeMessage(PeerId id, PeerExchangeMessage message)
        {
            // Ignore peer exchange messages on private torrents
            if (id.TorrentManager.Torrent.IsPrivate || !id.TorrentManager.Settings.EnablePeerExchange)
                return;

            // If we already have lots of peers, don't process the messages anymore.
            if ((Manager.Peers.Available + Manager.OpenConnections) >= manager.Settings.MaxConnections)
                return;

            IList<Peer> peers = Peer.Decode((BEncodedString)message.Added);
            int count = id.TorrentManager.AddPeersCore(peers);
            id.TorrentManager.RaisePeersFound(new PeerExchangePeersAdded(id.TorrentManager, count, peers.Count, id));
        }

        protected virtual void HandleLtChat(PeerId id, LTChat message)
        {

        }

        protected virtual void HandleLtMetadataMessage(PeerId id, LTMetadata message)
        {
            if (message.MetadataMessageType == LTMetadata.eMessageType.Request)
            {
                if (id.TorrentManager.HasMetadata)
                    id.Enqueue(new LTMetadata(id, LTMetadata.eMessageType.Data, message.Piece, id.TorrentManager.Torrent.Metadata));
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
            id.TorrentManager.PieceManager.Picker.CancelRequest(id, message.PieceIndex, message.StartOffset, message.RequestLength);
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
            SetAmInterestedStatus(id, manager.PieceManager.IsInteresting(id));
        }

        protected virtual void HandleUnchokeMessage(PeerId id, UnchokeMessage message)
        {
            id.IsChoking = false;

            // Add requests to the peers message queue
            manager.PieceManager.AddPieceRequests(id);
        }

        protected virtual void HandleBitfieldMessage(PeerId id, BitfieldMessage message)
        {
            id.BitField = message.BitField;
            id.Peer.IsSeeder = (id.BitField.AllTrue);

            SetAmInterestedStatus(id, manager.PieceManager.IsInteresting(id));
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

            for (int i = 0; i < id.PieceReads.Count; i++)
            {
                if (id.PieceReads[i].PieceIndex == message.PieceIndex && id.PieceReads[i].StartOffset == message.StartOffset && id.PieceReads[i].RequestLength == message.RequestLength)
                {
                    id.IsRequestingPiecesCount--;
                    id.PieceReads.RemoveAt(i);
                    break;
                }
            }
        }

        protected virtual void HandleChokeMessage(PeerId id, ChokeMessage message)
        {
            id.IsChoking = true;
            if (!id.SupportsFastPeer)
                manager.PieceManager.Picker.CancelRequests(id);
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
                if (manager.HasMetadata && !manager.Torrent.IsPrivate)
                    id.PeerExchangeManager = new PeerExchangeManager(id);
            }
        }
        
        protected virtual void HandleKeepAliveMessage(PeerId id, KeepAliveMessage message)
        {
            id.LastMessageReceived = DateTime.Now;
        }

        protected virtual void HandleNotInterested(PeerId id, NotInterestedMessage message)
        {
            id.IsInterested = false;
        }

        protected virtual void HandlePieceMessage(PeerId id, PieceMessage message)
        {
            id.PiecesReceived++;
            manager.PieceManager.PieceDataReceived(id, message);

            // Keep adding new piece requests to this peers queue until we reach the max pieces we're allowed queue
            manager.PieceManager.AddPieceRequests(id);
        }

        protected virtual void HandlePortMessage(PeerId id, PortMessage message)
        {
            id.Port = (ushort)message.Port;
        }

        protected virtual void HandleRequestMessage(PeerId id, RequestMessage message)
        {
            // If we are not on the last piece and the user requested a stupidly big/small amount of data
            // we will close the connection
            if (manager.Torrent.Pieces.Count != (message.PieceIndex + 1))
                if (message.RequestLength > RequestMessage.MaxSize || message.RequestLength < RequestMessage.MinSize)
                    throw new MessageException("Illegal piece request received. Peer requested " + message.RequestLength.ToString() + " byte");

            PieceMessage m = new PieceMessage(message.PieceIndex, message.StartOffset, message.RequestLength);

            // If we're not choking the peer, enqueue the message right away
            if (!id.AmChoking)
            {
                id.IsRequestingPiecesCount++;
                id.PieceReads.Add(m);
                id.TryProcessAsyncReads();
            }

            // If the peer supports fast peer and the requested piece is one of the allowed pieces, enqueue it
            // otherwise send back a reject request message
            else if (id.SupportsFastPeer && ClientEngine.SupportsFastPeer)
            {
                if (id.AmAllowedFastPieces.Contains(message.PieceIndex))
                {
                    id.IsRequestingPiecesCount++;
                    id.PieceReads.Add(m);
                    id.TryProcessAsyncReads();
                }
                else
                    id.Enqueue(new RejectRequestMessage(m));
            }
        }

        protected virtual void HandleHaveMessage(PeerId id, HaveMessage message)
        {
            id.HaveMessagesReceived++;

            // First set the peers bitfield to true for that piece
            id.BitField[message.PieceIndex] = true;

            // Fastcheck to see if a peer is a seeder or not
            id.Peer.IsSeeder = id.BitField.AllTrue;

            // We can do a fast check to see if the peer is interesting or not when we receive a Have Message.
            // If the peer just received a piece we don't have, he's interesting. Otherwise his state is unchanged
            if (!manager.Bitfield[message.PieceIndex])
                SetAmInterestedStatus(id, true);
        }

        public virtual void HandlePeerConnected(PeerId id, Direction direction)
        {
            MessageBundle bundle = new MessageBundle();

            AppendBitfieldMessage(id, bundle);
            AppendExtendedHandshake(id, bundle);
            AppendFastPieces(id, bundle);

            id.Enqueue(bundle);
        }

        public virtual void HandlePeerDisconnected(PeerId id)
        {

        }

        protected virtual void AppendExtendedHandshake(PeerId id, MessageBundle bundle)
        {
            if (id.SupportsLTMessages && ClientEngine.SupportsExtended)
                bundle.Messages.Add(new ExtendedHandshakeMessage(manager.HasMetadata ? manager.Torrent.Metadata.Length : 0));
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
                if (manager.Bitfield.AllFalse)
                    bundle.Messages.Add(new HaveNoneMessage());

                else if (manager.Bitfield.AllTrue)
                    bundle.Messages.Add(new HaveAllMessage());

                else
                    bundle.Messages.Add(new BitfieldMessage(manager.Bitfield));
            }
            else
            {
                bundle.Messages.Add(new BitfieldMessage(manager.Bitfield));
            }
        }

        public virtual void Tick(int counter)
        {
            PreLogicTick(counter);
            if (manager.State == TorrentState.Downloading)
                DownloadLogic(counter);
            else if (manager.State == TorrentState.Seeding)
                SeedingLogic(counter);
            PostLogicTick(counter);
            
        }

        void PreLogicTick(int counter)
        {
            PeerId id;

            //Execute iniitial logic for individual peers
            if (counter % (1000 / ClientEngine.TickLength) == 0) {   // Call it every second... ish
                manager.Monitor.Tick();
                manager.UpdateLimiters ();
            }

            if (manager.finishedPieces.Count > 0)
                SendHaveMessagesToAll();

            for (int i = 0; i < manager.Peers.ConnectedPeers.Count; i++)
            {
                id = manager.Peers.ConnectedPeers[i];
                if (id.Connection == null)
                    continue;

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
            DateTime nowTime = DateTime.Now;
            DateTime thirtySecondsAgo = nowTime.AddSeconds(-50);
            DateTime nintySecondsAgo = nowTime.AddSeconds(-90);
            DateTime onhundredAndEightySecondsAgo = nowTime.AddSeconds(-180);

            for (int i = 0; i < manager.Peers.ConnectedPeers.Count; i++)
            {
                id = manager.Peers.ConnectedPeers[i];
                if (id.Connection == null)
                    continue;

                if (id.QueueLength > 0 && !id.ProcessingQueue)
                {
                    id.ProcessingQueue = true;
                    id.ConnectionManager.ProcessQueue(id);
                }

                if (nintySecondsAgo > id.LastMessageSent)
                {
                    id.LastMessageSent = DateTime.Now;
                    id.Enqueue(new KeepAliveMessage());
                }

                if (onhundredAndEightySecondsAgo > id.LastMessageReceived)
                {
                    manager.Engine.ConnectionManager.CleanupSocket(id, "Inactivity");
                    continue;
                }

                if (thirtySecondsAgo > id.LastMessageReceived && id.AmRequestingPiecesCount > 0)
                {
                    manager.Engine.ConnectionManager.CleanupSocket(id, "Didn't send pieces");
                    continue;
                }
            }

            Tracker.Tracker tracker = manager.TrackerManager.CurrentTracker;
            if (tracker != null && (manager.State == TorrentState.Seeding || manager.State == TorrentState.Downloading))
            {
                // If the last connection succeeded, then update at the regular interval
                if (manager.TrackerManager.UpdateSucceeded)
                {
                    if (DateTime.Now > (manager.TrackerManager.LastUpdated.Add(tracker.UpdateInterval)))
                    {
                        manager.TrackerManager.Announce(TorrentEvent.None);
                    }
                }
                // Otherwise update at the min interval
                else if (DateTime.Now > (manager.TrackerManager.LastUpdated.Add(tracker.MinUpdateInterval)))
                {
                    manager.TrackerManager.Announce(TorrentEvent.None);
                }
            }
        }

		void DownloadLogic(int counter)
		{
			// FIXME: Hardcoded 15kB/sec - is this ok?
			if ((DateTime.Now - manager.StartTime) > TimeSpan.FromMinutes(1) && manager.Monitor.DownloadSpeed < 15 * 1024)
			{
				//if we don't have a webseed, insert it
				if (!manager.Peers.ConnectedPeers.Any(a => a.Peer.PeerId.Contains("-WebSeed-")))
				{
					foreach (string s in manager.Torrent.GetRightHttpSeeds)
					{
						string peerId = "-WebSeed-";
						peerId = peerId + (webseedCount++).ToString().PadLeft(20 - peerId.Length, '0');

						Uri uri = new Uri(s);
						Peer peer = new Peer(peerId, uri);
						PeerId id = new PeerId(peer, manager);
						HttpConnection connection = new HttpConnection(new Uri(s));
						connection.Manager = this.manager;
						peer.IsSeeder = true;
						id.BitField.SetAll(true);
						id.Encryptor = new PlainTextEncryption();
						id.Decryptor = new PlainTextEncryption();
						id.IsChoking = false;
						id.AmInterested = !manager.Complete;
						id.Connection = connection;
						id.ClientApp = new Software(id.PeerID);
						manager.Peers.ConnectedPeers.Add(id);
						manager.RaisePeerConnected(new PeerConnectionEventArgs(manager, id, Direction.Outgoing));
						PeerIO.EnqueueReceiveMessage(id.Connection, id.Decryptor, Manager.DownloadLimiter, id.Monitor, id.TorrentManager, id.ConnectionManager.messageReceivedCallback, id);
					}
				}
			}

			// Remove inactive peers we haven't heard from if we're downloading
			if (manager.State == TorrentState.Downloading && manager.lastCalledInactivePeerManager + TimeSpan.FromSeconds(5) < DateTime.Now)
			{
				manager.InactivePeerManager.TimePassed();
				manager.lastCalledInactivePeerManager = DateTime.Now;
			}

			// Now choke/unchoke peers; first instantiate the choke/unchoke manager if we haven't done so already
			if (manager.chokeUnchoker == null)
				manager.chokeUnchoker = new ChokeUnchokeManager(manager, manager.Settings.MinimumTimeBetweenReviews, manager.Settings.PercentOfMaxRateToSkipReview);
			manager.chokeUnchoker.UnchokeReview();
		}

        void SeedingLogic(int counter)
        {
            //Choke/unchoke peers; first instantiate the choke/unchoke manager if we haven't done so already
            if (manager.chokeUnchoker == null)
                manager.chokeUnchoker = new ChokeUnchokeManager(manager, manager.Settings.MinimumTimeBetweenReviews, manager.Settings.PercentOfMaxRateToSkipReview);

            manager.chokeUnchoker.UnchokeReview();
        }

        protected virtual void SetAmInterestedStatus(PeerId id, bool interesting)
        {
            if (interesting && !id.AmInterested)
            {
                id.AmInterested = true;
                id.Enqueue(new InterestedMessage());

                // He's interesting, so attempt to queue up any FastPieces (if that's possible)
                manager.PieceManager.AddPieceRequests(id);
            }
            else if (!interesting && id.AmInterested)
            {
                id.AmInterested = false;
                id.Enqueue(new NotInterestedMessage());
            }
        }

        void SendHaveMessagesToAll()
        {
            for (int i = 0; i < manager.Peers.ConnectedPeers.Count; i++)
            {
                if (manager.Peers.ConnectedPeers[i].Connection == null)
                    continue;

                MessageBundle bundle = new MessageBundle();

                foreach (int pieceIndex in manager.finishedPieces)
                {
                    // If the peer has the piece already, we need to recalculate his "interesting" status.
                    bool hasPiece = manager.Peers.ConnectedPeers[i].BitField[pieceIndex];
                    if (hasPiece)
                    {
                        bool isInteresting = manager.PieceManager.IsInteresting(manager.Peers.ConnectedPeers[i]);
                        SetAmInterestedStatus(manager.Peers.ConnectedPeers[i], isInteresting);
                    }

                    // Check to see if have supression is enabled and send the have message accordingly
                    if (!hasPiece || (hasPiece && !manager.Engine.Settings.HaveSupressionEnabled))
                        bundle.Messages.Add(new HaveMessage(pieceIndex));
                }

                manager.Peers.ConnectedPeers[i].Enqueue(bundle);
            }
            manager.finishedPieces.Clear();
        }
    }
}
