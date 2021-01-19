//
// EndgamePicker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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

namespace MonoTorrent.Client.PiecePicking
{
    // Keep a list of all the pieces which have not yet being fully downloaded
    // From this list we will make requests for all the blocks until the piece is complete.
    public class EndGamePicker : IPiecePicker
    {
        // This list stores all the pieces which have not yet been completed. If a piece is *not* in this list
        // we don't need to download it.
        List<Piece> Pieces { get; }

        // These are all the requests for the individual blocks
        internal List<ActivePieceRequest> Requests { get; }

        ITorrentData TorrentData { get; set; }

        public EndGamePicker ()
        {
            Pieces = new List<Piece> ();
            Requests = new List<ActivePieceRequest> ();
        }

        public int AbortRequests (IPieceRequester peer)
        {
            throw new NotImplementedException ();
        }

        // Cancels a pending request when the predicate returns 'true'
        void CancelWhere (Predicate<ActivePieceRequest> predicate, bool sendCancel)
        {
            for (int i = 0; i < Requests.Count; i++) {
                ActivePieceRequest r = Requests[i];
                if (predicate (r)) {
                    r.RequestedOff.AmRequestingPiecesCount--;
                    if (sendCancel)
                        r.RequestedOff.CancelRequest (r);
                }
            }
            Requests.RemoveAll (predicate);
        }

        public PieceRequest? ContinueAnyExistingRequest (IPieceRequester peer, int startIndex, int endIndex)
        {
            return null;
        }
        public PieceRequest? ContinueExistingRequest (IPieceRequester peer, int startIndex, int endIndex)
        {
            return null;
        }

        public int CurrentReceivedCount ()
        {
            return (int) Toolbox.Accumulate (Pieces, p => p.TotalReceived);
        }

        public int CurrentRequestCount ()
        {
            return Requests.Count;
        }

        public IList<ActivePieceRequest> ExportActiveRequests ()
        {
            return new List<ActivePieceRequest> (Requests);
        }

        public void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests)
        {
            Requests.Clear ();
            Pieces.Clear ();

            // 'Requests' should contain a list of all the pieces we need to complete
            foreach (var pieceRequests in requests.GroupBy (p => p.Request.PieceIndex)) {
                var piece = new Piece (pieceRequests.Key, torrentData.PieceLength, torrentData.Size);
                Pieces.Add (piece);
                foreach (var request in pieceRequests)
                    piece.Blocks[request.Request.StartOffset / Piece.BlockSize].FromRequest (request);
            }
            Requests.AddRange (requests.Where (r => !r.Received));
            TorrentData = torrentData;
        }

        public bool IsInteresting (IPieceRequester peer, BitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            // Only request 2 pieces at a time in endgame mode
            // to prevent a *massive* overshoot
            if (peer.IsChoking || peer.AmRequestingPiecesCount >= 2)
                return null;

            LoadPieces (available);

            // 1) See if there are any blocks which have not been requested at all. Request the block if the peer has it
            foreach (Piece p in Pieces) {
                if (!available[p.Index] || p.AllBlocksRequested)
                    continue;

                for (int i = 0; i < p.BlockCount; i++) {
                    if (p.Blocks[i].Requested)
                        continue;
                    PieceRequest requestMessage = p.Blocks[i].CreateRequest (peer);
                    Requests.Add (new ActivePieceRequest (requestMessage, peer));
                    return new[] { requestMessage };
                }
            }

            // 2) For each block with an existing request, add another request. We do a search from the start
            //    of the list to the end. So when we add a duplicate request, move both requests to the end of the list
            foreach (Piece p in Pieces) {
                if (!available[p.Index])
                    continue;

                for (int i = 0; i < p.BlockCount; i++) {
                    if (p.Blocks[i].Received || AlreadyRequested (p.Blocks[i], peer))
                        continue;

                    int c = Requests.Count;
                    for (int j = 0; j < Requests.Count - 1 && (c-- > 0); j++) {
                        if (Requests[j].Request.PieceIndex == p.Index && Requests[j].Request.StartOffset == p.Blocks[i].StartOffset) {
                            var r = Requests[j];
                            Requests.RemoveAt (j);
                            Requests.Add (r);
                            j--;
                        }
                    }
                    PieceRequest requestMessage = p.Blocks[i].CreateRequest (peer);
                    Requests.Add (new ActivePieceRequest (requestMessage, peer));
                    return new[] { requestMessage };
                }
            }

            return null;
        }

        public void RequestRejected (IPieceRequester peer, PieceRequest rejectedRequest)
        {
            throw new NotImplementedException ();
        }

        void LoadPieces (BitField b)
        {
            int lastPiece = b.Length - 1;
            int piece = 0;
            while (piece <= lastPiece && (piece = b.FirstTrue (piece, lastPiece)) != -1) {
                if (!Pieces.Exists (p => p.Index == piece))
                    Pieces.Add (new Piece (piece, TorrentData.PieceLength, TorrentData.Size));
                piece++;
            }
        }

        bool AlreadyRequested (Block block, IPieceRequester peer)
        {
            bool b = Requests.Exists (r => r.Request.PieceIndex == block.PieceIndex &&
                                           r.Request.StartOffset == block.StartOffset &&
                                           r.RequestedOff == peer);
            return b;
        }

        public void CancelRequest (IPieceRequester peer, int piece, int startOffset, int length)
        {
            CancelWhere (r => r.Request.PieceIndex == piece &&
                              r.Request.StartOffset == startOffset &&
                              r.Request.RequestLength == length &&
                              r.RequestedOff == peer, false);
        }

        public IList<PieceRequest> CancelRequests (IPieceRequester peer, int startIndex, int endIndex)
        {
            var existingRequests = Requests.Where (r => r.RequestedOff == peer && r.Request.PieceIndex >= startIndex && r.Request.PieceIndex <= endIndex && !r.Received).ToArray ();
            Requests.RemoveAll (r => existingRequests.Contains (r));
            peer.AmRequestingPiecesCount -= existingRequests.Length;
            return existingRequests.Select (p => new PieceRequest (p.Request.PieceIndex, p.Request.StartOffset, p.Request.RequestLength)).ToArray ();
        }

        public void Tick ()
        {
            // no-op
        }

        public bool ValidatePiece (IPieceRequester peer, PieceRequest request, out bool pieceComplete, out IList<IPieceRequester> peersInvolved)
        {
            pieceComplete = false;
            peersInvolved = null;
            var maybe_result = Requests.Cast<ActivePieceRequest?>().SingleOrDefault (t => t.Value.Request == request && t.Value.RequestedOff == peer);
            if (!maybe_result.HasValue) {
                return false;
            }

            var r = maybe_result.Value;
            var piece = Pieces.Single (p => p.Index == r.Request.PieceIndex);
            if (piece == null)
                return false;

            // All the other requests for this block need to be cancelled.
            CancelWhere (req => req.Request == request &&
                                req.RequestedOff != peer, true);

            // Mark the block as received
            piece.Blocks[request.StartOffset / Piece.BlockSize].Received = true;

            Requests.Remove (r);
            peer.AmRequestingPiecesCount--;

            // Once a piece is completely received, remove it from our list.
            // If a piece *fails* the hashcheck, we need to add it back into the list so
            // we download it again.
            if (piece.AllBlocksReceived) {
                Pieces.Remove (piece);
                CancelWhere (r => r.Request.PieceIndex == request.PieceIndex, false);
                pieceComplete = true;
                peersInvolved = piece.Blocks.Select (p => p.RequestedOff).Where (t => t != null).ToArray ();
            }

            return true;
        }
    }
}
