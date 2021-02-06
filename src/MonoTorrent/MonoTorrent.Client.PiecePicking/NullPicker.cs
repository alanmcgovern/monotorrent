//
// NullPicker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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

namespace MonoTorrent.Client.PiecePicking
{
    class NullPicker : IPiecePicker
    {
        public int AbortRequests (IPeer peer)
        {
            return 0;
        }

        public IList<PieceRequest> CancelRequests (IPeer peer, int startIndex, int endIndex)
        {
            return Array.Empty<PieceRequest> ();
        }

        public PieceRequest? ContinueAnyExistingRequest (IPeer peer, int startIndex, int endIndex, int maxDuplicateRequests)
        {
            return null;
        }

        public PieceRequest? ContinueExistingRequest (IPeer peer, int startIndex, int endIndex)
        {
            return null;
        }

        public int CurrentReceivedCount ()
        {
            return 0;
        }

        public int CurrentRequestCount ()
        {
            return 0;
        }

        public IList<ActivePieceRequest> ExportActiveRequests ()
        {
            return Array.Empty<ActivePieceRequest> ();
        }

        public void Initialise (ITorrentData torrentData)
        {
        }

        public bool IsInteresting (IPeer peer, BitField bitfield)
        {
            return false;
        }

        public IList<PieceRequest> PickPiece (IPeer peer, BitField available, IReadOnlyList<IPeer> otherPeers, int count, int startIndex, int endIndex)
        {
            return Array.Empty<PieceRequest> ();
        }

        public void RequestRejected (IPeer peer, PieceRequest rejectedRequest)
        {
        }

        public bool ValidatePiece (IPeer peer, PieceRequest request, out bool pieceComplete, out IList<IPeer> peersInvolved)
        {
            pieceComplete = false;
            peersInvolved = null;
            return false;
        }
    }
}
