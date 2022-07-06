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


using System;
using System.Collections.Generic;

namespace MonoTorrent.PiecePicking
{
    public class RarestFirstPicker : PiecePickerFilter
    {
        readonly Stack<BitField> rarest;
        readonly Stack<BitField> spares;

        public RarestFirstPicker (IPiecePicker picker)
            : base (picker)
        {
            rarest = new Stack<BitField> ();
            spares = new Stack<BitField> ();
        }

        public override void Initialise (IPieceRequesterData torrentData)
        {
            base.Initialise (torrentData);
            rarest.Clear ();
            spares.Clear ();
        }

        public override int PickPiece (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherPeers, int startIndex, int endIndex, Span<PieceSegment> requests)
        {
            if (available.AllFalse)
                return 0;

            if (requests.Length > 1)
                return base.PickPiece (peer, available, otherPeers, startIndex, endIndex, requests);

            GenerateRarestFirst (available, otherPeers);

            while (rarest.Count > 0) {
                BitField current = rarest.Pop ();
                int requested = base.PickPiece (peer, current, otherPeers, startIndex, endIndex, requests);
                spares.Push (current);

                if (requested > 0)
                    return requested;
            }

            return 0;
        }

        void GenerateRarestFirst (ReadOnlyBitField peerBitfield, ReadOnlySpan<ReadOnlyBitField> otherPeers)
        {
            // Move anything in the rarest buffer into the spares
            while (rarest.Count > 0)
                spares.Push (rarest.Pop ());

            BitField current = (spares.Count > 0 ? spares.Pop () : new BitField (peerBitfield.Length)).From (peerBitfield);

            // Store this bitfield as the first iteration of the Rarest First algorithm.
            rarest.Push (current);

            // Get a cloned copy of the bitfield and begin iterating to find the rarest pieces
            for (int i = 0; i < otherPeers.Length; i++) {
                if (otherPeers[i].AllTrue)
                    continue;

                current = (spares.Count > 0 ? spares.Pop () : new BitField (current.Length)).From (current);

                // currentBitfield = currentBitfield & (!otherBitfield)
                // This calculation finds the pieces this peer has that other peers *do not* have.
                // i.e. the rarest piece.
                current.NAnd (otherPeers[i]);

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
