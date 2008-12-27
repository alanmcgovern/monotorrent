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
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    public class RarestFirstPicker : PiecePicker
    {
        TorrentFile[] files;
        Stack<BitField> rarest;
        Stack<BitField> spares;
        int length;

        BitField DequeueSpare()
        {
            return spares.Count > 0 ? spares.Pop() : new BitField(length);
        }

        public RarestFirstPicker(PiecePicker picker)
            : base(picker)
        {
            rarest = new Stack<BitField>();
            spares = new Stack<BitField>();
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            base.Initialise(bitfield, files, requests);
            this.files = files;
            this.length = bitfield.Length;
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            if (peerBitfield.AllFalse)
                return null;

            if (count > 1)
                return base.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);
            
            GenerateRarestFirst(peerBitfield, otherPeers);

            while (rarest.Count > 0)
            {
                BitField current = rarest.Pop();
                MessageBundle bundle = base.PickPiece(id, current, otherPeers, count, startIndex, endIndex);
                spares.Push(current);

                if (bundle != null)
                    return bundle;
            }

            return null;
        }

        void GenerateRarestFirst(BitField peerBitfield, List<PeerId> otherPeers)
        {
            // Move anything in the rarest buffer into the spares
            while (rarest.Count > 0)
                spares.Push(rarest.Pop());

            BitField current = DequeueSpare();
            current.From(peerBitfield);

            // Store this bitfield as the first iteration of the Rarest First algorithm.
            rarest.Push(current);

            // Get a cloned copy of the bitfield and begin iterating to find the rarest pieces
            for (int i = 0; i < otherPeers.Count; i++)
            {
                if (otherPeers[i].BitField.AllTrue)
                    continue;

                current = DequeueSpare().From(current);

                // currentBitfield = currentBitfield & (!otherBitfield)
                // This calculation finds the pieces this peer has that other peers *do not* have.
                // i.e. the rarest piece.
                current.NAnd(otherPeers[i].BitField);

                // If the bitfield now has no pieces we've completed our task
                if (current.AllFalse)
                {
                    spares.Push(current);
                    break;
                }

                // Otherwise push the bitfield on the stack and clone it and iterate again.
                rarest.Push(current);
            }
        }
    }
}