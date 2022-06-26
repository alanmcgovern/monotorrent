//
// PieceHashes.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
// Copyright (C) 2022 Alan McGovern
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
using System.Linq;
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
    class PieceHashesMode : Mode
    {
        const int MaxHashesPerRequest = 512;

        class HashesRequesterData : IPieceRequesterData, IMessageEnqueuer
        {
            public BitField ValidatedPieces { get; }
            public IList<ITorrentManagerFile> Files => Array.Empty<ITorrentManagerFile> ();
            public int PieceCount => (totalHashes + PieceLength - 1) / PieceLength;
            public int PieceLength => MaxHashesPerRequest;

            readonly int actualPieceLength;
            readonly MerkleRoot root;
            readonly int totalHashes;

            public HashesRequesterData (MerkleRoot root, int actualPieceLength, int totalHashes)
            {
                this.actualPieceLength = actualPieceLength;
                this.root = root;
                this.totalHashes = totalHashes;
                ValidatedPieces = new BitField (PieceCount);
            }

            public int SegmentsPerPiece (int piece)
                => 1;

            public int ByteOffsetToPieceIndex (long byteOffset)
                => (int) (byteOffset / PieceLength);

            public int BytesPerPiece (int pieceIndex)
                => pieceIndex == PieceCount - 1 ? totalHashes - pieceIndex * PieceLength : PieceLength;

            void IMessageEnqueuer.EnqueueRequest (IPeer peer, PieceSegment block)
            {
                var message = HashRequestMessage.Create (root, totalHashes, actualPieceLength, block.PieceIndex * PieceLength, MaxHashesPerRequest);
                ((PeerId) peer).MessageQueue.Enqueue (message);
            }

            void IMessageEnqueuer.EnqueueRequests (IPeer peer, Span<PieceSegment> blocks)
            {
                foreach (var block in blocks)
                    ((IMessageEnqueuer) this).EnqueueRequest (peer, block);
            }

            void IMessageEnqueuer.EnqueueCancellation (IPeer peer, PieceSegment segment)
            {
                // You can't cancel a request for hashes
            }

            void IMessageEnqueuer.EnqueueCancellations (IPeer peer, Span<PieceSegment> segments)
            {
                // you can't cancel a request for hashes
            }
        }

        static readonly PeerId CompletedSentinal = PeerId.CreateNull (1);
        const int SHA256HashLength = 32;

        static readonly Logger logger = Logger.Create (nameof (PieceHashesMode));
        ValueStopwatch LastAnnounced { get; set; }
        public override TorrentState State => TorrentState.FetchingHashes;
        Dictionary<ITorrentFile, (IPieceRequester, HashesRequesterData)> pickers;
        Dictionary<ITorrentFile, MerkleLayers> infoHashes;
        HashSet<PeerId> IgnoredPeers { get; }
        bool StopWhenDone { get; }

        public PieceHashesMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, bool stopWhenDone)
            : base (manager, diskManager, connectionManager, settings)
        {
            if (manager.Torrent is null)
                throw new InvalidOperationException ($"{nameof (PieceHashesMode)} can only be used after the torrent metadata is available");

            pickers = manager.Torrent!.Files.Where (t => t.StartPieceIndex != t.EndPieceIndex).ToDictionary (t => t, value => {
                var data = new HashesRequesterData (value.PiecesRoot, manager.Torrent.PieceLength, value.EndPieceIndex - value.StartPieceIndex + 1);
                var request = Manager.Engine!.Factories.CreatePieceRequester (new PieceRequesterSettings (false, false, false, ignoreBitFieldAndChokeState: true));
                request.Initialise (data, data, new ReadOnlyBitField[] { data.ValidatedPieces } );
                return (request, data);
            });
            infoHashes = manager.Torrent.Files.Where (t => t.EndPieceIndex != t.StartPieceIndex).ToDictionary (t => t, value => new MerkleLayers (value.PiecesRoot, manager.Torrent.PieceLength, value.EndPieceIndex - value.StartPieceIndex + 1));
            IgnoredPeers = new HashSet<PeerId> ();
            StopWhenDone = stopWhenDone;
        }

        public override void Tick (int counter)
        {
            MaybeAnnounce ();
            MaybeRequestNext ();
        }

        async void MaybeAnnounce ()
        {
            if (!LastAnnounced.IsRunning || LastAnnounced.Elapsed > TimeSpan.FromMinutes (3)) {
                LastAnnounced = ValueStopwatch.StartNew ();
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
        }


        void MaybeRequestNext ()
        {
            foreach (var peer in Manager.Peers.ConnectedPeers) {
                if (IgnoredPeers.Contains (peer))
                    continue;
                foreach (var picker in pickers) {
                    if (!picker.Value.Item2.ValidatedPieces.AllTrue)
                        picker.Value.Item1.AddRequests (peer, peer.BitField, Array.Empty<ReadOnlyBitField> ());
                }
            }
        }

        public override void HandlePeerConnected (PeerId id)
        {
            base.HandlePeerConnected (id);
            MaybeRequestNext ();
        }

        public override void HandlePeerDisconnected (PeerId id)
        {
            base.HandlePeerDisconnected (id);
            foreach (var picker in pickers)
                picker.Value.Item1.CancelRequests (id, 0, picker.Key.EndPieceIndex - picker.Key.StartPieceIndex + 1);
        }

        protected override void HandleHashRejectMessage (PeerId id, HashRejectMessage hashRejectMessage)
        {
            // If someone rejects us, let's remove them from the list for now...
            base.HandleHashRejectMessage (id, hashRejectMessage);
            var file = Manager.Torrent!.Files.FirstOrDefault (f => f.PiecesRoot.Span.SequenceEqual (hashRejectMessage.PiecesRoot.Span));
            if (file == null)
                return;

            var picker = pickers[file];
            if (!picker.Item1.ValidatePiece (id, new PieceSegment (hashRejectMessage.Index / MaxHashesPerRequest, 0), out _, out _))
                return;

            IgnoredPeers.Add (id);
        }

        protected override void HandleHashesMessage (PeerId id, HashesMessage hashesMessage)
        {
            base.HandleHashesMessage (id, hashesMessage);

            var file = Manager.Torrent!.Files.FirstOrDefault (f => f.PiecesRoot.Span.SequenceEqual (hashesMessage.PiecesRoot.Span));
            if (file == null)
                return;

            var picker = pickers[file];
            if (!picker.Item1.ValidatePiece (id, new PieceSegment (hashesMessage.Index / MaxHashesPerRequest, 0), out _, out _)) {
                ConnectionManager.CleanupSocket (Manager, id);
                return;
            }

            // NOTE: Tweak this so we validate the hash in-place, and ensure the data we've been given, provided with the ancestor
            // hashes, combines to form the `PiecesRoot` value.
            var success = infoHashes[file].TryAppend (hashesMessage.BaseLayer, hashesMessage.Index, hashesMessage.Length, hashesMessage.Hashes.Span.Slice (0, hashesMessage.Length * 32), hashesMessage.Hashes.Span.Slice (hashesMessage.Length * 32));
            if (success)
                picker.Item2.ValidatedPieces[hashesMessage.Index / MaxHashesPerRequest] = true;

            if (picker.Item2.ValidatedPieces.AllTrue) {
                using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);

                if (!infoHashes[file].TryVerify (out ReadOnlyMerkleLayers? verifiedPieceHashes))
                    picker.Item2.ValidatedPieces.SetAll (false);
            }

            if (pickers.All (picker => picker.Value.Item2.ValidatedPieces.AllTrue)) {
                Manager.PieceHashes = Manager.Torrent.CreatePieceHashes (infoHashes.ToDictionary (t => t.Key.PiecesRoot, v => (v.Value.TryVerify (out var root) ? root : null)!));
                Manager.PendingV2PieceHashes.SetAll (false);

                // Cancel any duplicate requests
                foreach (var peer in Manager.Peers.ConnectedPeers)
                    foreach (var p in pickers)
                        p.Value.Item1.CancelRequests (peer, 0, p.Key.EndPieceIndex - p.Key.StartPieceIndex + 1);
                if (StopWhenDone)
                    Manager.Mode = new StoppedMode (Manager, DiskManager, ConnectionManager, Settings);
                else
                    Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            }

            MaybeRequestNext ();
        }
    }
}
