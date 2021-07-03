//
// RarestFirstPicker.cs
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

namespace MonoTorrent.PiecePicking
{
    public class RarestFirstPicker : PiecePickerFilter
    {
        readonly Stack<MutableBitField> rarest;
        readonly Stack<MutableBitField> spares;

        public RarestFirstPicker (IPiecePicker picker)
            : base (picker)
        {
            rarest = new Stack<MutableBitField> ();
            spares = new Stack<MutableBitField> ();
        }

        public override void Initialise (ITorrentData torrentData)
        {
            base.Initialise (torrentData);
            rarest.Clear ();
            spares.Clear ();
        }

        public override IList<BlockInfo> PickPiece (IPeer peer, BitField available, IReadOnlyList<IPeer> otherPeers, int count, int startIndex, int endIndex)
        {
            if (available.AllFalse)
                return null;

            if (count > 1)
                return base.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);

            GenerateRarestFirst (available, otherPeers);

            while (rarest.Count > 0) {
                MutableBitField current = rarest.Pop ();
                IList<BlockInfo> bundle = base.PickPiece (peer, current, otherPeers, count, startIndex, endIndex);
                spares.Push (current);

                if (bundle != null)
                    return bundle;
            }

            return null;
        }

        void GenerateRarestFirst (BitField peerBitfield, IReadOnlyList<IPeer> otherPeers)
        {
            // Move anything in the rarest buffer into the spares
            while (rarest.Count > 0)
                spares.Push (rarest.Pop ());

            MutableBitField current = (spares.Count > 0 ? spares.Pop () : new MutableBitField (peerBitfield.Length)).From (peerBitfield);

            // Store this bitfield as the first iteration of the Rarest First algorithm.
            rarest.Push (current);

            // Get a cloned copy of the bitfield and begin iterating to find the rarest pieces
            for (int i = 0; i < otherPeers.Count; i++) {
                if (otherPeers[i].BitField.AllTrue)
                    continue;

                current = (spares.Count > 0 ? spares.Pop () : new MutableBitField (current.Length)).From (current);

                // currentBitfield = currentBitfield & (!otherBitfield)
                // This calculation finds the pieces this peer has that other peers *do not* have.
                // i.e. the rarest piece.
                current.NAnd (otherPeers[i].BitField);

                // If the bitfield now has no pieces we've completed our task
                if (current.AllFalse) {
                    spares.Push (current);
                    break;
                }

                // Otherwise push the bitfield on the stack and clone it and iterate again.
                rarest.Push (current);
            }
        }
    }
}