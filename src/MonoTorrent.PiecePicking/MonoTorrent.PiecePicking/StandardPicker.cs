//
// StandardPicker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;

// using MonoTorrent.Logging;

namespace MonoTorrent.PiecePicking
{
    partial class StandardPicker
    {
        class PickedPieces
        {
            readonly BitField alreadyRequestedBitField;
            readonly Dictionary<int, List<Piece>> duplicates;
            readonly Dictionary<IRequester, Piece> mostRecentRequest;
            readonly Dictionary<int, Piece> requests;
            List<IRequester> tmpList;

            public ReadOnlyBitField AlreadyRequestedBitfield => alreadyRequestedBitField;

            public Dictionary<int, Piece>.ValueCollection Values => requests.Values;

            public PickedPieces (int pieceCount)
            {
                alreadyRequestedBitField = new BitField (pieceCount);
                duplicates = new Dictionary<int, List<Piece>> ();
                mostRecentRequest = new Dictionary<IRequester, Piece> ();
                requests = new Dictionary<int, Piece> ();
                tmpList = new List<IRequester> ();
            }

            internal void Remove (int index)
            {
                alreadyRequestedBitField[index] = false;
                requests.Remove (index);
                duplicates.Remove (index);

                foreach (var v in mostRecentRequest)
                    if (v.Value.Index == index)
                        tmpList.Add (v.Key);

                foreach (var toRemove in tmpList)
                    mostRecentRequest.Remove (toRemove);
                tmpList.Clear ();
            }

            internal bool TryGetValue (int pieceIndex, [MaybeNullWhen (false)] out Piece piece)
                => requests.TryGetValue (pieceIndex, out piece);

            internal bool TryGetDuplicates (int pieceIndex, [MaybeNullWhen (false)] out List<Piece> extraPieces)
                => duplicates.TryGetValue (pieceIndex, out extraPieces);

            internal bool TryGetMostRecentRequest (IRequester peer, [MaybeNullWhen (false)] out Piece mostRecent)
                => mostRecentRequest.TryGetValue (peer, out mostRecent);

            internal void CreateDuplicates (int index, List<Piece> extraPieces)
                => duplicates.Add (index, extraPieces);

            internal void AddRequest (IRequester peer, Piece piece)
            {
                requests.Add (piece.Index, piece);
                alreadyRequestedBitField[piece.Index] = true;
                mostRecentRequest[peer] = piece;
            }

            internal void ClearMostRecentRequest (IRequester peer)
                => mostRecentRequest.Remove (peer);
        }
    }

    public partial class StandardPicker : IPiecePicker
    {
        static ICache<Piece> PieceCache { get; } = new SynchronizedCache<Piece> (() => new Piece ());

        // static readonly Logger logger = Logger.Create (nameof(StandardPicker));

        BitField? CanRequestBitField;
        PickedPieces? Requests { get; set; }
        IPieceRequesterData? TorrentData { get; set; }

        public StandardPicker ()
        {
        }

        public int CancelRequests (IRequester peer, int startIndex, int endIndex, Span<PieceSegment> cancellations)
        {
            if (Requests == null)
                return 0;

            var length = cancellations.Length;
            foreach (var piece in Requests.Values) {
                if (piece.Index < startIndex || piece.Index > endIndex)
                    continue;

                CancelRequests (peer, piece, ref cancellations);
                if (Requests.TryGetDuplicates(piece.Index, out List<Piece>? duplicates)) {
                    foreach (var dupe in duplicates)
                        CancelRequests (peer, dupe, ref cancellations);
                }
            }

            Requests.ClearMostRecentRequest (peer);
            return length - cancellations.Length;
        }

        void CancelRequests (IRequester peer, Piece piece, ref Span<PieceSegment> cancellations)
        {
            foreach (ref Block block in piece.Blocks.AsSpan ()) {
                if (!block.Received && block.RequestedOff == peer) {
                    block.CancelRequest ();
                    piece.Abandoned = true;
                    cancellations[0] = new PieceSegment (piece.Index, block.BlockIndex);
                    cancellations = cancellations.Slice (1);
                }
            }
        }

        public void RequestRejected (IRequester peer, PieceSegment rejectedRequest)
        {
            if (Requests == null)
                return;

            foreach (var piece in Requests.Values) {
                if (piece.Index != rejectedRequest.PieceIndex)
                    continue;

                RequestRejected (peer, piece, rejectedRequest);

                if (Requests.TryGetDuplicates (piece.Index, out List<Piece>? duplicates)) {
                    foreach (var dupe in duplicates)
                        RequestRejected (peer, piece, rejectedRequest);
                }
            }

            Requests.ClearMostRecentRequest (peer);
        }

