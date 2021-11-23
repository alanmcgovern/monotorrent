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
using System.Linq;

// using MonoTorrent.Logging;

namespace MonoTorrent.PiecePicking
{
    public class StandardPicker : IPiecePicker
    {
        // static readonly Logger logger = Logger.Create (nameof(StandardPicker));

        readonly Dictionary<int, List<Piece>> duplicates;
        readonly SortList<Piece> requests;

        ITorrentData TorrentData { get; set; }

        public StandardPicker ()
        {
            duplicates = new Dictionary<int, List<Piece>> ();
            requests = new SortList<Piece> ();
        }

        public int AbortRequests (IPeer peer)
        {
            return CancelWhere (b => peer == b.RequestedOff);
        }

        public IList<BlockInfo> CancelRequests (IPeer peer, int startIndex, int endIndex)
        {
            IList<BlockInfo> cancelled = null;

            for (int p = 0; p < requests.Count; p++) {
                Piece piece = requests[p];
                if (piece.Index < startIndex || piece.Index > endIndex)
                    continue;

                Block[] blocks = requests[p].Blocks;
                for (int i = 0; i < blocks.Length; i++) {
                    if (!blocks[i].Received && blocks[i].RequestedOff == peer) {
                        blocks[i].CancelRequest ();
                        requests[p].Abandoned = true;
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

        int CancelWhere (Predicate<Block> predicate)
        {
            int count = 0;
            for (int p = 0; p < requests.Count; p++) {
                Block[] blocks = requests[p].Blocks;
                for (int i = 0; i < blocks.Length; i++) {
                    if (predicate (blocks[i]) && !blocks[i].Received) {
                        requests[p].Abandoned = true;
                        blocks[i].CancelRequest ();
                        count++;
                    }
                }
            }

            if (count > 0)
                requests.RemoveAll (p => p.NoBlocksRequested);
            return count;
        }

        public int CurrentReceivedCount ()
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
                count += requests[i].TotalReceived;
            return count;
        }

        public int CurrentRequestCount ()
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
                count += requests[i].TotalRequested - requests[i].TotalReceived;
            return count;
        }

        public IList<ActivePieceRequest> ExportActiveRequests ()
        {
            var list = new List<ActivePieceRequest> ();
            foreach (var piece in requests) {
                foreach (var block in piece.Blocks) {
                    if (block.Requested)
                        list.Add (new ActivePieceRequest (block.PieceIndex, block.StartOffset, block.RequestLength, block.RequestedOff, block.Received));
                }
            }
            return list;
        }

        public void Initialise (ITorrentData torrentData)
        {
            TorrentData = torrentData;
            requests.Clear ();
        }

        public bool IsInteresting (IPeer peer, BitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public IList<BlockInfo> PickPiece (IPeer peer, BitField available, IReadOnlyList<IPeer> otherPeers, int count, int startIndex, int endIndex)
        {
            BlockInfo? message;
            IList<BlockInfo> bundle;

            // If there is already a request on this peer, try to request the next block. If the peer is choking us, then the only
            // requests that could be continued would be existing "Fast" pieces.
            if ((message = ContinueExistingRequest (peer, startIndex, endIndex, 1, false, false)) != null)
                return new[] { message.Value };

            // Then we check if there are any allowed "Fast" pieces to download
            if (peer.IsChoking && (message = GetFromList (peer, available, peer.IsAllowedFastPieces)) != null)
                return new[] { message.Value };

            // If the peer is choking, then we can't download from them as they had no "fast" pieces for us to download
            if (peer.IsChoking)
                return null;

            // Only try to continue an abandoned piece if this peer has not recently been involved in downloading data which
            // failed it's hash check.
            if (peer.RepeatedHashFails == 0 && (message = ContinueExistingRequest (peer, startIndex, endIndex, 1, true, false)) != null)
                return new[] { message.Value };

            // We see if the peer has suggested any pieces we should request
            if ((message = GetFromList (peer, available, peer.SuggestedPieces)) != null)
                return new[] { message.Value };

            // Now we see what pieces the peer has that we don't have and try and request one
            if ((bundle = GetStandardRequest (peer, available, startIndex, endIndex, count)) != null)
                return bundle;

            return null;
        }

        public void Reset ()
        {
            duplicates.Clear ();
            requests.Clear ();
        }

        static readonly Func<Piece, int, int> IndexComparer = (Piece piece, int comparand)
            => piece.Index.CompareTo (comparand);

        public bool ValidatePiece (IPeer peer, BlockInfo request, out bool pieceComplete, out IList<IPeer> peersInvolved)
        {
            int pIndex = requests.BinarySearch (IndexComparer, request.PieceIndex);
            pieceComplete = false;
            peersInvolved = null;

            if (pIndex < 0) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} No piece.", request.PieceIndex, request.StartOffset, peer);
                return false;
            }

            var primaryPiece = requests[pIndex];
            var result = ValidateRequestWithPiece (peer, request, primaryPiece, out pieceComplete, out peersInvolved);

            // If there are no duplicate requests, exit early! Otherwise we'll need to do book keeping to
            // ensure our received piece is not re-requested again.
            if (!duplicates.TryGetValue (request.PieceIndex, out List<Piece> extraPieces)) {
                if (pieceComplete)
                    requests.RemoveAt (pIndex);
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
                requests.RemoveAt (pIndex);
                duplicates.Remove (primaryPiece.Index);
                return result;
            }
            return result;
        }

        bool ValidateRequestWithPiece (IPeer peer, BlockInfo request, Piece piece, out bool pieceComplete, out IList<IPeer> peersInvolved)
        {
            pieceComplete = false;
            peersInvolved = null;

            // Pick out the block that this piece message belongs to
            int blockIndex = Block.IndexOf (piece.Blocks, request.StartOffset, request.RequestLength);
            if (blockIndex == -1 || !peer.Equals (piece.Blocks[blockIndex].RequestedOff)) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} No block ", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            if (piece.Blocks[blockIndex].Received) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} Already received.", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            if (!piece.Blocks[blockIndex].Requested) {
                //logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} Not requested.", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            peer.AmRequestingPiecesCount--;
            piece.Blocks[blockIndex].Received = true;

            if (piece.AllBlocksReceived) {
                pieceComplete = true;
                peersInvolved = piece.Blocks.Select (t => t.RequestedOff).Distinct ().ToArray ();
            }
            return true;
        }

        public BlockInfo? ContinueExistingRequest (IPeer peer, int startIndex, int endIndex)
            => ContinueExistingRequest (peer, startIndex, endIndex, 1, false, false);

        BlockInfo? ContinueExistingRequest (IPeer peer, int startIndex, int endIndex, int maxDuplicateRequests, bool allowAbandoned, bool allowAny)
        {
            for (int req = 0; req < requests.Count; req++) {
                Piece p = requests[req];
                int index = p.Index;
                if (p.AllBlocksRequested || index < startIndex || index > endIndex || !peer.BitField[index])
                    continue;

                // For each piece that was assigned to this peer, try to request a block from it
                // A piece is 'assigned' to a peer if he is the first person to request a block from that piece
                if (allowAny || (allowAbandoned && p.Abandoned) || peer == p.Blocks[0].RequestedOff) {
                    for (int i = 0; i < p.BlockCount; i++) {
                        if (!p.Blocks[i].Received && !p.Blocks[i].Requested)
                            return p.Blocks[i].CreateRequest (peer);
                    }
                }
            }

            // If all blocks have been requested at least once and we're allowed more than 1 request then
            // let's try and issue a duplicate!
            for (int duplicate = 1; duplicate < maxDuplicateRequests; duplicate++) {
                for (int req = 0; req < requests.Count; req++) {
                    Piece primaryPiece = requests[req];
                    if (primaryPiece.Index < startIndex || primaryPiece.Index > endIndex || !peer.BitField[primaryPiece.Index])
                        continue;

                    if (!duplicates.TryGetValue (primaryPiece.Index, out List<Piece> extraPieces))
                        duplicates[primaryPiece.Index] = extraPieces = new List<Piece> ();

                    if (extraPieces.Count < duplicate) {
                        var newPiece = new Piece (primaryPiece.Index, TorrentData.BytesPerPiece (primaryPiece.Index));
                        for (int i = 0; i < primaryPiece.BlockCount; i++)
                            if (primaryPiece.Blocks[i].Received)
                                newPiece.Blocks[i].TrySetReceived (primaryPiece.Blocks[i].RequestedOff);
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

        BlockInfo? GetFromList (IPeer peer, BitField bitfield, IList<int> pieces)
        {
            if (!peer.SupportsFastPeer)
                return null;

            for (int i = 0; i < pieces.Count; i++) {
                int index = pieces[i];
                // A peer should only suggest a piece he has, but just in case.
                if (index >= bitfield.Length || !bitfield[index] || AlreadyRequested (index))
                    continue;

                pieces.RemoveAt (i);
                var p = new Piece (index, TorrentData.BytesPerPiece (index));
                requests.Add (p);
                return p.Blocks[0].CreateRequest (peer);
            }


            return null;
        }

        IList<BlockInfo> GetStandardRequest (IPeer peer, BitField current, int startIndex, int endIndex, int count)
        {
            int piecesNeeded = (count * Piece.BlockSize) / TorrentData.PieceLength;
            if ((count * Piece.BlockSize) % TorrentData.PieceLength != 0)
                piecesNeeded++;
            int checkIndex = CanRequest (current, startIndex, endIndex, ref piecesNeeded);

            // Nothing to request.
            if (checkIndex == -1)
                return null;

            var bundle = new List<BlockInfo> (count);
            for (int i = 0; bundle.Count < count && i < piecesNeeded; i++) {
                // Request the piece
                var p = new Piece (checkIndex + i, TorrentData.BytesPerPiece (checkIndex + i));
                requests.Add (p);
                for (int j = 0; j < p.Blocks.Length && bundle.Count < count; j++)
                    bundle.Add (p.Blocks[j].CreateRequest (peer));
            }
            return bundle;
        }

        protected bool AlreadyRequested (int index)
        {
            return requests.BinarySearch (IndexComparer, index) >= 0;
        }

        int CanRequest (BitField bitfield, int pieceStartIndex, int pieceEndIndex, ref int pieceCount)
        {
            // This is the easiest case to consider - special case it
            if (pieceCount == 1) {
                while (pieceStartIndex <= pieceEndIndex && (pieceStartIndex = bitfield.FirstTrue (pieceStartIndex, pieceEndIndex)) != -1) {
                    var end = bitfield.FirstFalse (pieceStartIndex, pieceEndIndex);

                    // If end is a valid value, it's the first *false* piece. Subtract '1' from it
                    // to give us the last available piece we can request. If it's -1 then we can use
                    // 'pieceEndIndex' as the last available piece to request as all pieces are available.
                    var lastAvailable = end == -1 ? pieceEndIndex : end - 1;
                    for (int i = pieceStartIndex; i <= lastAvailable; i++)
                        if (!AlreadyRequested (i))
                            return i;
                    pieceStartIndex = lastAvailable + 1;
                }
                return -1;
            }

            int largestStart = 0;
            int largestEnd = 0;
            while (pieceStartIndex <= pieceEndIndex && (pieceStartIndex = bitfield.FirstTrue (pieceStartIndex, pieceEndIndex)) != -1) {
                int end = bitfield.FirstFalse (pieceStartIndex, pieceEndIndex);
                if (end == -1)
                    end = Math.Min (pieceStartIndex + pieceCount, bitfield.Length);

                // Do not include 'end' as it's the first *false* piece.
                for (int i = pieceStartIndex; i < end; i++)
                    if (AlreadyRequested (i))
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
