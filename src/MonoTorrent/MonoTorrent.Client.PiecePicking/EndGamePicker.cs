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
    public class EndGamePicker : PiecePicker
    {
        static readonly Predicate<Request> TimedOut = delegate (Request r) { return r.Block.RequestTimedOut; };

        // Struct to link a request for a block to a peer
        // This way we can have multiple requests for the same block
        internal class Request
        {
            public Request(IPieceRequester peer, Block block)
            {
                Peer = peer;
                Block = block;
            }
            public Block Block;
            public IPieceRequester Peer;
        }

        // This list stores all the pieces which have not yet been completed. If a piece is *not* in this list
        // we don't need to download it.
        List<Piece> pieces;

        // These are all the requests for the individual blocks
        List<Request> requests;
        internal List<Request> Requests => requests;

        ITorrentData TorrentData { get; set; }

        public EndGamePicker()
            : base(null)
        {
            requests = new List<Request>();
        }

        // Cancels a pending request when the predicate returns 'true'
        void CancelWhere(Predicate<Request> predicate, bool sendCancel)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                Request r = requests[i];
                if (predicate(r))
                {
                    r.Peer.AmRequestingPiecesCount--;
                    if (sendCancel)
                        r.Peer.Cancel(r.Block.PieceIndex, r.Block.StartOffset, r.Block.RequestLength);
                }
            }
            requests.RemoveAll(predicate);
        }

        public override void CancelTimedOutRequests()
        {
            CancelWhere(TimedOut, false);
        }

        public override PieceRequest ContinueExistingRequest(IPieceRequester peer)
        {
            return null;
        }

        public override int CurrentReceivedCount()
        {
            return (int) Toolbox.Accumulate (pieces, p => p.TotalReceived);
        }

        public override int CurrentRequestCount()
        {
            return requests.Count;
        }

        public override List<Piece> ExportActiveRequests()
        {
            return new List<Piece>(pieces);
        }

        public override void Initialise(BitField bitfield, ITorrentData torrentData, IEnumerable<Piece> requests)
        {
            // 'Requests' should contain a list of all the pieces we need to complete
            pieces = new List<Piece>(requests);
            TorrentData = torrentData;
            foreach (Piece piece in pieces)
            {
                for (int i = 0; i < piece.BlockCount; i++)
                    if (piece.Blocks[i].RequestedOff != null && !piece.Blocks[i].Received)
                        this.requests.Add(new Request(piece.Blocks[i].RequestedOff, piece.Blocks[i]));
            }
        }

        public override bool IsInteresting(BitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public override IList<PieceRequest> PickPiece(IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            // Only request 2 pieces at a time in endgame mode
            // to prevent a *massive* overshoot
            if (peer.IsChoking || peer.AmRequestingPiecesCount > 2)
                return null;

            LoadPieces(available);

            // 1) See if there are any blocks which have not been requested at all. Request the block if the peer has it
            foreach (Piece p in pieces)
            {
                if(!available[p.Index] || p.AllBlocksRequested)
                    continue;

                for (int i = 0; i < p.BlockCount; i++)
                {
                    if (p.Blocks[i].Requested)
                        continue;
                    var requestMessage = p.Blocks[i].CreateRequest (peer);
                    requests.Add(new Request(peer, p.Blocks[i]));
                    return new [] { requestMessage };
                }
            }

            // 2) For each block with an existing request, add another request. We do a search from the start
            //    of the list to the end. So when we add a duplicate request, move both requests to the end of the list
            foreach (Piece p in pieces)
            {
                if (!available[p.Index])
                    continue;

                for (int i = 0; i < p.BlockCount; i++)
                {
                    if (p.Blocks[i].Received || AlreadyRequested(p.Blocks[i], peer))
                        continue;

                    int c = requests.Count;
                    for (int j = 0; j < requests.Count - 1 && (c-- > 0); j++)
                    {
                        if (requests[j].Block.PieceIndex == p.Index && requests[j].Block.StartOffset == p.Blocks[i].StartOffset)
                        {
                            Request r = requests[j];
                            requests.RemoveAt(j);
                            requests.Add(r);
                            j--;
                        }
                    }
                    var requestMessage = p.Blocks[i].CreateRequest(peer);
                    requests.Add(new Request(peer, p.Blocks[i]));
                    return new [] { requestMessage };
                }
            }

            return null;
        }

        void LoadPieces(BitField b)
        {
            int length = b.Length;
            for (int i = b.FirstTrue(0, length); i != -1; i = b.FirstTrue(i + 1, length))
                if (!pieces.Exists(delegate(Piece p) { return p.Index == i; }))
                    pieces.Add(new Piece(i, TorrentData.PieceLength, TorrentData.Size));
        }

        private bool AlreadyRequested(Block block, IPieceRequester peer)
        {
            bool b = requests.Exists(delegate(Request r) {
                return r.Block.PieceIndex == block.PieceIndex &&
                       r.Block.StartOffset == block.StartOffset &&
                       r.Peer == peer;
            });
            return b;
        }

        public override void Reset()
        {
            // Though if you reset an EndGamePicker it really means that you should be using a regular picker now
            requests.Clear();
        }

        public override void CancelRequest(IPieceRequester peer, int piece, int startOffset, int length)
        {
            CancelWhere(delegate (Request r) {
                return r.Block.PieceIndex == piece &&
                       r.Block.StartOffset == startOffset &&
                       r.Block.RequestLength == length &&
                       peer == r.Peer;
            }, false);
        }

        public override void CancelRequests(IPieceRequester peer)
        {
            CancelWhere(delegate(Request r) { return r.Peer == peer; }, false);
        }

        public override bool ValidatePiece(IPieceRequester peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            var r = requests.SingleOrDefault (t => t.Block.PieceIndex == pieceIndex && t.Block.StartOffset == startOffset && t.Block.RequestLength == length && t.Peer == peer);
            if (r == null) {
                piece = null;
                return false;
            }

            piece = pieces.Single (p => p.Index == r.Block.PieceIndex);
            if (piece == null)
                return false;

            // All the other requests for this block need to be cancelled.
            CancelWhere(delegate(Request req) {
                return req.Block.PieceIndex == pieceIndex &&
                        req.Block.StartOffset == startOffset &&
                        req.Block.RequestLength == length &&
                        req.Peer != peer;
            }, true);

            // Mark the block as received
            piece.Blocks[startOffset / Piece.BlockSize].Received = true;

            requests.Remove(r);
            peer.AmRequestingPiecesCount--;

            // Once a piece is completely received, remove it from our list.
            // If a piece *fails* the hashcheck, we need to add it back into the list so
            // we download it again.
            if (piece.AllBlocksReceived) {
                pieces.Remove(piece);
                CancelWhere (r => r.Block.PieceIndex == pieceIndex, false);
            }

            return true;
        }
    }
}
