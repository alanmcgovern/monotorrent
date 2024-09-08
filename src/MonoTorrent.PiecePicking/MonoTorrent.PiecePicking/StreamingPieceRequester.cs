//
// StreamingPieceRequester.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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

namespace MonoTorrent.PiecePicking
{
    public class StreamingPieceRequester : IStreamingPieceRequester
    {
        bool RefreshAfterSeeking = false;

        IMessageEnqueuer? Enqueuer { get; set; }
        IPieceRequesterData? TorrentData { get; set; }

        IReadOnlyList<ReadOnlyBitField>? IgnoringBitfields { get; set; }

        public bool InEndgameMode => false;

        IPiecePicker? LowPriorityPicker { get; set; }

        IPiecePicker? HighPriorityPicker { get; set; }

        /// <summary>
        /// This is the piece index of the block of data currently being consumed by the
        /// media player or other program.
        /// </summary>
        public int HighPriorityPieceIndex { get; private set; }

        /// <summary>
        /// The number of pieces which will be kept buffered to avoid stuttering while streaming media.
        /// </summary>
        internal int HighPriorityCount { get; set; } = 15;

        internal int LowPriorityCount => HighPriorityCount * 2;

        BitField? Temp { get; set; }

        public void Initialise (IPieceRequesterData torrentData, IMessageEnqueuer enqueuer, ReadOnlySpan<ReadOnlyBitField> ignoringBitfields)
        {
            TorrentData = torrentData;
            Enqueuer = enqueuer;
            IgnoringBitfields = ignoringBitfields.ToArray ();
            Temp = new BitField (TorrentData.PieceCount);

            var standardPicker = new StandardPicker ();

            HighPriorityPicker = IgnoringPicker.Wrap (new PriorityPicker (standardPicker), IgnoringBitfields);

            LowPriorityPicker = new RandomisedPicker (standardPicker);
            LowPriorityPicker = new RarestFirstPicker (LowPriorityPicker);
            LowPriorityPicker = new PriorityPicker (LowPriorityPicker);
            LowPriorityPicker = IgnoringPicker.Wrap (LowPriorityPicker, IgnoringBitfields);

            LowPriorityPicker.Initialise (torrentData);
            HighPriorityPicker.Initialise (torrentData);
        }

        ReadOnlyBitField[] otherAvailableCache = Array.Empty<ReadOnlyBitField> ();
        public void AddRequests (ReadOnlySpan<(IRequester Peer, ReadOnlyBitField Available)> peers)
        {
            if (!RefreshAfterSeeking || TorrentData is null || Enqueuer == null)
                return;

            RefreshAfterSeeking = false;
            var allPeers = peers
                .ToArray ()
                .OrderBy (t => -t.Peer.DownloadSpeed)
                .ToArray ();

            // It's only worth cancelling requests for peers which support cancellation. This is part of
            // of the fast peer extensions. For peers which do not support cancellation all we can do is
            // close the connection or allow the existing request queue to drain naturally.
            var start = HighPriorityPieceIndex;
            var end = Math.Min (TorrentData.PieceCount - 1, start + HighPriorityCount - 1);

            var bitfield = GenerateAlreadyHaves ();
            if (bitfield.FirstFalse (start, end) != -1) {
                foreach (var peer in allPeers.Where (p => p.Peer.CanCancelRequests)) {
                    if (HighPriorityPieceIndex > 0)
                        CancelRequests (peer.Peer, 0, HighPriorityPieceIndex - 1);

                    if (HighPriorityPieceIndex + HighPriorityCount < bitfield.Length)
                        CancelRequests (peer.Peer, HighPriorityPieceIndex + HighPriorityCount, bitfield.Length - 1);
                }
            }

            if (otherAvailableCache.Length < peers.Length)
                otherAvailableCache = new ReadOnlyBitField[peers.Length];
            var otherAvailable = otherAvailableCache.AsSpan (0, peers.Length);
            for (int i = 0; i < otherAvailable.Length; i++)
                otherAvailable[i] = peers[i].Available;

            var fastestPeers = allPeers
                .Where (t => t.Peer.DownloadSpeed > 50 * 1024)
                .ToArray ();

            // Queue up 12 pieces for each of our fastest peers. At a download
            // speed of 50kB/sec this should be 3 seconds of transfer for each peer.
            // We queue from peers which support cancellation first as their queue
            // is likely to be empty.
            foreach (var supportsFastPeer in new[] { true, false }) {
                for (int i = 0; i < 4; i++) {
                    foreach (var peer in fastestPeers.Where (p => p.Peer.SupportsFastPeer == supportsFastPeer)) {
                        AddRequests (peer.Peer, peer.Available, otherAvailable, HighPriorityPieceIndex, Math.Min (HighPriorityPieceIndex + 1, bitfield.Length - 1), 2, preferredMaxRequests: (i + 1) * 2);
                    }
                }
            }

            // Then fill up the request queues for all peers
            foreach (var peer in peers)
                AddRequests (peer.Peer, peer.Available, otherAvailable);
        }

