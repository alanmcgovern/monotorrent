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
        static readonly PeerId CompletedSentinal = PeerId.CreateNull (1);
        const int MaxHashesPerRequest = 512;
        const int SHA256HashLength = 32;

        static readonly Logger logger = Logger.Create (nameof (PieceHashesMode));
        ValueStopwatch LastAnnounced { get; set; }
        public override TorrentState State => TorrentState.FetchingHashes;
        Dictionary<ITorrentFile, PeerId?[]> pickers;
        Dictionary<ITorrentFile, Memory<byte>> infoHashes;

        public PieceHashesMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings)
            : base (manager, diskManager, connectionManager, settings)
        {
            if (manager.Torrent is null)
                throw new InvalidOperationException ($"{nameof (PieceHashesMode)} can only be used after the torrent metadata is available");

            pickers = manager.Torrent!.Files.ToDictionary (t => t, value => new PeerId?[(value.EndPieceIndex - value.StartPieceIndex + MaxHashesPerRequest) / MaxHashesPerRequest]);
            infoHashes = manager.Torrent.Files.ToDictionary (t => t, value => new Memory<byte> (new byte[(value.EndPieceIndex - value.StartPieceIndex + 1) * SHA256HashLength]));

            // Files with 1 piece do not have additional hashes to fetch. The PiecesRoot *is* the SHA256 of the entire file.
            foreach (var value in pickers)
                if (value.Key.EndPieceIndex == value.Key.StartPieceIndex)
                    value.Value[0] = CompletedSentinal;
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

        List<PeerId> Peers = new List<PeerId> ();

        public override void HandlePeerConnected (PeerId id)
        {
            base.HandlePeerConnected (id);
            Peers.Add (id);
        }

        public override void HandlePeerDisconnected (PeerId id)
        {
            base.HandlePeerDisconnected (id);
            Peers.Remove (id);
        }

        void MaybeRequestNext ()
        {
            if (Peers.Count == 0)
                return;

            foreach (var picker in pickers) {
                for (int i = 0; i < picker.Value.Length; i++) {
                    // Successfully downloaded!
                    if (picker.Value[i] == CompletedSentinal)
                        continue;

                    if (picker.Value[i] == null) {
                        picker.Value[i] = RequestChunk (picker.Key, i * MaxHashesPerRequest);
                        return;
                    } else if (!picker.Value[i]!.IsConnected) {
                        // We'll request this on the next tick.
                        picker.Value[i] = null; 
                    }
                }
            }
        }

        protected override void HandleHashRejectMessage (PeerId id, HashRejectMessage hashRejectMessage)
        {
            // If someone rejects us, let's remove them from the list for now...
            base.HandleHashRejectMessage (id, hashRejectMessage);
            Peers.Remove (id);
            RemoveRequest (id, hashRejectMessage.PiecesRoot, hashRejectMessage.Index / MaxHashesPerRequest);
        }

        protected override void HandleHashesMessage (PeerId id, HashesMessage hashesMessage)
        {
            base.HandleHashesMessage (id, hashesMessage);
            RemoveRequest (id, hashesMessage.PiecesRoot, hashesMessage.Index / MaxHashesPerRequest);

            var file = Manager.Torrent!.Files.FirstOrDefault (f => f.PiecesRoot.Span.SequenceEqual (hashesMessage.PiecesRoot.Span));
            if (file == null)
                return;

            // NOTE: Tweak this so we validate the hash in-place, and ensure the data we've been given, provided with the ancestor
            // hashes, combines to form the `PiecesRoot` value.
            var memory = infoHashes[file];
            for (int i = 0; i < hashesMessage.Length; i++)
                hashesMessage.Hashes[i].CopyTo (memory.Slice ((hashesMessage.Index + i) * 32, 32));
            MarkDone (hashesMessage.PiecesRoot, hashesMessage.Index / MaxHashesPerRequest);

            if (pickers[file].All (t => t == CompletedSentinal)) {
                using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);
                Span<byte> resultBuffer = stackalloc byte[32];
                MerkleHash.TryHash (hasher, infoHashes[file], Manager.Torrent.PieceLength, resultBuffer, out int written);
                if (!resultBuffer.SequenceEqual (hashesMessage.PiecesRoot.Span))
                    pickers[file].AsSpan ().Clear ();
            }

            if (pickers.All (picker => picker.Value.All (t => t == CompletedSentinal))) {
                Manager.PieceHashes = Manager.Torrent.CreatePieceHashes (infoHashes.ToDictionary (t => BEncodedString.FromMemory (t.Key.PiecesRoot), v => BEncodedString.FromMemory (v.Value)));
                Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            }
        }

        void MarkDone (ReadOnlyMemory<byte> piecesRoot, int requestOffset)
        {
            var file = Manager.Torrent!.Files.FirstOrDefault (t => t.PiecesRoot.Span.SequenceEqual (piecesRoot.Span));
            if (file == null)
                return;

            var picker = pickers[file];
            picker[requestOffset] = CompletedSentinal;
        }

        void RemoveRequest (PeerId id, ReadOnlyMemory<byte> piecesRoot, int requestOffset)
        {
            var file = Manager.Torrent!.Files.FirstOrDefault (t => t.PiecesRoot.Span.SequenceEqual (piecesRoot.Span));
            if (file == null)
                return;

            var picker = pickers[file];
            if (picker[requestOffset] != id) {
                id.Connection.Dispose ();
                throw new InvalidOperationException ("Something bad happened and the peer rejected a request we did not send");
            }
            picker[requestOffset] = null;
        }

        PeerId RequestChunk (ITorrentFile file, int hashOffset)
        {
            int totalHashes = file.EndPieceIndex - file.StartPieceIndex + 1;
            var hashesRequested = Math.Min (MaxHashesPerRequest, totalHashes - hashOffset);

            var preferredPeer = Peers[0];
            Peers.RemoveAt (0);
            Peers.Add (preferredPeer);

            var leafLayer = (int) Math.Log (Constants.BlockSize, 2);
            var pieceLayer = (int) Math.Log (Manager.Torrent!.PieceLength, 2) - leafLayer;
            var proofLayers = (int) Math.Ceiling (Math.Log (file.Length / (double)Manager.Torrent.PieceLength, 2)) - 1;
            preferredPeer.MessageQueue.Enqueue (new HashRequestMessage (file.PiecesRoot, pieceLayer, hashOffset, hashesRequested, proofLayers));
            return preferredPeer;
        }
    }
}
