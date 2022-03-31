//
// PeerId.PieceRequester.cs
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

using MonoTorrent.Connections.Peer;
using MonoTorrent.Messages.Peer;
using MonoTorrent.PiecePicking;

namespace MonoTorrent.Client
{
    public partial class PeerId : IPeerWithMessaging
    {
        int IPeer.AmRequestingPiecesCount { get => AmRequestingPiecesCount; set => AmRequestingPiecesCount = value; }
        bool IPeer.CanRequestMorePieces {
            get {
                if (Connection is HttpPeerConnection) {
                    return AmRequestingPiecesCount == 0;
                } else {
                    if (MaxPendingRequests < 20)
                        return AmRequestingPiecesCount < MaxPendingRequests;
                    return (AmRequestingPiecesCount / (float) MaxPendingRequests) < 0.75;
                }
            }
        }

        long IPeer.DownloadSpeed => Monitor.DownloadRate;
        List<int> IPeer.IsAllowedFastPieces => IsAllowedFastPieces;
        bool IPeer.IsChoking => IsChoking;
        int IPeer.RepeatedHashFails => Peer.RepeatedHashFails;
        List<int> IPeer.SuggestedPieces => SuggestedPieces;
        bool IPeer.CanCancelRequests => SupportsFastPeer;
        int IPeer.TotalHashFails => Peer.TotalHashFails;
        int IPeer.MaxPendingRequests => MaxPendingRequests;

        void IPeerWithMessaging.EnqueueRequest (BlockInfo request)
        {
            Span<BlockInfo> buffer = stackalloc BlockInfo[1];
            buffer[0] = request;
            ((IPeerWithMessaging) this).EnqueueRequests (buffer);
        }

        void IPeerWithMessaging.EnqueueRequests (Span<BlockInfo> requests)
        {

            (var bundle, var releaser) = PeerMessage.Rent<RequestBundle> ();
            bundle.Initialize (requests);
            MessageQueue.Enqueue (bundle, releaser);
        }

        void IPeerWithMessaging.EnqueueCancellation (BlockInfo request)
        {
            (var msg, var releaser) = PeerMessage.Rent<CancelMessage> ();
            msg.Initialize (request.PieceIndex, request.StartOffset, request.RequestLength);
            MessageQueue.Enqueue (msg, releaser);
        }

        void IPeerWithMessaging.EnqueueCancellations (IList<BlockInfo> requests)
        {
            for (int i = 0; i < requests.Count; i++)
                MessageQueue.Enqueue (new CancelMessage (requests[i].PieceIndex, requests[i].StartOffset, requests[i].RequestLength), default);
        }

        int IPeer.PreferredRequestAmount (int pieceLength)
        {
            if (Connection is HttpPeerConnection) {
                // How many whole pieces fit into 2MB
                var count = (2 * 1024 * 1024) / pieceLength;

                // Make sure we have at least one whole piece
                count = Math.Max (count, 1);

                return count * (pieceLength / Constants.BlockSize);
            } else {
                return 1;
            }
        }
    }
}