        void RequestRejected (IRequester peer, Piece piece, PieceSegment rejectedRequest)
        {
            if (piece.Blocks[rejectedRequest.BlockIndex].RequestedOff == peer) {
                piece.Abandoned = true;
                piece.Blocks[rejectedRequest.BlockIndex].CancelRequest ();
            }
        }

        public int CurrentReceivedCount ()
        {
            int count = 0;
            if (Requests != null)
                foreach (var piece in Requests.Values)
                    count += piece.TotalReceived;
            return count;
        }

        public int CurrentRequestCount ()
        {
            int count = 0;
            if (Requests != null)
                foreach (var piece in Requests.Values)
                    count += piece.TotalRequested - piece.TotalReceived;
            return count;
        }

        public IList<ActivePieceRequest> ExportActiveRequests ()
        {
            if (Requests == null)
                return Array.Empty<ActivePieceRequest> ();

            var list = new List<ActivePieceRequest> ();
            foreach (var piece in Requests.Values) {
                foreach (var block in piece.Blocks) {
                    if (block.RequestedOff != null)
                        list.Add (new ActivePieceRequest (block.PieceIndex, block.BlockIndex, block.RequestedOff, block.Received));
                }
            }
            return list;
        }

        public void Initialise (IPieceRequesterData torrentData)
        {
            TorrentData = torrentData;

            CanRequestBitField = new BitField (TorrentData.PieceCount);
            Requests = new PickedPieces (TorrentData.PieceCount);
        }

        public bool IsInteresting (IRequester peer, ReadOnlyBitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public int PickPiece (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherPeers, int startIndex, int endIndex, Span<PieceSegment> requests)
        {
            // If there is already a request on this peer, try to request the next block. If the peer is choking us, then the only
            // requests that could be continued would be existing "Fast" pieces.
            if (ContinueExistingRequest (peer, available, startIndex, endIndex, 1, false, false, out requests[0]))
                return 1;

            // Then we check if there are any allowed "Fast" pieces to download
            if (peer.IsChoking && GetFromList (peer, available, peer.IsAllowedFastPieces, out requests[0]))
                return 1;

            // If the peer is choking, then we can't download from them as they had no "fast" pieces for us to download
            if (peer.IsChoking)
                return 0;

            // Only try to continue an abandoned piece if this peer has not recently been involved in downloading data which
            // failed it's hash check.
            if (peer.RepeatedHashFails == 0 && ContinueExistingRequest (peer, available, startIndex, endIndex, 1, true, false, out requests[0]))
                return 1;

            // We see if the peer has suggested any pieces we should request
            if (GetFromList (peer, available, peer.SuggestedPieces, out requests[0]))
                return 1;

            // Now we see what pieces the peer has that we don't have and try and request one
            return GetStandardRequest (peer, available, startIndex, endIndex, requests);
        }

        public void Reset ()
        {
            if (TorrentData != null)
                Requests = new PickedPieces (TorrentData.PieceCount);
        }

        static readonly Func<Piece, int, int> IndexComparer = (Piece piece, int comparand)
            => piece.Index.CompareTo (comparand);

        public bool ValidatePiece (IRequester peer, PieceSegment request, out bool pieceComplete, HashSet<IRequester> peersInvolved)
        {
            if (Requests == null || !Requests.TryGetValue (request.PieceIndex, out Piece? primaryPiece)) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} No piece.", request.PieceIndex, request.StartOffset, peer);
                pieceComplete = false;
                return false;
            }

            var result = ValidateRequestWithPiece (peer, request, primaryPiece, out pieceComplete, peersInvolved);

            // If there are no duplicate requests, exit early! Otherwise we'll need to do book keeping to
            // ensure our received piece is not re-requested again.
            if (!Requests.TryGetDuplicates (request.PieceIndex, out List<Piece>? extraPieces)) {
                if (pieceComplete) {
                    Requests.Remove (request.PieceIndex);
                    PieceCache.Enqueue (primaryPiece);
                }
                return result;
            }

            // We have duplicate requests and have, so far, failed to validate the block. Try to validate it now.
            if (!result) {
                for (int i = 0; i < extraPieces.Count && !result; i++)
                    if ((result = ValidateRequestWithPiece (peer, request, extraPieces[i], out pieceComplete, peersInvolved)))
                        break;
            }


