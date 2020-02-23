//
// TestPicker.cs
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

namespace MonoTorrent.Client.PiecePicking
{
    class TestPicker : PiecePicker
    {
        public List<BitField> IsInterestingBitfield = new List<BitField> ();
        public List<IPieceRequester> PickPieceId = new List<IPieceRequester> ();
        public List<BitField> PickPieceBitfield = new List<BitField> ();
        public List<IReadOnlyList<IPieceRequester>> PickPiecePeers = new List<IReadOnlyList<IPieceRequester>> ();
        public List<Tuple<int, int>> PickedIndex = new List<Tuple<int, int>> ();
        public List<int> PickPieceCount = new List<int> ();

        public List<int> PickedPieces = new List<int> ();

        public bool ReturnNoPiece = true;

        public bool HasCancelledRequests { get; set; }
        public List<IPieceRequester> CancelledRequestsFrom { get; } = new List<IPieceRequester> ();

        public TestPicker ()
            : base (null)
        {
        }

        public override void CancelRequests (IPieceRequester peer)
        {
            HasCancelledRequests = true;
            CancelledRequestsFrom.Add (peer);
        }

        public override void CancelRequest (IPieceRequester peer, int piece, int startOffset, int length)
        {
            HasCancelledRequests = true;
            CancelledRequestsFrom.Add (peer);
        }

        public override IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            PickPieceId.Add (peer);
            BitField clone = new BitField (available.Length);
            clone.Or (available);
            PickPieceBitfield.Add (clone);
            PickPiecePeers.Add (otherPeers);
            PickedIndex.Add (Tuple.Create (startIndex, endIndex));
            PickPieceCount.Add (count);

            for (int i = startIndex; i < endIndex; i++) {
                if (PickedPieces.Contains (i))
                    continue;
                PickedPieces.Add (i);
                if (ReturnNoPiece)
                    return null;
                else
                    return Array.Empty<PieceRequest> ();
            }
            return null;
        }

        public override void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<Piece> requests)
        {

        }

        public override bool IsInteresting (BitField bitfield)
        {
            IsInterestingBitfield.Add (bitfield);
            return !bitfield.AllFalse;
        }
    }
}
