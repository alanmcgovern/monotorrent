//
// MetadataMode.cs
//
// Authors:
//   Olivier Dufour olivier.duff@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
// Copyright (C) 2009 Olivier Dufour
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
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Logging;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;
using MonoTorrent.Messages.Peer.Libtorrent;
using MonoTorrent.PiecePicking;

namespace MonoTorrent.Client.Modes
{
    class MetadataMode : Mode
    {
        static readonly Logger logger = Logger.Create (nameof (MetadataMode));

        class MetadataData : IPieceRequesterData, IMessageEnqueuer
        {
            public IList<ITorrentManagerFile> Files => Array.Empty<ITorrentManagerFile> ();
            public int PieceCount => 1;
            public int PieceLength { get; }

            int length;

            public MetadataData (int size)
            {
                length = size;
                PieceLength = (int) Math.Pow (2, Math.Ceiling (Math.Log (size, 2)) + 1);
            }

            public int BytesPerBlock(int pieceIndex, int blockIndex)
                => Math.Min (Constants.BlockSize, BytesPerPiece (pieceIndex) - blockIndex * Constants.BlockSize);

            public int SegmentsPerPiece (int piece)
                => (length + Constants.BlockSize - 1) / Constants.BlockSize;

            public int ByteOffsetToPieceIndex (long byteOffset)
                => 0;

            public int BytesPerPiece (int piece)
                => length;

            void IMessageEnqueuer.EnqueueRequest (IPeer peer, PieceSegment block)
            {
                var message = new LTMetadata (((PeerId) peer).ExtensionSupports, LTMetadata.MessageType.Request, block.BlockIndex);
                ((PeerId) peer).MessageQueue.Enqueue (message);
            }

            void IMessageEnqueuer.EnqueueRequests (IPeer peer, Span<PieceSegment> blocks)
            {
                foreach (var block in blocks)
                    ((IMessageEnqueuer)this).EnqueueRequest (peer, block);
            }

            void IMessageEnqueuer.EnqueueCancellation (IPeer peer, PieceSegment segment)
            {
               // you can't cancel a request for metadata 
            }

            void IMessageEnqueuer.EnqueueCancellations (IPeer peer, Span<PieceSegment> segments)
            {
                // you can't cancel a request for metadata
            }
        }

        static readonly TimeSpan timeout = TimeSpan.FromSeconds (10);
        string savePath;
        bool stopWhenDone;

        bool HasAnnounced { get; set; }
        MetadataData? RequesterData { get; set; }
        IPieceRequester? Requester { get; set; }
        byte[]? Stream { get; set; }

        public override bool CanHashCheck => true;
        public override TorrentState State => TorrentState.Metadata;

        public MetadataMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, string savePath)
            : this (manager, diskManager, connectionManager, settings, savePath, false)
        {

        }

