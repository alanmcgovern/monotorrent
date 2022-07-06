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
    public partial class PeerId : IRequester
    {
        int IRequester.AmRequestingPiecesCount { get => AmRequestingPiecesCount; set => AmRequestingPiecesCount = value; }
        bool IRequester.CanRequestMorePieces {
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

        long IRequester.DownloadSpeed => Monitor.DownloadRate;
        List<int> IRequester.IsAllowedFastPieces => IsAllowedFastPieces;
        bool IRequester.IsChoking => IsChoking;
        int IRequester.RepeatedHashFails => Peer.RepeatedHashFails;
        List<int> IRequester.SuggestedPieces => SuggestedPieces;
        bool IRequester.CanCancelRequests => SupportsFastPeer;
        int IRequester.MaxPendingRequests => MaxPendingRequests;

        int IRequester.PreferredRequestAmount (int pieceLength)
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
