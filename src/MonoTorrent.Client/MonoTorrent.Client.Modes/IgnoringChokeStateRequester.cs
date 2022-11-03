//
// IgnoringChokeStateRequester.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
// Copyright (C) 2022 Alan McGovern
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


using System.Collections.Generic;

using MonoTorrent.PiecePicking;

namespace MonoTorrent.Client.Modes
{
    class IgnoringChokeStateRequester : IRequester
    {
        IRequester Requester { get; }

        // Hardcode this to 'false' so we can request anything from the peer.
        // This is useful to request metadata and piecehashes from a peer.
        public bool IsChoking => false;

        // Everything else defers to the actual IRequester for the true data.
        public int AmRequestingPiecesCount {
            get => Requester.AmRequestingPiecesCount;
            set => Requester.AmRequestingPiecesCount = value;
        }
        public bool CanCancelRequests => Requester.CanCancelRequests;
        public bool CanRequestMorePieces => Requester.CanRequestMorePieces;
        public long DownloadSpeed => Requester.DownloadSpeed;
        public List<int> IsAllowedFastPieces => Requester.IsAllowedFastPieces;
        public int MaxPendingRequests => Requester.MaxPendingRequests;
        public int RepeatedHashFails => Requester.RepeatedHashFails;
        public List<int> SuggestedPieces => Requester.SuggestedPieces;
        public bool SupportsFastPeer => Requester.SupportsFastPeer;

        public IgnoringChokeStateRequester (IRequester requester)
            => (Requester) = (requester);

        public int PreferredRequestAmount (int pieceLength)
            => Requester.PreferredRequestAmount (pieceLength);
    }
}
