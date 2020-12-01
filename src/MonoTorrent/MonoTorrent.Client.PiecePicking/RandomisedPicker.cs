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

namespace MonoTorrent.Client.PiecePicking
{
    public class RandomisedPicker : PiecePicker
    {
        readonly Random random = new Random ();

        public RandomisedPicker (PiecePicker picker)
            : base (picker)
        {

        }

        public override IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            if (available.AllFalse)
                return null;

            // If there's only one piece to choose then there isn't any midpoint.
            if (endIndex - startIndex < 2)
                return base.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);

            // If there are two or more pieces to choose, ensure we always start *at least* one
            // piece beyond the start index.
            int midpoint = random.Next (startIndex + 1, endIndex);
            return base.PickPiece (peer, available, otherPeers, count, midpoint, endIndex) ??
                   base.PickPiece (peer, available, otherPeers, count, startIndex, midpoint);
        }
    }
}
