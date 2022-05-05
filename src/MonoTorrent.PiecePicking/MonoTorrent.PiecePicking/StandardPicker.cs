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
            readonly Dictionary<IPeer, Piece> mostRecentRequest;
            readonly Dictionary<int, Piece> requests;

            public ReadOnlyBitField AlreadyRequestedBitfield => alreadyRequestedBitField;

            public Dictionary<int, Piece>.ValueCollection Values => requests.Values;

            public PickedPieces (int pieceCount)
            {
                alreadyRequestedBitField = new BitField (pieceCount);
                duplicates = new Dictionary<int, List<Piece>> ();
                mostRecentRequest = new Dictionary<IPeer, Piece> ();
                requests = new Dictionary<int, Piece> ();
            }

            internal void Remove (int index)
            {
                requests.Remove (index);
                alreadyRequestedBitField[index] = false;
                foreach (var entry in mostRecentRequest) {
                    if (entry.Value.Index == index) {
                        mostRecentRequest.Remove (entry.Key);
                        break;
                    }
                }
            }

            internal void Remove (int index, IList<IPeer> peersInvolved)
            {
                alreadyRequestedBitField[index] = false;
                requests.Remove (index);
                duplicates.Remove (index);

                for (int i = 0; i < peersInvolved.Count; i++) {
                    var involvedPeer = peersInvolved[i];
                    if (mostRecentRequest.TryGetValue (involvedPeer, out Piece? piece))
                        if (piece.Index == index)
                            mostRecentRequest.Remove (involvedPeer);
                }
            }

            internal bool TryGetValue (int pieceIndex, [MaybeNullWhen (false)] out Piece piece)
                => requests.TryGetValue (pieceIndex, out piece);

            internal bool TryGetDuplicates (int pieceIndex, [MaybeNullWhen (false)] out List<Piece> extraPieces)
                => duplicates.TryGetValue (pieceIndex, out extraPieces);

            internal bool TryGetMostRecentRequest (IPeer peer, [MaybeNullWhen (false)] out Piece mostRecent)
                => mostRecentRequest.TryGetValue (peer, out mostRecent);

            internal void CreateDuplicates (int index, List<Piece> extraPieces)
                => duplicates.Add (index, extraPieces);

            internal void AddRequest (IPeer peer, Piece piece)
            {
                requests.Add (piece.Index, piece);
                alreadyRequestedBitField[piece.Index] = true;
                mostRecentRequest[peer] = piece;
            }
        }
    }

    public partial class StandardPicker : IPiecePicker
    {
        static ICache<Piece> PieceCache { get; } = new Cache<Piece> (() => new Piece (-1, -1)).Synchronize ();

        // static readonly Logger logger = Logger.Create (nameof(StandardPicker));

        BitField? CanRequestBitField;
        PickedPieces? Requests { get; set; }
        ITorrentInfo? TorrentData { get; set; }

        public StandardPicker ()
        {
        }

        public int AbortRequests (IPeer peer)
        {
            return CancelWhere (b => peer == b.RequestedOff);
        }

        public IList<BlockInfo> CancelRequests (IPeer peer, int startIndex, int endIndex)
        {
            if (Requests == null)
                return Array.Empty<BlockInfo> ();

            IList<BlockInfo>? cancelled = null;
            foreach (var piece in Requests.Values) {
                if (piece.Index < startIndex || piece.Index > endIndex)
                    continue;

                Block[] blocks = piece.Blocks;
                for (int i = 0; i < blocks.Length; i++) {
                    if (!blocks[i].Received && blocks[i].RequestedOff == peer) {
                        blocks[i].CancelRequest ();
                        piece.Abandoned = true;
                        cancelled ??= new List<BlockInfo> ();
                        cancelled.Add (new BlockInfo (piece.Index, blocks[i].StartOffset, blocks[i].RequestLength));
                    }
                }
            }

            return cancelled ?? Array.Empty<BlockInfo> ();
        }

        public void RequestRejected (IPeer peer, BlockInfo rejectedRequest)
        {
            CancelWhere (b => b.StartOffset == rejectedRequest.StartOffset &&
                              b.RequestLength == rejectedRequest.RequestLength &&
                              b.PieceIndex == rejectedRequest.PieceIndex &&
                              b.RequestedOff == peer);
        }


        readonly Stack<Piece> toRemove = new Stack<Piece> ();
        int CancelWhere (Predicate<Block> predicate)
        {
            if (Requests == null)
                return 0;

            int count = 0;
            foreach (var piece in Requests.Values) {
                Block[] blocks = piece.Blocks;
                for (int i = 0; i < blocks.Length; i++) {
                    if (predicate (blocks[i]) && !blocks[i].Received) {
                        piece.Abandoned = true;
                        blocks[i].CancelRequest ();
                        count++;
                    }
                }
                if (piece.NoBlocksRequested)
                    toRemove.Push (piece);
            }

            while (toRemove.Count > 0) {
                var piece = toRemove.Pop ();
                Requests.Remove (piece.Index);
            }
            return count;
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
                        list.Add (new ActivePieceRequest (block.PieceIndex, block.StartOffset, block.RequestLength, block.RequestedOff, block.Received));
                }
            }
            return list;
        }

        public void Initialise (ITorrentManagerInfo torrentData)
        {
            TorrentData = torrentData.TorrentInfo!;

            CanRequestBitField = new BitField (TorrentData.PieceCount ());
            Requests = new PickedPieces (TorrentData.PieceCount ());
        }

        public bool IsInteresting (IPeer peer, ReadOnlyBitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public int PickPiece (IPeer peer, ReadOnlyBitField available, IReadOnlyList<IPeer> otherPeers, int startIndex, int endIndex, Span<BlockInfo> requests)
        {
            BlockInfo? message;

            // If there is already a request on this peer, try to request the next block. If the peer is choking us, then the only
            // requests that could be continued would be existing "Fast" pieces.
            if ((message = ContinueExistingRequest (peer, startIndex, endIndex, 1, false, false)) != null) {
                requests[0] = message.Value;
                return 1;
            }

            // Then we check if there are any allowed "Fast" pieces to download
            if (peer.IsChoking && (message = GetFromList (peer, available, peer.IsAllowedFastPieces)) != null) {
                requests[0] = message.Value;
                return 1;
            }

            // If the peer is choking, then we can't download from them as they had no "fast" pieces for us to download
            if (peer.IsChoking)
                return 0;

            // Only try to continue an abandoned piece if this peer has not recently been involved in downloading data which
            // failed it's hash check.
            if (peer.RepeatedHashFails == 0 && (message = ContinueExistingRequest (peer, startIndex, endIndex, 1, true, false)) != null) {
                requests[0] = message.Value;
                return 1;
            }

            // We see if the peer has suggested any pieces we should request
            if ((message = GetFromList (peer, available, peer.SuggestedPieces)) != null) {
                requests[0] = message.Value;
                return 1;
            }

            // Now we see what pieces the peer has that we don't have and try and request one
            return GetStandardRequest (peer, available, startIndex, endIndex, requests);
        }

        public void Reset ()
        {
            if (TorrentData != null)
                Requests = new PickedPieces (TorrentData.PieceCount ());
        }

        static readonly Func<Piece, int, int> IndexComparer = (Piece piece, int comparand)
            => piece.Index.CompareTo (comparand);

        public bool ValidatePiece (IPeer peer, BlockInfo request, out bool pieceComplete, out IList<IPeer> peersInvolved)
        {
            if (Requests == null || !Requests.TryGetValue (request.PieceIndex, out Piece? primaryPiece)) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} No piece.", request.PieceIndex, request.StartOffset, peer);
                pieceComplete = false;
                peersInvolved = Array.Empty<IPeer> ();
                return false;
            }

            var result = ValidateRequestWithPiece (peer, request, primaryPiece, out pieceComplete, out peersInvolved);

            // If there are no duplicate requests, exit early! Otherwise we'll need to do book keeping to
            // ensure our received piece is not re-requested again.
            if (!Requests.TryGetDuplicates (request.PieceIndex, out List<Piece>? extraPieces)) {
                if (pieceComplete) {
                    Requests.Remove (request.PieceIndex, peersInvolved);
                    PieceCache.Enqueue (primaryPiece);
                }
                return result;
            }

            // We have duplicate requests and have, so far, failed to validate the block. Try to validate it now.
            if (!result) {
                for (int i = 0; i < extraPieces.Count && !result; i++)
                    if ((result = ValidateRequestWithPiece (peer, request, extraPieces[i], out pieceComplete, out peersInvolved)))
                        break;
            }


            // If we successfully validated the block using *any* version of the request, update the primary piece and
            // all duplicates to reflect this. This will implicitly cancel any outstanding requests from other peers.
            if (result) {
                primaryPiece.Blocks[request.StartOffset / Piece.BlockSize].TrySetReceived (peer);
                for (int i = 0; i < extraPieces.Count; i++)
                    extraPieces[i].Blocks[request.StartOffset / Piece.BlockSize].TrySetReceived (peer);
            }

            // If the piece is complete then remove it, and any dupes, from the picker.
            if (pieceComplete) {
                Requests.Remove (request.PieceIndex, peersInvolved);
                PieceCache.Enqueue (primaryPiece);
                return result;
            }
            return result;
        }

        bool ValidateRequestWithPiece (IPeer peer, BlockInfo request, Piece piece, out bool pieceComplete, out IList<IPeer> peersInvolved)
        {
            pieceComplete = false;
            peersInvolved = Array.Empty<IPeer> ();

            // Pick out the block that this piece message belongs to
            int blockIndex = Block.IndexOf (piece.Blocks, request.StartOffset, request.RequestLength);
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
                peersInvolved = piece.CalculatePeersInvolved ();
            }
            return true;
        }

        public BlockInfo? ContinueExistingRequest (IPeer peer, int startIndex, int endIndex)
            => ContinueExistingRequest (peer, startIndex, endIndex, 1, false, false);

        BlockInfo? ContinueExistingRequest (IPeer peer, int startIndex, int endIndex, int maxDuplicateRequests, bool allowAbandoned, bool allowAny)
        {
            if (Requests is null || TorrentData is null)
                return null;

            if (Requests.TryGetMostRecentRequest (peer, out Piece? mostRecent)) {
                foreach (ref Block block in mostRecent.Blocks.AsSpan ())
                    if (!block.Requested && !block.Received)
                        return block.CreateRequest (peer);

                if (maxDuplicateRequests == 1 && !allowAbandoned && !allowAny)
                    return null;
            }

            foreach (var p in Requests.Values) {
                int index = p.Index;
                if (p.AllBlocksRequested || index < startIndex || index > endIndex || !peer.BitField[index])
                    continue;

                // For each piece that was assigned to this peer, try to request a block from it
                // A piece is 'assigned' to a peer if he is the first person to request a block from that piece
                if (allowAny || (allowAbandoned && p.Abandoned) || peer == p.Blocks[0].RequestedOff) {
                    foreach (ref Block block in p.Blocks.AsSpan ()) {
                        if (!block.Requested && !block.Received)
                            return block.CreateRequest (peer);
                    }
                }
            }

            // If all blocks have been requested at least once and we're allowed more than 1 request then
            // let's try and issue a duplicate!
            for (int duplicate = 1; duplicate < maxDuplicateRequests; duplicate++) {
                foreach (var primaryPiece in Requests.Values) {
                    if (primaryPiece.Index < startIndex || primaryPiece.Index > endIndex || !peer.BitField[primaryPiece.Index])
                        continue;

                    if (!Requests.TryGetDuplicates (primaryPiece.Index, out List<Piece>? extraPieces)) {
                        extraPieces = new List<Piece> ();
                        Requests.CreateDuplicates (primaryPiece.Index, extraPieces);
                    }

                    if (extraPieces.Count < duplicate) {
                        var newPiece = PieceCache.Dequeue ().Initialise (primaryPiece.Index, TorrentData.BytesPerPiece (primaryPiece.Index));
                        for (int i = 0; i < primaryPiece.BlockCount; i++)
                            if (primaryPiece.Blocks[i].Received)
                                newPiece.Blocks[i].TrySetReceived (primaryPiece.Blocks[i].RequestedOff!);
                        extraPieces.Add (newPiece);
                    }

                    for (int extraPieceIndex = 0; extraPieceIndex < extraPieces.Count; extraPieceIndex++) {
                        var extraPiece = extraPieces[extraPieceIndex];
                        for (int i = 0; i < extraPiece.BlockCount; i++)
                            if (!extraPiece.Blocks[i].Requested && !HasAlreadyRequestedBlock (primaryPiece, extraPieces, peer, i))
                                return extraPiece.Blocks[i].CreateRequest (peer);
                    }
                }
            }

            // If we get here it means all the blocks in the pieces being downloaded by the peer are already requested
            return null;
        }

        static bool HasAlreadyRequestedBlock (Piece piece, IList<Piece> extraPieces, IPeer peer, int blockIndex)
        {
            if (piece.Blocks[blockIndex].RequestedOff == peer)
                return true;
            for (int i = 0; i < extraPieces.Count; i++)
                if (extraPieces[i].Blocks[blockIndex].RequestedOff == peer)
                    return true;
            return false;
        }

        public BlockInfo? ContinueAnyExistingRequest (IPeer peer, int startIndex, int endIndex, int maxDuplicateRequests)
        {
            // If this peer is currently a 'dodgy' peer, then don't allow him to help with someone else's
            // piece request.
            if (peer.RepeatedHashFails != 0)
                return null;

            return ContinueExistingRequest (peer, startIndex, endIndex, maxDuplicateRequests, true, true);
        }

        BlockInfo? GetFromList (IPeer peer, ReadOnlyBitField bitfield, IList<int> pieces)
        {
            if (!peer.SupportsFastPeer || Requests is null || TorrentData is null)
                return null;

            for (int i = 0; i < pieces.Count; i++) {
                int index = pieces[i];
                if (index >= bitfield.Length || !bitfield[index] || Requests.AlreadyRequestedBitfield [index])
                    continue;

                pieces.RemoveAt (i);
                var p = PieceCache.Dequeue ().Initialise (index, TorrentData.BytesPerPiece (index));
                Requests.AddRequest (peer, p);
                return p.Blocks[0].CreateRequest (peer);
            }


            return null;
        }

        int GetStandardRequest (IPeer peer, ReadOnlyBitField current, int startIndex, int endIndex, Span<BlockInfo> requests)
        {
            if (TorrentData == null || Requests == null)
                return 0;

            int piecesNeeded = (requests.Length * Piece.BlockSize) / TorrentData.PieceLength;
            if ((requests.Length * Piece.BlockSize) % TorrentData.PieceLength != 0)
                piecesNeeded++;
            int checkIndex = CanRequest (current, startIndex, endIndex, ref piecesNeeded);

            // Nothing to request.
            if (checkIndex == -1)
                return 0;

            var totalRequested = 0;
            for (int i = 0; totalRequested < requests.Length && i < piecesNeeded; i++) {
                // Request the piece
                var p = PieceCache.Dequeue ().Initialise (checkIndex + i, TorrentData.BytesPerPiece (checkIndex + i));
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
