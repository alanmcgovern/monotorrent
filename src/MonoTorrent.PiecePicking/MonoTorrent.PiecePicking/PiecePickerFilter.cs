//
// PiecePickerFilter.cs
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

namespace MonoTorrent.PiecePicking
{
    public abstract class PiecePickerFilter : IPiecePicker
    {
        protected IPiecePicker Next { get; }

        protected PiecePickerFilter (IPiecePicker picker)
            => Next = picker;

        public int CancelRequests (IRequester peer, int startIndex, int endIndex, Span<PieceSegment> cancellations)
            => Next.CancelRequests (peer, startIndex, endIndex, cancellations);

        public bool ContinueAnyExistingRequest (IRequester peer, ReadOnlyBitField available, int startIndex, int endIndex, int maxDuplicateRequests, out PieceSegment segment)
            => Next.ContinueAnyExistingRequest (peer, available, startIndex, endIndex, maxDuplicateRequests, out segment);

        public bool ContinueExistingRequest (IRequester peer, int startIndex, int endIndex, out PieceSegment segment)
            => Next.ContinueExistingRequest (peer, startIndex, endIndex, out segment);

        public int CurrentReceivedCount ()
            => Next.CurrentReceivedCount ();

        public int CurrentRequestCount ()
            => Next.CurrentRequestCount ();

        public IList<ActivePieceRequest> ExportActiveRequests ()
            => Next.ExportActiveRequests ();

        public virtual void Initialise (IPieceRequesterData torrentData)
            => Next.Initialise (torrentData);

        public virtual bool IsInteresting (IRequester peer, ReadOnlyBitField bitfield)
            => Next.IsInteresting (peer, bitfield);

        public virtual int PickPiece (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherPeers, int startIndex, int endIndex, Span<PieceSegment> requests)
            => Next.PickPiece (peer, available, otherPeers, startIndex, endIndex, requests);

        public void RequestRejected (IRequester peer, PieceSegment request)
            => Next.RequestRejected (peer, request);

        public bool ValidatePiece (IRequester peer, PieceSegment request, out bool pieceComplete, HashSet<IRequester> peersInvolved)
            => Next.ValidatePiece (peer, request, out pieceComplete, peersInvolved);
    }
}
