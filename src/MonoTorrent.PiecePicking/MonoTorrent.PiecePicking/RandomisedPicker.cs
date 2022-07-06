//
// RandomisedPicker.cs
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
    public class RandomisedPicker : PiecePickerFilter
    {
        Random Random { get; }

        public RandomisedPicker (IPiecePicker picker)
            : base (picker)
        {
            Random = new Random ();
        }

        public override int PickPiece (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherPeers, int startIndex, int endIndex, Span<PieceSegment> requests)
        {
            if (available.AllFalse)
                return 0;

            // If there's only one piece to choose then there isn't any midpoint.
            if (endIndex - startIndex < 2 || requests.Length > 1)
                return base.PickPiece (peer, available, otherPeers, startIndex, endIndex, requests);

            // If there are two or more pieces to choose, ensure we always start *at least* one
            // piece beyond the start index.
            int midpoint = Random.Next (startIndex + 1, endIndex);
            var requested = base.PickPiece (peer, available, otherPeers, midpoint, endIndex, requests);
            return requested > 0 ? requested : base.PickPiece (peer, available, otherPeers, startIndex, midpoint, requests);
        }
    }
}