            // If we successfully validated the block using *any* version of the request, update the primary piece and
            // all duplicates to reflect this. This will implicitly cancel any outstanding requests from other peers.
            if (result) {
                primaryPiece.Blocks[request.BlockIndex].TrySetReceived (peer);
                for (int i = 0; i < extraPieces.Count; i++)
                    extraPieces[i].Blocks[request.BlockIndex].TrySetReceived (peer);
            }

            // If the piece is complete then remove it, and any dupes, from the picker.
            if (pieceComplete) {
                Requests.Remove (request.PieceIndex);
                PieceCache.Enqueue (primaryPiece);
                return result;
            }
            return result;
        }

        bool ValidateRequestWithPiece (IRequester peer, PieceSegment request, Piece piece, out bool pieceComplete, HashSet<IRequester> peersInvolved)
        {
            pieceComplete = false;

            // Pick out the block that this piece message belongs to
            int blockIndex = request.BlockIndex;
            ref Block block = ref piece.Blocks[blockIndex];
            if (blockIndex == -1 || !peer.Equals (block.RequestedOff)) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} No block ", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            if (block.Received) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} Already received.", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            if (!block.Requested) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} Not requested.", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            peer.AmRequestingPiecesCount--;
            block.Received = true;

            if (piece.AllBlocksReceived) {
                pieceComplete = true;
                piece.CalculatePeersInvolved (peersInvolved);
            }
            return true;
        }

        public bool ContinueExistingRequest (IRequester peer, int startIndex, int endIndex, out PieceSegment segment)
            => ContinueExistingRequest (peer, null, startIndex, endIndex, 1, false, false, out segment);

        bool ContinueExistingRequest (IRequester peer, ReadOnlyBitField? availablePieces, int startIndex, int endIndex, int maxDuplicateRequests, bool allowAbandoned, bool allowAny, out PieceSegment segment)
        {
            segment = PieceSegment.Invalid;
            if (Requests is null || TorrentData is null)
                return false;

            if (Requests.TryGetMostRecentRequest (peer, out Piece? mostRecent)) {
                foreach (ref Block block in mostRecent.Blocks.AsSpan ())
                    if (!block.Requested && !block.Received) {
                        segment = block.CreateRequest (peer);
                        return true;
                    }

                if (maxDuplicateRequests == 1 && !allowAbandoned && !allowAny)
                    return false;
            }

            if (availablePieces == null)
                return false;

            foreach (var p in Requests.Values) {
                int index = p.Index;
                if (p.AllBlocksRequested || index < startIndex || index > endIndex || !availablePieces[index])
                    continue;

                // For each piece that was assigned to this peer, try to request a block from it
                // A piece is 'assigned' to a peer if he is the first person to request a block from that piece
                if (allowAny || (allowAbandoned && p.Abandoned) || peer == p.Blocks[0].RequestedOff) {
                    foreach (ref Block block in p.Blocks.AsSpan ()) {
                        if (!block.Requested && !block.Received) {
                            segment = block.CreateRequest (peer);
                            return true;
                        }
                    }
                }
            }

            // If all blocks have been requested at least once and we're allowed more than 1 request then
            // let's try and issue a duplicate!
            for (int duplicate = 1; duplicate < maxDuplicateRequests; duplicate++) {
                foreach (var primaryPiece in Requests.Values) {
                    if (primaryPiece.Index < startIndex || primaryPiece.Index > endIndex || !availablePieces[primaryPiece.Index])
                        continue;

                    if (!Requests.TryGetDuplicates (primaryPiece.Index, out List<Piece>? extraPieces)) {
                        extraPieces = new List<Piece> ();
                        Requests.CreateDuplicates (primaryPiece.Index, extraPieces);
                    }

                    if (extraPieces.Count < duplicate) {
                        var newPiece = PieceCache.Dequeue ().Initialise (primaryPiece.Index, TorrentData.SegmentsPerPiece (primaryPiece.Index));
                        for (int i = 0; i < primaryPiece.BlockCount; i++)
                            if (primaryPiece.Blocks[i].Received)
                                newPiece.Blocks[i].TrySetReceived (primaryPiece.Blocks[i].RequestedOff!);
                        extraPieces.Add (newPiece);
                    }

                    for (int extraPieceIndex = 0; extraPieceIndex < extraPieces.Count; extraPieceIndex++) {
                        var extraPiece = extraPieces[extraPieceIndex];
                        for (int i = 0; i < extraPiece.BlockCount; i++) {
                            if (!extraPiece.Blocks[i].Requested && !HasAlreadyRequestedBlock (primaryPiece, extraPieces, peer, i)) {
                                segment = extraPiece.Blocks[i].CreateRequest (peer);
                                return true;
                            }
                        }
                    }
                }
            }

            // If we get here it means all the blocks in the pieces being downloaded by the peer are already requested
            return false;
        }

        static bool HasAlreadyRequestedBlock (Piece piece, IList<Piece> extraPieces, IRequester peer, int blockIndex)
        {
            if (piece.Blocks[blockIndex].RequestedOff == peer)
                return true;
            for (int i = 0; i < extraPieces.Count; i++)
                if (extraPieces[i].Blocks[blockIndex].RequestedOff == peer)
                    return true;
            return false;
        }

        public bool ContinueAnyExistingRequest (IRequester peer, ReadOnlyBitField available, int startIndex, int endIndex, int maxDuplicateRequests, out PieceSegment segment)
        {
            // If this peer is currently a 'dodgy' peer, then don't allow him to help with someone else's
            // piece request.
            if (peer.RepeatedHashFails != 0) {
                segment = PieceSegment.Invalid;
                return false;
            }

            return ContinueExistingRequest (peer, available, startIndex, endIndex, maxDuplicateRequests, true, true, out segment);
        }

        bool GetFromList (IRequester peer, ReadOnlyBitField bitfield, IList<int> pieces, out PieceSegment segment)
        {
            segment = PieceSegment.Invalid;
            if (!peer.SupportsFastPeer || Requests is null || TorrentData is null)
                return false;

            for (int i = 0; i < pieces.Count; i++) {
                int index = pieces[i];
                if (index >= bitfield.Length || !bitfield[index] || Requests.AlreadyRequestedBitfield [index])
                    continue;

                pieces.RemoveAt (i);
                var p = PieceCache.Dequeue ().Initialise (index, TorrentData.SegmentsPerPiece (index));
                Requests.AddRequest (peer, p);
                segment = p.Blocks[0].CreateRequest (peer);
                return true;
            }


            return false;
        }

        int GetStandardRequest (IRequester peer, ReadOnlyBitField current, int startIndex, int endIndex, Span<PieceSegment> requests)
        {
            if (TorrentData == null || Requests == null)
                return 0;

            int piecesNeeded = (requests.Length * Constants.BlockSize) / TorrentData.PieceLength;
            if ((requests.Length * Constants.BlockSize) % TorrentData.PieceLength != 0)
                piecesNeeded++;
            int checkIndex = CanRequest (current, startIndex, endIndex, ref piecesNeeded);

            // Nothing to request.
            if (checkIndex == -1)
                return 0;

            var totalRequested = 0;
            for (int i = 0; totalRequested < requests.Length && i < piecesNeeded; i++) {
                // Request the piece
                var p = PieceCache.Dequeue ().Initialise (checkIndex + i, TorrentData.SegmentsPerPiece (checkIndex + i));
                Requests.AddRequest (peer, p);
                for (int j = 0; j < p.Blocks.Length && totalRequested < requests.Length; j++)
                    requests[totalRequested++] = p.Blocks[j].CreateRequest (peer);
            }
            return totalRequested;
        }

        int CanRequest (ReadOnlyBitField bitfield, int pieceStartIndex, int pieceEndIndex, ref int pieceCount)
        {
            if (CanRequestBitField == null || Requests == null)
                return 0;

            // This is the easiest case to consider - special case it
            if (pieceCount == 1) {
                return CanRequestBitField.From (bitfield).NAnd (Requests.AlreadyRequestedBitfield).FirstTrue (pieceStartIndex, pieceEndIndex);
            }

            int largestStart = 0;
            int largestEnd = 0;
            while (pieceStartIndex <= pieceEndIndex && (pieceStartIndex = bitfield.FirstTrue (pieceStartIndex, pieceEndIndex)) != -1) {
                int end = bitfield.FirstFalse (pieceStartIndex, pieceEndIndex);
                if (end == -1)
                    end = Math.Min (pieceStartIndex + pieceCount, bitfield.Length);

                // Do not include 'end' as it's the first *false* piece.
                for (int i = pieceStartIndex; i < end; i++)
                    if (Requests.AlreadyRequestedBitfield[i])
                        end = i;

                if ((end - pieceStartIndex) >= pieceCount)
                    return pieceStartIndex;

                if ((largestEnd - largestStart) < (end - pieceStartIndex)) {
                    largestStart = pieceStartIndex;
                    largestEnd = end;
                }

                pieceStartIndex = Math.Max (pieceStartIndex + 1, end);
            }

            pieceCount = largestEnd - largestStart;
            return pieceCount == 0 ? -1 : largestStart;
        }
    }
}