        public void AddRequests (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> allPeers)
        {
            if (TorrentData is null)
                return;

            // The first two pieces in the high priority set should be requested multiple times to ensure fast delivery
            var pieceCount = TorrentData.PieceCount;
            for (int i = HighPriorityPieceIndex; i < pieceCount && i <= HighPriorityPieceIndex + 1; i++)
                AddRequests (peer, available, allPeers, HighPriorityPieceIndex, HighPriorityPieceIndex, 2, preferredMaxRequests: 4);

            var lowPriorityEnd = Math.Min (pieceCount - 1, HighPriorityPieceIndex + LowPriorityCount - 1);
            AddRequests (peer, available, allPeers, HighPriorityPieceIndex, lowPriorityEnd, 1, preferredMaxRequests: 3);
            AddRequests (peer, available, allPeers, 0, pieceCount - 1, 1, preferredMaxRequests: 2);
        }

        void AddRequests (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> allPeers, int startPieceIndex, int endPieceIndex, int maxDuplicates, int preferredMaxRequests)
        {
            if (!peer.CanRequestMorePieces || TorrentData == null || Enqueuer == null)
                return;

            int preferredRequestAmount = peer.PreferredRequestAmount (TorrentData.PieceLength);
            int maxTotalRequests = Math.Min (preferredMaxRequests, peer.MaxPendingRequests);
            // FIXME: Add a test to ensure we do not unintentionally request blocks off peers which are choking us.
            // This used to say if (!peer.IsChoing || peer.SupportsFastPeer), and with the recent changes we might
            // not actually guarantee that 'ContinueExistingRequest' or 'ContinueAnyExistingRequest' properly takes
            // into account that a peer which is choking us can *only* resume a 'fast piece' in the 'AmAllowedfastPiece' list.
            if (!peer.IsChoking) {
                while (peer.CanRequestMorePieces && peer.AmRequestingPiecesCount < maxTotalRequests) {
                    if (LowPriorityPicker!.ContinueAnyExistingRequest (peer, available, startPieceIndex, endPieceIndex, maxDuplicates, out PieceSegment request))
                        Enqueuer.EnqueueRequest(peer, request);
                    else
                        break;
                }
            }

            // If the peer supports fast peer and they are choking us, they'll still send pieces in the allowed fast set.
            if (peer.SupportsFastPeer && peer.IsChoking) {
                while (peer.CanRequestMorePieces && peer.AmRequestingPiecesCount < maxTotalRequests) {
                    if (LowPriorityPicker!.ContinueExistingRequest (peer, startPieceIndex, endPieceIndex, out PieceSegment segment))
                        Enqueuer.EnqueueRequest (peer, segment);
                    else
                        break;
                }
            }

            // Should/could we simplify things for IPiecePicker implementations by guaranteeing IPiecePicker.PickPiece calls will
            // only be made to pieces which *can* be requested? Why not!
            // FIXME add a test for this.
            if (!peer.IsChoking || (peer.SupportsFastPeer && peer.IsAllowedFastPieces.Count > 0)) {
                BitField? filtered = null;
                while (peer.CanRequestMorePieces && peer.AmRequestingPiecesCount < maxTotalRequests) {
                    filtered ??= GenerateAlreadyHaves ().Not ().And (available);
                    Span<PieceSegment> buffer = stackalloc PieceSegment[maxTotalRequests - peer.AmRequestingPiecesCount];
                    int requested = PriorityPick (peer, filtered, allPeers, startPieceIndex, endPieceIndex, buffer);
                    if (requested > 0) {
                        Enqueuer.EnqueueRequests (peer, buffer.Slice (0, requested));
                    } else
                        break;
                }
            }
        }

