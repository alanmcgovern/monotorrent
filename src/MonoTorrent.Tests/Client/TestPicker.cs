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
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    class TestPicker : PiecePicker
    {
        public List<PeerId> PickPieceId = new List<PeerId>();
        public List<BitField> PickPieceBitfield = new List<BitField>();
        public List<List<PeerId>> PickPiecePeers = new List<List<PeerId>>();
        public List<int> PickPieceStartIndex = new List<int>();
        public List<int> PickPieceEndIndex = new List<int>();
        public List<int> PickPieceCount = new List<int>();

        public List<int> PickedPieces = new List<int>();

        public bool ReturnNoPiece = true;
        public TestPicker()
            : base(null)
        {
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            PickPieceId.Add(id);
            BitField clone = new BitField(peerBitfield.Length);
            clone.Or(peerBitfield);
            PickPieceBitfield.Add(clone);
            PickPiecePeers.Add(otherPeers);
            PickPieceStartIndex.Add(startIndex);
            PickPieceEndIndex.Add(endIndex);
            PickPieceCount.Add(count);

            for (int i = startIndex; i < endIndex; i++)
            {
                if (PickedPieces.Contains(i))
                    continue;
                PickedPieces.Add(i);
                if (ReturnNoPiece)
                    return null;
                else
                    return new MessageBundle();
            }
            return null;
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            
        }
    }
}
