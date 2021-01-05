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
        internal List<PieceRequest> Requests { get; }

        ITorrentData TorrentData { get; set; }

        public EndGamePicker ()
        {
            Pieces = new List<Piece> ();
            Requests = new List<PieceRequest> ();
        }

        public int AbortRequests (IPieceRequester peer)
        {
            throw new NotImplementedException ();
        }

        // Cancels a pending request when the predicate returns 'true'
        void CancelWhere (Predicate<PieceRequest> predicate, bool sendCancel)
        {
            for (int i = 0; i < Requests.Count; i++) {
                PieceRequest r = Requests[i];
                if (predicate (r)) {
                    r.RequestedOff.AmRequestingPiecesCount--;
                    if (sendCancel)
                        r.RequestedOff.CancelRequest (r);
                }
            }
            Requests.RemoveAll (predicate);
        }

        public PieceRequest ContinueAnyExisting (IPieceRequester peer, int startIndex, int endIndex)
        {
            return null;
        }
        public PieceRequest ContinueExistingRequest (IPieceRequester peer, int startIndex, int endIndex)
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

        public IList<PieceRequest> ExportActiveRequests ()
        {
            return new List<PieceRequest> (Requests);
        }

        public void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<PieceRequest> requests)
        {
            Requests.Clear ();
            Pieces.Clear ();

            // 'Requests' should contain a list of all the pieces we need to complete
            foreach (var pieceRequests in requests.GroupBy (p => p.PieceIndex)) {
                var piece = new Piece (pieceRequests.Key, torrentData.PieceLength, torrentData.Size);
                Pieces.Add (piece);
                foreach (var request in pieceRequests)
                    piece.Blocks[request.StartOffset / Piece.BlockSize].FromRequest (request);
            }
            Requests.AddRange (requests.Where (r => !r.Received));
            TorrentData = torrentData;
        }

        public bool IsInteresting (BitField bitfield)
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
                    Requests.Add (requestMessage);
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
                        if (Requests[j].PieceIndex == p.Index && Requests[j].StartOffset == p.Blocks[i].StartOffset) {
                            var r = Requests[j];
                            Requests.RemoveAt (j);
                            Requests.Add (r);
                            j--;
                        }
                    }
                    PieceRequest requestMessage = p.Blocks[i].CreateRequest (peer);
                    Requests.Add (requestMessage);
                    return new[] { requestMessage };
                }
            }

            return null;
        }

        public void RequestRejected (PieceRequest request)
        {
            throw new NotImplementedException ();
        }

        void LoadPieces (BitField b)
        {
            int length = b.Length;
            for (int i = b.FirstTrue (0, length); i != -1; i = b.FirstTrue (i + 1, length))
                if (!Pieces.Exists (p => p.Index == i))
                    Pieces.Add (new Piece (i, TorrentData.PieceLength, TorrentData.Size));
        }

        bool AlreadyRequested (Block block, IPieceRequester peer)
        {
            bool b = Requests.Exists (r => r.PieceIndex == block.PieceIndex &&
                                           r.StartOffset == block.StartOffset &&
                                           r.RequestedOff == peer);
            return b;
        }

        public void CancelRequest (IPieceRequester peer, int piece, int startOffset, int length)
        {
            CancelWhere (r => r.PieceIndex == piece &&
                              r.StartOffset == startOffset &&
                              r.RequestLength == length &&
                              r.RequestedOff == peer, false);
        }

        public IList<PieceRequest> CancelRequests (IPieceRequester peer, int startIndex, int endIndex)
        {
            var existingRequests = Requests.Where (r => r.RequestedOff == peer && r.PieceIndex >= startIndex && r.PieceIndex <= endIndex && !r.Received).ToArray ();
            Requests.RemoveAll (r => existingRequests.Contains (r));
            peer.AmRequestingPiecesCount -= existingRequests.Length;
            return existingRequests;
        }

        public void Tick ()
        {
            // no-op
        }

        public bool ValidatePiece (IPieceRequester peer, int pieceIndex, int startOffset, int length, out bool pieceComplete, out IList<IPieceRequester> peersInvolved)
        {
            pieceComplete = false;
            peersInvolved = null;
            var r = Requests.SingleOrDefault (t => t.PieceIndex == pieceIndex && t.StartOffset == startOffset && t.RequestLength == length && t.RequestedOff == peer);
            if (r == null) {
                return false;
            }

            var piece = Pieces.Single (p => p.Index == r.PieceIndex);
            if (piece == null)
                return false;

            // All the other requests for this block need to be cancelled.
            CancelWhere (req => req.PieceIndex == pieceIndex &&
                                req.StartOffset == startOffset &&
                                req.RequestLength == length &&
                                req.RequestedOff != peer, true);

            // Mark the block as received
            piece.Blocks[startOffset / Piece.BlockSize].Received = true;

            Requests.Remove (r);
            peer.AmRequestingPiecesCount--;

            // Once a piece is completely received, remove it from our list.
            // If a piece *fails* the hashcheck, we need to add it back into the list so
            // we download it again.
            if (piece.AllBlocksReceived) {
                Pieces.Remove (piece);
                CancelWhere (r => r.PieceIndex == pieceIndex, false);
                pieceComplete = true;
                peersInvolved = piece.Blocks.Select (p => p.RequestedOff).Where (t => t != null).ToArray ();
            }

            return true;
        }
    }
}