        public MetadataMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, string savePath, bool stopWhenDone)
            : base (manager, diskManager, connectionManager, settings)
        {
            this.savePath = savePath;
            this.stopWhenDone = stopWhenDone;
        }

        public override void HandlePeerDisconnected (PeerId id)
        {
            base.HandlePeerDisconnected (id);
            if (Requester != null && RequesterData != null)
                Requester.CancelRequests (id, 0, RequesterData.PieceCount);
        }

        public override void Tick (int counter)
        {
            if (!HasAnnounced) {
                HasAnnounced = true;
                SendAnnounces ();
            }

            foreach (PeerId id in Manager.Peers.ConnectedPeers)
                if (id.SupportsLTMessages && id.ExtensionSupports.Supports (LTMetadata.Support.Name))
                    RequestNextNeededPiece (id);
        }

        async void SendAnnounces ()
        {
            try {
                Manager.DhtAnnounce ();
                await Task.WhenAll (
                    Manager.TrackerManager.AnnounceAsync (CancellationToken.None).AsTask (),
                    Manager.LocalPeerAnnounceAsync ()
                );
            } catch {
                // Nothing.
            }
        }

        protected override void HandleLtMetadataMessage (PeerId id, LTMetadata message)
        {
            base.HandleLtMetadataMessage (id, message);

            if (Requester is null || RequesterData is null || Stream is null)
                return;

            switch (message.MetadataMessageType) {
                case LTMetadata.MessageType.Data:
                    if (!Requester.ValidatePiece (id, new PieceSegment (0, message.Piece), out bool pieceComplete, out IList<IPeer> peersInvolved))
                        return;

                    message.MetadataPiece.CopyTo (Stream.AsMemory (message.Piece * LTMetadata.BlockSize));
                    if (pieceComplete) {
                        InfoHash? v1InfoHash;
                        InfoHash? v2InfoHash;

                        using (SHA1 hasher = SHA1.Create ())
                            v1InfoHash = InfoHash.FromMemory (hasher.ComputeHash (Stream));

                        using (SHA256 hasher = SHA256.Create ())
                            v2InfoHash = InfoHash.FromMemory (hasher.ComputeHash (Stream));

                        if (!Manager.InfoHashes.Contains (v1InfoHash) && !Manager.InfoHashes.Contains (v2InfoHash)) {
                            // Do nothing. As the piece has been marked as 'complete' by the picker, the internal picker state has dropped all references to the piece.
                            // We'll automatically retry downloading all pieces now.
                        } else {
                            BEncodedDictionary dict = new BEncodedDictionary ();
                            dict.Add ("info", BEncodedValue.Decode (Stream));

                            if (Manager.TrackerManager.Tiers != null && Manager.TrackerManager.Tiers.Count > 0) {
                                BEncodedList announceTrackers = new BEncodedList ();
                                foreach (var tier in Manager.TrackerManager.Tiers) {
                                    BEncodedList announceUrls = new BEncodedList ();

                                    foreach (var tracker in tier.Trackers) {
                                        announceUrls.Add (new BEncodedString (tracker.Uri.OriginalString));
                                    }

                                    announceTrackers.Add (announceUrls);
                                }

                                dict.Add ("announce-list", announceTrackers);
                            }
                            var rawData = dict.Encode ();
                            if (Torrent.TryLoad (rawData, out Torrent? t)) {
                                Requester = null;
                                RequesterData = null;

                                if (stopWhenDone) {
                                    Manager.SetMetadata (t);
                                    Manager.RaiseMetadataReceived (rawData);
                                    return;
                                }

                                try {
                                    if (this.Settings.AutoSaveLoadMagnetLinkMetadata) {
                                        if (Path.GetDirectoryName (savePath) is string parentDir && !Directory.Exists (parentDir))
                                            Directory.CreateDirectory (parentDir);
                                        File.Delete (savePath);
                                        File.WriteAllBytes (savePath, dict.Encode ());
                                    }
                                } catch (Exception ex) {
                                    logger.ExceptionFormated (ex, "Cannot write metadata to path '{0}'", savePath);
                                    Manager.TrySetError (Reason.WriteFailure, ex);
                                    return;
                                }
                                Manager.SetMetadata (t);
                                _ = Manager.StartAsync ();
                                Manager.RaiseMetadataReceived (rawData);
                            } else {
                                // Do nothing. As the piece has been marked as 'complete' by the picker, the internal picker state has dropped all references to the piece.
                                // We'll automatically retry downloading all pieces now.
                            }
                        }
                    }
                    RequestNextNeededPiece (id);
                    break;
                case LTMetadata.MessageType.Reject:
                    //TODO
                    //Think to what we do in this situation
                    //for moment nothing ;)
                    //reject or flood?
                    break;
                case LTMetadata.MessageType.Request://ever done in base class but needed to avoid default
                    break;
                default:
                    throw new MessageException ($"Invalid messagetype in LTMetadata: {message.MetadataMessageType}");
            }

        }

        protected override void HandleAllowedFastMessage (PeerId id, AllowedFastMessage message)
        {
            // Disregard these when in metadata mode as we can't request regular pieces anyway
        }

        protected override void HandleHaveAllMessage (PeerId id, HaveAllMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveMessage (PeerId id, HaveMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveNoneMessage (PeerId id, HaveNoneMessage message)
        {
            // Nothing
        }

        protected override void HandleInterestedMessage (PeerId id, InterestedMessage message)
        {
            // Nothing
        }

        void RequestNextNeededPiece (PeerId id)
        {
            if (Requester is null )
                return;

            Requester.AddRequests (id, id.BitField, Array.Empty<ReadOnlyBitField> ());
        }

        protected override void AppendBitfieldMessage (PeerId id, MessageBundle bundle)
        {
            if (id.SupportsFastPeer)
                bundle.Add (HaveNoneMessage.Instance, default);
            // If the fast peer extensions are not supported we must not send a
            // bitfield message because we don't know how many pieces the torrent
            // has. We could probably send an invalid one and force the connection
            // to close.
        }

        protected override void HandleBitfieldMessage (PeerId id, BitfieldMessage message)
        {
            // If we receive a bitfield message we should ignore it. We don't know how many
            // pieces the torrent has so we can't actually safely decode the bitfield.
            if (message != BitfieldMessage.UnknownLength)
                throw new InvalidOperationException ("BitfieldMessages should not be decoded normally while in metadata mode.");
        }

        protected override void HandleExtendedHandshakeMessage (PeerId id, ExtendedHandshakeMessage message)
        {
            base.HandleExtendedHandshakeMessage (id, message);

            if (id.ExtensionSupports.Supports (LTMetadata.Support.Name)) {
                var metadataSize = message.MetadataSize.GetValueOrDefault (0);
                if (Stream == null && metadataSize > 0) {
                    Stream = new byte[metadataSize];
                    Requester = Manager.Engine!.Factories.CreatePieceRequester (new PieceRequesterSettings (false, false, false, ignoreBitFieldAndChokeState: true));
                    RequesterData = new MetadataData (metadataSize);
                    Requester.Initialise (RequesterData, RequesterData, Array.Empty<ReadOnlyBitField> ());
                }

                // We only create the Stream if the remote peer has sent the metadata size key in their handshake.
                // There's no guarantee the remote peer has the metadata yet, so even though they support metadata
                // mode they might not be able to share the data.
                RequestNextNeededPiece (id);
            }
        }

        protected override void SetAmInterestedStatus (PeerId id, bool interesting)
        {
            // Never set a peer as interesting when in metadata mode
            // we don't want to try download any data
        }
    }
}
