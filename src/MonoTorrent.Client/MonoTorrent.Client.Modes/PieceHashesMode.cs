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
            public ReadOnlyBitField AvailablePieces { get; }
            Dictionary<PeerId, IgnoringChokeStateRequester> WrappedPeers { get; }
            Dictionary<IgnoringChokeStateRequester, PeerId> UnwrappedPeers { get; }
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

                AvailablePieces = new BitField (PieceCount).SetAll (true);
                ValidatedPieces = new BitField (PieceCount);
                WrappedPeers = new Dictionary<PeerId, IgnoringChokeStateRequester> ();
                UnwrappedPeers = new Dictionary<IgnoringChokeStateRequester, PeerId> ();
            }

            public IRequester Wrap (PeerId peer)
            {
                if (!WrappedPeers.TryGetValue (peer, out IgnoringChokeStateRequester? wrapper)) {
                    WrappedPeers[peer] = wrapper = new IgnoringChokeStateRequester (peer);
                    UnwrappedPeers[wrapper] = peer;
                }
                return wrapper;
            }

            public int SegmentsPerPiece (int piece)
                => 1;

            public int ByteOffsetToPieceIndex (long byteOffset)
                => (int) (byteOffset / PieceLength);

            public int BytesPerPiece (int pieceIndex)
                => pieceIndex == PieceCount - 1 ? totalHashes - pieceIndex * PieceLength : PieceLength;

            void IMessageEnqueuer.EnqueueRequest (IRequester wrappedPeer, PieceSegment block)
            {
                var peer = UnwrappedPeers[(IgnoringChokeStateRequester) wrappedPeer];
                var message = HashRequestMessage.Create (root, totalHashes, actualPieceLength, block.PieceIndex * PieceLength, MaxHashesPerRequest);
                peer.MessageQueue.Enqueue (message);
            }

            void IMessageEnqueuer.EnqueueRequests (IRequester peer, Span<PieceSegment> blocks)
            {
                foreach (var block in blocks)
                    ((IMessageEnqueuer) this).EnqueueRequest (peer, block);
            }

            void IMessageEnqueuer.EnqueueCancellation (IRequester peer, PieceSegment segment)
            {
                // You can't cancel a request for hashes
            }

            void IMessageEnqueuer.EnqueueCancellations (IRequester peer, Span<PieceSegment> segments)
            {
                // you can't cancel a request for hashes
            }
        }

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
                var request = Manager.Engine!.Factories.CreatePieceRequester (new PieceRequesterSettings (false, false, false, 3));
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
            CloseConnectionsForStalePeers ();
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
                        picker.Value.Item1.AddRequests (picker.Value.Item2.Wrap (peer), picker.Value.Item2.AvailablePieces, Array.Empty<ReadOnlyBitField> ());
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
                picker.Value.Item1.CancelRequests (picker.Value.Item2.Wrap (id), 0, picker.Key.EndPieceIndex - picker.Key.StartPieceIndex + 1);
        }

        protected override void HandleHashRejectMessage (PeerId id, HashRejectMessage hashRejectMessage)
        {
            // If someone rejects us, let's remove them from the list for now...
            base.HandleHashRejectMessage (id, hashRejectMessage);
            var file = Manager.Torrent!.Files.FirstOrDefault (f => f.PiecesRoot.Span.SequenceEqual (hashRejectMessage.PiecesRoot.Span));
            if (file == null)
                return;

            var picker = pickers[file];
            if (!picker.Item1.ValidatePiece (picker.Item2.Wrap (id), new PieceSegment (hashRejectMessage.Index / MaxHashesPerRequest, 0), out _, new HashSet<IRequester> ()))
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
            if (!picker.Item1.ValidatePiece (picker.Item2.Wrap (id), new PieceSegment (hashesMessage.Index / MaxHashesPerRequest, 0), out _, new HashSet<IRequester> ())) {
                ConnectionManager.CleanupSocket (Manager, id);
                return;
            }

            // If the piece validated correctly we should indicate that this peer is healthy and is providing the data
            // we requested
            if (id.AmRequestingPiecesCount == 0) {
                id.LastBlockReceived.Reset ();
            } else {
                id.LastBlockReceived.Restart ();
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
                var actualMerkleLayers = infoHashes.ToDictionary (t => t.Key.PiecesRoot, v => (v.Value.TryVerify (out var root) ? root : null)!);
                Manager.PieceHashes = Manager.Torrent.CreatePieceHashes (actualMerkleLayers);
                Manager.PendingV2PieceHashes.SetAll (false);

                // Cache the data for future re-use
                var path = Settings.GetV2HashesPath (Manager.InfoHashes);
                var data = new BEncodedDictionary ();
                foreach (var kvp in actualMerkleLayers)
                    data[BEncodedString.FromMemory (kvp.Key.AsMemory ())] = BEncodedString.FromMemory (kvp.Value.GetHashes (kvp.Value.PieceLayerIndex));
                Directory.CreateDirectory (Path.GetDirectoryName (path)!);
                File.WriteAllBytes (path, data.Encode ());

                // Cancel any duplicate requests
                foreach (var peer in Manager.Peers.ConnectedPeers)
                    foreach (var p in pickers)
                        p.Value.Item1.CancelRequests (p.Value.Item2.Wrap (peer), 0, p.Key.EndPieceIndex - p.Key.StartPieceIndex + 1);
                if (StopWhenDone)
                    Manager.Mode = new StoppedMode (Manager, DiskManager, ConnectionManager, Settings);
                else
                    Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            }

            MaybeRequestNext ();
        }
    }
}
