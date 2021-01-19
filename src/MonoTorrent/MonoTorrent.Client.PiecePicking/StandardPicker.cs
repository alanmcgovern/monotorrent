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

using MonoTorrent.Logging;

namespace MonoTorrent.Client.PiecePicking
{
    public class StandardPicker : IPiecePicker
    {
        static readonly Logger logger = Logger.Create ();

        readonly SortList<Piece> requests;

        ITorrentData TorrentData { get; set; }

        public StandardPicker ()
        {
            requests = new SortList<Piece> ();
        }

        public int AbortRequests (IPieceRequester peer)
        {
            return CancelWhere (b => peer == b.RequestedOff);
        }

        public IList<PieceRequest> CancelRequests (IPieceRequester peer, int startIndex, int endIndex)
        {
            IList<PieceRequest> cancelled = null;

            for (int p = 0; p < requests.Count; p++) {
                Piece piece = requests[p];
                if (piece.Index < startIndex || piece.Index > endIndex)
                    continue;

                Block[] blocks = requests[p].Blocks;
                for (int i = 0; i < blocks.Length; i++) {
                    if (!blocks[i].Received && blocks[i].RequestedOff == peer) {
                        blocks[i].CancelRequest ();
                        requests[p].Abandoned = true;
                        cancelled ??= new List<PieceRequest> ();
                        cancelled.Add (new PieceRequest (piece.Index, blocks[i].StartOffset, blocks[i].RequestLength));
                    }
                }
            }

            return cancelled ?? Array.Empty<PieceRequest> ();
        }

        public void RequestRejected (IPieceRequester peer, PieceRequest rejectedRequest)
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
            foreach(var piece in requests) {
                foreach (var block in piece.Blocks) {
                    if (block.Requested)
                        list.Add (new ActivePieceRequest (block.PieceIndex, block.StartOffset, block.RequestLength, block.Received, block.RequestedOff));
                    // FIXME: Include 'RequestedOff'
                }
            }
            return list;
        }

