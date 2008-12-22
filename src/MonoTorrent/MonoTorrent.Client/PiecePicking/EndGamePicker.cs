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
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    // Keep a list of all the pieces which have not yet being fully downloaded
    // From this list we will make requests for all the blocks until the piece is complete.
    public class EndGamePicker : PiecePicker
    {
        static Predicate<Request> TimedOut = delegate(Request r) { return r.Block.RequestTimedOut; };
        static Predicate<Request> NotRequested = delegate(Request r) { return r.Block.RequestedOff == null; };

        // Struct to link a request for a block to a peer
        // This way we can have multiple requests for the same block
        struct Request
        {
            public Request(PeerId peer, Block block)
            {
                Peer = peer;
                Block = block;
            }
            public Block Block;
            public PeerId Peer;
        }

        // This list stores all the pieces which have not yet been completed. If a piece is *not* in this list
        // we don't need to download it.
        List<Piece> pieces;

        // These are all the requests for the individual blocks
        List<Request> requests;

        public EndGamePicker()
            : base(null)
        {
            requests = new List<Request>();
        }

        // Cancels a pending request when the predicate returns 'true'
        void CancelWhere(Predicate<Request> predicate)
        {
            for (int i = 0; i < requests.Count; i++)
                if (predicate(requests[i]))
                    requests[i].Block.CancelRequest();

            requests.RemoveAll(NotRequested);
        }

        public override void CancelTimedOutRequests()
        {
            CancelWhere(TimedOut);
        }

        public override int CurrentRequestCount()
        {
            return requests.Count;
        }

        public override List<Piece> ExportActiveRequests()
        {
            return new List<Piece>(pieces);
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            // 'Requests' should contain a list of all the pieces we need to complete
            pieces = new List<Piece>(requests);
            foreach (Piece piece in pieces)
            {
                for (int i = 0; i < piece.BlockCount; i++)
                    if (piece.Blocks[i].RequestedOff != null)
                        this.requests.Add(new Request(piece.Blocks[i].RequestedOff, piece.Blocks[i]));
            }
        }

        public override bool IsInteresting(BitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            // 1) See if there are any blocks which have not been requested. Request the block if the peer has it
            foreach (Piece p in pieces)
            {
                if(!peerBitfield[p.Index])
                    continue;

                for (int i = 0; i < p.BlockCount; i++)
                {
                    if (p.Blocks[i].Requested)
                        continue;
                    Request request = new Request(id, p.Blocks[i]);
                    requests.Add(request);
                    return new MessageBundle(request.Block.CreateRequest(id));
                }
            }

            // 2) For each block with an existing request, add another request. We do a search from the start
            //    of the list to the end. So when we add a duplicate request, move both requests to the end of the list
            for (int i = 0; i < requests.Count; i++)
            {
                if (!peerBitfield[requests[i].Block.PieceIndex] || requests[i].Peer == id)
                    continue;
                Request r = new Request(id, requests[i].Block);
                requests.Add(requests[0]);
                requests.RemoveAt(0);
                requests.Add(r);
                return new MessageBundle(r.Block.CreateRequest(id));
            }

            return null;
        }

        public override void Reset()
        {
            // Though if you reset an EndGamePicker it really means that you should be using a regular picker now
            requests.Clear();
        }

        public override void CancelRequest(PeerId peer, int piece, int startOffset, int length)
        {
            CancelWhere(delegate (Request r) {
                return r.Block.PieceIndex == piece &&
                       r.Block.StartOffset == startOffset &&
                       r.Block.RequestLength == length &&
                       peer.Equals(r.Peer);
            });
        }

        public override void CancelRequests(PeerId peer)
        {
            CancelWhere(delegate(Request r) { return r.Peer == peer; });
        }

        public override bool ValidatePiece(PeerId peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            foreach (Request r in requests)
            {
                // When we get past this block, it means we've found a valid request for this piece
                if (r.Block.PieceIndex != pieceIndex || r.Block.StartOffset != startOffset || r.Block.RequestLength != length || r.Peer != peer)
                    continue;

                // All the other requests for this block need to be cancelled. NOTE: Currently
                // we don't send a Cancel message. We need to.
                foreach (Piece p in pieces)
                {
                    if (p.Index != pieceIndex)
                        continue;

                    CancelWhere(delegate(Request req) {
                        return req.Block.PieceIndex == pieceIndex &&
                               req.Block.StartOffset == startOffset &&
                               r.Block.RequestLength == length &&
                               r.Peer != peer;
                    });

                    // Mark the block as received
                    p.Blocks[startOffset / Piece.BlockSize].Received = true;

                    // Once a piece is completely received, remove it from our list.
                    // If a piece *fails* the hashcheck, we need to add it back into the list so
                    // we download it again.
                    if (p.AllBlocksReceived)
                        pieces.Remove(p);

                    piece = p;
                    return true;
                }
            }

            // The request was not valid
            piece = null;
            return false;
        }
    }
}
