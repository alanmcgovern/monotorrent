//
// StandardPieceRequester.cs
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
    public class StandardPieceRequester : IPieceRequester
    {
        IReadOnlyList<ReadOnlyBitField>? IgnorableBitfields { get; set; }
        Memory<PieceSegment> RequestBufferCache { get; set; }
        Memory<ReadOnlyBitField> OtherBitfieldCache { get; set; }
        BitField? Temp { get; set; }

        IMessageEnqueuer? Enqueuer { get; set; }
        IPieceRequesterData? TorrentData { get; set; }

        public bool InEndgameMode { get; private set; }
        IPiecePicker? Picker { get; set; }
        PieceRequesterSettings Settings { get; }

        public StandardPieceRequester (PieceRequesterSettings settings)
            => Settings = settings ?? throw new ArgumentNullException (nameof (settings));

        public void Initialise (IPieceRequesterData torrentData, IMessageEnqueuer enqueuer, ReadOnlySpan<ReadOnlyBitField> ignoringBitfields)
        {
            IgnorableBitfields = ignoringBitfields.ToArray ();
            Enqueuer = enqueuer;
            TorrentData = torrentData;

            Temp = new BitField (TorrentData.PieceCount);

            IPiecePicker picker = new StandardPicker (Settings.IgnoreBitFieldAndChokeState);
            if (Settings.AllowRandomised)
                picker = new RandomisedPicker (picker);
            if (Settings.AllowRarestFirst)
                picker = new RarestFirstPicker (picker);
            if (Settings.AllowPrioritisation)
                picker = new PriorityPicker (picker);

            Picker = picker;
            Picker.Initialise (torrentData);
        }

        ReadOnlyBitField ApplyIgnorables (ReadOnlyBitField primary)
        {
            Temp!.From (primary);
            for (int i = 0; i < IgnorableBitfields!.Count; i++)
                Temp.NAnd (IgnorableBitfields[i]);
            return Temp;
        }

        ReadOnlyBitField[] otherAvailableCache = Array.Empty<ReadOnlyBitField> ();
        public void AddRequests (ReadOnlySpan<(IPeer Peer, ReadOnlyBitField Available)> peers)
        {
            if (otherAvailableCache.Length < peers.Length)
                otherAvailableCache = new ReadOnlyBitField[peers.Length];
            var otherAvailable = otherAvailableCache.AsSpan (0, peers.Length);
            for (int i = 0; i < otherAvailable.Length; i++)
                otherAvailable[i] = peers[i].Available;

            for (int i = 0; i < peers.Length; i++) {
                (var peer, var bitfield) = peers[i];
                if (peer.SuggestedPieces.Count > 0 || (!peer.IsChoking && peer.AmRequestingPiecesCount == 0))
                    AddRequests (peer, bitfield, otherAvailable);
            }
        }

        public void AddRequests (IPeer peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> allPeers)
        {
            int maxRequests = peer.MaxPendingRequests;

            if (!peer.CanRequestMorePieces || Picker == null || TorrentData == null || Enqueuer == null)
                return;

            // This is safe to invoke. 'ContinueExistingRequest' strongly guarantees that a peer will only
            // continue a piece they have initiated. If they're choking then the only piece they can continue
            // will be a fast piece (if one exists!)
            if (Settings.IgnoreBitFieldAndChokeState || !peer.IsChoking || peer.SupportsFastPeer) {
                while (peer.AmRequestingPiecesCount < maxRequests) {
                    PieceSegment? request = Picker.ContinueExistingRequest (peer, 0, available.Length - 1);
                    if (request != null)
                        Enqueuer.EnqueueRequest (peer, request.Value);
                    else
                        break;
                }
            }

            int count = peer.PreferredRequestAmount (TorrentData.PieceLength);
            if (RequestBufferCache.Length < count)
                RequestBufferCache = new Memory<PieceSegment> (new PieceSegment[count]);

            // Reuse the same buffer across multiple requests. However ensure the piecepicker is given
            // a Span<T> of the expected size - so slice the reused buffer if it's too large.
            var requestBuffer = RequestBufferCache.Span.Slice (0, count);
            if (Settings.IgnoreBitFieldAndChokeState || !peer.IsChoking || (peer.SupportsFastPeer && peer.IsAllowedFastPieces.Count > 0)) {
                ReadOnlyBitField filtered = null!;

                while (peer.AmRequestingPiecesCount < maxRequests) {
                    if (Settings.IgnoreBitFieldAndChokeState)
                        filtered ??= ApplyIgnorables (Temp!.SetAll (true));
                    else
                        filtered ??= ApplyIgnorables (available);

                    int requests = Picker.PickPiece (peer, filtered, allPeers, 0, TorrentData.PieceCount - 1, requestBuffer);
                    if (requests > 0)
                        Enqueuer.EnqueueRequests (peer, requestBuffer.Slice (0, requests));
                    else
                        break;
                }
            }

            if ((Settings.IgnoreBitFieldAndChokeState || !peer.IsChoking) && peer.AmRequestingPiecesCount == 0) {
                ReadOnlyBitField filtered = null!;
                while (peer.AmRequestingPiecesCount < maxRequests) {
                    if (Settings.IgnoreBitFieldAndChokeState)
                        filtered ??= ApplyIgnorables (Temp!.SetAll (true));
                    else
                        filtered ??= ApplyIgnorables (available);
                    PieceSegment? request = Picker.ContinueAnyExistingRequest (peer, filtered, 0, TorrentData.PieceCount - 1, 1);
                    // If this peer is a seeder and we are unable to request any new blocks, then we should enter
                    // endgame mode. Every block has been requested at least once at this point.
                    if (request == null && (InEndgameMode || available.AllTrue)) {
                        request = Picker.ContinueAnyExistingRequest (peer, filtered, 0, TorrentData.PieceCount - 1, 2);
                        InEndgameMode |= request != null;
                    }

                    if (request != null)
                        Enqueuer.EnqueueRequest (peer, request.Value);
                    else
                        break;
                }
            }
        }

        public bool ValidatePiece (IPeer peer, PieceSegment blockInfo, out bool pieceComplete, out IList<IPeer> peersInvolved)
        {
            pieceComplete = false;
            peersInvolved = Array.Empty<IPeer> ();
            return Picker != null && Picker.ValidatePiece (peer, blockInfo, out pieceComplete, out peersInvolved);
        }

        public bool IsInteresting (IPeer peer, ReadOnlyBitField bitfield)
            => Picker != null && Picker.IsInteresting (peer, bitfield);

        PieceSegment[] CancellationsCache = Array.Empty<PieceSegment> ();
        public void CancelRequests (IPeer peer, int startIndex, int endIndex)
        {
            if (Picker == null || Enqueuer == null)
                return;

            if (CancellationsCache.Length < peer.AmRequestingPiecesCount)
                CancellationsCache = new PieceSegment[peer.AmRequestingPiecesCount];

            var cancellations = CancellationsCache.AsSpan (0, peer.AmRequestingPiecesCount);
            var cancelled = Picker.CancelRequests (peer, startIndex, endIndex, cancellations);
            Enqueuer.EnqueueCancellations (peer, cancellations.Slice (0, cancelled));
        }

        public void RequestRejected (IPeer peer, PieceSegment pieceRequest)
            => Picker?.RequestRejected (peer, pieceRequest);

        public int CurrentRequestCount ()
            => Picker == null ? 0 : Picker.CurrentRequestCount ();

    }
}
