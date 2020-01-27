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


using System.Collections.Generic;

namespace MonoTorrent.Client.PiecePicking
{
    public class IgnoringPicker : PiecePicker
    {
        readonly BitField Bitfield;
        readonly BitField Temp;

        public IgnoringPicker (BitField bitfield, PiecePicker picker)
            : base (picker)
        {
            Bitfield = bitfield;
            Temp = new BitField (bitfield.Length);
        }

        public override IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            // Invert 'bitfield' and AND it with the peers bitfield
            // Any pieces which are 'true' in the bitfield will not be downloaded
            if (Bitfield.AllFalse)
                return base.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);

            Temp.From (available).NAnd (Bitfield);
            if (Temp.AllFalse)
                return null;

            return base.PickPiece (peer, Temp, otherPeers, count, startIndex, endIndex);
        }

        public override bool IsInteresting (BitField bitfield)
        {
            Temp.From (bitfield).NAnd (Bitfield);
            return !Temp.AllFalse && base.IsInteresting (Temp);
        }
    }
}
