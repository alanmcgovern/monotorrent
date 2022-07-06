//
// IgnoringPicker.cs
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
    public class IgnoringPicker : PiecePickerFilter
    {
        readonly ReadOnlyBitField Bitfield;
        readonly BitField Temp;

        public static IPiecePicker Wrap (IPiecePicker picker, IEnumerable<ReadOnlyBitField> ignoringBitfields)
        {
            var result = picker;
            foreach (var bf in ignoringBitfields)
                result = new IgnoringPicker (bf, result);
            return result;
        }

        public IgnoringPicker (ReadOnlyBitField bitfield, IPiecePicker picker)
            : base (picker)
        {
            Bitfield = bitfield;
            Temp = new BitField (bitfield.Length);
        }

        public override bool IsInteresting (IRequester peer, ReadOnlyBitField bitfield)
            => !Temp.From (bitfield).NAnd (Bitfield).AllFalse
            && base.IsInteresting (peer, Temp);

        public override int PickPiece (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherPeers, int startIndex, int endIndex, Span<PieceSegment> requests)
        {
            // Invert 'bitfield' and AND it with the peers bitfield
            // Any pieces which are 'true' in the bitfield will not be downloaded
            if (Bitfield.AllFalse)
                return base.PickPiece (peer, available, otherPeers, startIndex, endIndex, requests);

            Temp.From (available).NAnd (Bitfield);
            if (Temp.AllFalse)
                return 0;

            return base.PickPiece (peer, Temp, otherPeers, startIndex, endIndex, requests);
        }
    }
}