        public void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests)
        {
            TorrentData = torrentData;
            this.requests.Clear ();
            foreach (var group in requests.GroupBy (p => p.PieceIndex)) {
                var piece = new Piece (group.Key, torrentData.PieceLength, torrentData.Size);
                foreach (var block in group)
                    piece.Blocks[block.StartOffset / Piece.BlockSize].FromRequest (block);
                this.requests.Add (piece);
            }
        }

        public bool IsInteresting (IPieceRequester peer, BitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            PieceRequest? message;
            IList<PieceRequest> bundle;

            // If there is already a request on this peer, try to request the next block. If the peer is choking us, then the only
            // requests that could be continued would be existing "Fast" pieces.
            if ((message = ContinueExistingRequest (peer, startIndex, endIndex, false, false)) != null)
                return new[] { message.Value };

            // Then we check if there are any allowed "Fast" pieces to download
            if (peer.IsChoking && (message = GetFromList (peer, available, peer.IsAllowedFastPieces)) != null)
                return new[] { message.Value };

            // If the peer is choking, then we can't download from them as they had no "fast" pieces for us to download
            if (peer.IsChoking)
                return null;

            if ((message = ContinueExistingRequest (peer, startIndex, endIndex, true, false)) != null)
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
            requests.Clear ();
        }

        static readonly Func<Piece, int, int> IndexComparer = (Piece piece, int comparand)
            => piece.Index.CompareTo (comparand);

        public void Tick ()
        {
            // no-op
        }

        public bool ValidatePiece (IPieceRequester peer, PieceRequest request, out bool pieceComplete, out IList<IPieceRequester> peersInvolved)
        {
            int pIndex = requests.BinarySearch (IndexComparer, request.PieceIndex);
            pieceComplete = false;
            peersInvolved = null;

            if (pIndex < 0) {
                logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} No piece.", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            var piece = requests[pIndex];
            // Pick out the block that this piece message belongs to
            int blockIndex = Block.IndexOf (piece.Blocks, request.StartOffset, request.RequestLength);
            if (blockIndex == -1 || !peer.Equals (piece.Blocks[blockIndex].RequestedOff)) {
                logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} No block ", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            if (piece.Blocks[blockIndex].Received) {
                logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} Already received.", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            if (!piece.Blocks[blockIndex].Requested) {
                logger.InfoFormatted ("Piece validation failed: {0}-{1}. {2} Not requested.", request.PieceIndex, request.StartOffset, peer);
                return false;
            }
            peer.AmRequestingPiecesCount--;
            piece.Blocks[blockIndex].Received = true;

            if (piece.AllBlocksReceived) {
                pieceComplete = true;
                peersInvolved = piece.Blocks.Select (t => t.RequestedOff).Distinct ().ToArray ();
                requests.RemoveAt (pIndex);
            }
            return true;
        }

        public PieceRequest? ContinueExistingRequest (IPieceRequester peer, int startIndex, int endIndex)
            => ContinueExistingRequest (peer, startIndex, endIndex, false, false);

        PieceRequest? ContinueExistingRequest (IPieceRequester peer, int startIndex, int endIndex, bool allowAbandoned, bool allowAny)
        {
            for (int req = 0; req < requests.Count; req++) {
                Piece p = requests[req];
                if (p.Index < startIndex || p.Index > endIndex || p.AllBlocksRequested || !peer.BitField[p.Index])
                    continue;

                // For each piece that was assigned to this peer, try to request a block from it
                // A piece is 'assigned' to a peer if he is the first person to request a block from that piece
                if (allowAny || (allowAbandoned && p.Abandoned && peer.RepeatedHashFails == 0) || peer == p.Blocks[0].RequestedOff) {
                    for (int i = 0; i < p.BlockCount; i++) {
                        if (!p.Blocks[i].Received && !p.Blocks[i].Requested)
                            return p.Blocks[i].CreateRequest (peer);
                    }
                }
            }

            // If we get here it means all the blocks in the pieces being downloaded by the peer are already requested
            return null;
        }

        public PieceRequest? ContinueAnyExistingRequest (IPieceRequester peer, int startIndex, int endIndex)
        {
            // If this peer is currently a 'dodgy' peer, then don't allow him to help with someone else's
            // piece request.
            if (peer.RepeatedHashFails != 0)
                return null;

            return ContinueExistingRequest (peer, startIndex, endIndex, true, true);
        }

        PieceRequest? GetFromList (IPieceRequester peer, BitField bitfield, IList<int> pieces)
        {
            if (!peer.SupportsFastPeer || !ClientEngine.SupportsFastPeer)
                return null;

            for (int i = 0; i < pieces.Count; i++) {
                int index = pieces[i];
                // A peer should only suggest a piece he has, but just in case.
                if (index >= bitfield.Length || !bitfield[index] || AlreadyRequested (index))
                    continue;

                pieces.RemoveAt (i);
                var p = new Piece (index, TorrentData.PieceLength, TorrentData.Size);
                requests.Add (p);
                return p.Blocks[0].CreateRequest (peer);
            }


            return null;
        }

        IList<PieceRequest> GetStandardRequest (IPieceRequester peer, BitField current, int startIndex, int endIndex, int count)
        {
            int piecesNeeded = (count * Piece.BlockSize) / TorrentData.PieceLength;
            if ((count * Piece.BlockSize) % TorrentData.PieceLength != 0)
                piecesNeeded++;
            int checkIndex = CanRequest (current, startIndex, endIndex, ref piecesNeeded);

            // Nothing to request.
            if (checkIndex == -1)
                return null;

            var bundle = new List<PieceRequest> (count);
            for (int i = 0; bundle.Count < count && i < piecesNeeded; i++) {
                // Request the piece
                var p = new Piece (checkIndex + i, TorrentData.PieceLength, TorrentData.Size);
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
            while (pieceStartIndex <= pieceEndIndex  && (pieceStartIndex = bitfield.FirstTrue (pieceStartIndex, pieceEndIndex)) != -1) {
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