        BitField GenerateAlreadyHaves ()
        {
            Temp!.From (IgnoringBitfields![0]);
            for (int i = 1; i < IgnoringBitfields.Count; i++)
                Temp.Or (IgnoringBitfields[i]);
            return Temp;
        }

        int PriorityPick (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherPeers, int startIndex, int endIndex, Span<PieceSegment> requests)
        {
            int requestCount;

            if (HighPriorityPieceIndex >= startIndex && HighPriorityPieceIndex <= endIndex) {
                var start = HighPriorityPieceIndex;
                var end = Math.Min (endIndex, HighPriorityPieceIndex + HighPriorityCount - 1);

                for (int prioritised = start; prioritised <= start + 1 && prioritised <= end; prioritised++) {
                    if (available[prioritised]) {
                        if ((requestCount = HighPriorityPicker!.PickPiece (peer, available, otherPeers, prioritised, prioritised, requests)) > 0)
                            return requestCount;
                        if (HighPriorityPicker.ContinueAnyExistingRequest (peer, available, prioritised, prioritised, 3, out requests[0]))
                            return 1;
                    }
                }

                if ((requestCount = HighPriorityPicker!.PickPiece (peer, available, otherPeers, start, end, requests)) > 0)
                    return requestCount;
            }

            var lowPriorityEndIndex = Math.Min (HighPriorityPieceIndex + LowPriorityCount, endIndex);
            if ((requestCount = LowPriorityPicker!.PickPiece (peer, available, otherPeers, HighPriorityPieceIndex, lowPriorityEndIndex, requests)) > 0)
                return requestCount;

            // If we're downloading from the 'not important at all' section, queue up at most 2.
            if (peer.AmRequestingPiecesCount < 2)
                return LowPriorityPicker.PickPiece (peer, available, otherPeers, startIndex, endIndex, requests);

            return 0;
        }

        /// <summary>
        /// Cancel any pending requests and then issue new requests so we immediately download pieces from the new high
        /// priority set.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="position"></param>
        public void SeekToPosition (ITorrentManagerFile file, long position)
        {
            // Update the high priority set, then cancel pending requests.
            var oldIndex = HighPriorityPieceIndex;
            ReadToPosition (file, position);
            RefreshAfterSeeking |= oldIndex != HighPriorityPieceIndex;
        }

        /// <summary>
        /// Inform the picker that we have sequentially read data and so will need to update the high priority set without
        /// cancelling pending requests.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="position"></param>
        public void ReadToPosition (ITorrentManagerFile file, long position)
        {
            if (TorrentData != null) {
                if (position >= file.Length)
                    HighPriorityPieceIndex = file.EndPieceIndex;
                else
                    HighPriorityPieceIndex = TorrentData.ByteOffsetToPieceIndex (position + file.OffsetInTorrent);
            }
        }

        public bool ValidatePiece (IRequester peer, PieceSegment blockInfo, out bool pieceComplete, HashSet<IRequester> peersInvolved)
            => HighPriorityPicker!.ValidatePiece (peer, blockInfo, out pieceComplete, peersInvolved);

        public bool IsInteresting (IRequester peer, ReadOnlyBitField bitfield)
            => HighPriorityPicker!.IsInteresting (peer, bitfield);

        PieceSegment[] CancellationsCache = Array.Empty<PieceSegment> ();
        public void CancelRequests (IRequester peer, int startIndex, int endIndex)
        {
            if (HighPriorityPicker is null || Enqueuer is null)
                return;

            if (CancellationsCache.Length < peer.AmRequestingPiecesCount)
                CancellationsCache = new PieceSegment[peer.AmRequestingPiecesCount];

            var cancellations = CancellationsCache.AsSpan (0, peer.AmRequestingPiecesCount);
            var cancelled = HighPriorityPicker.CancelRequests (peer, startIndex, endIndex, cancellations);
            Enqueuer.EnqueueCancellations (peer, cancellations.Slice (0, cancelled));
        }

        public void RequestRejected (IRequester peer, PieceSegment blockInfo)
            => HighPriorityPicker!.RequestRejected (peer, blockInfo);

        public int CurrentRequestCount ()
            => HighPriorityPicker!.CurrentRequestCount ();
    }
}
