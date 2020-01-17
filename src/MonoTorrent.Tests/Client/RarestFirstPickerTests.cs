//
// RarestFirstPickerTests.cs
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

using NUnit.Framework;

namespace MonoTorrent.Client.PiecePicking
{
    [TestFixture]
    public class RarestFirstPickerTests
    {
        class TestTorrentData : ITorrentData
        {
            public int BlocksPerPiece => PieceLength / Piece.BlockSize;
            public TorrentFile[] Files { get; set; }
            public int PieceLength { get; set; }
            public int Pieces => (int) Math.Ceiling ((double) Size / PieceLength);
            public long Size { get; set; }
        }

        BitField bitfield;
        TestPicker checker;
        PeerId peer;
        List<PeerId> peers;
        RarestFirstPicker picker;
        TestTorrentData torrentData;

        [SetUp]
        public void Setup ()
        {
            int pieceLength = 16 * Piece.BlockSize;
            int pieces = 40;
            int size = pieces * pieceLength;

            bitfield = new BitField (pieces);
            torrentData = new TestTorrentData {
                Files = new[] { new TorrentFile ("Test", size) },
                PieceLength = pieceLength,
                Size = size
            };

            checker = new TestPicker ();
            picker = new RarestFirstPicker (checker);
            picker.Initialise (bitfield, torrentData, new List<Piece> ());

            peer = PeerId.CreateNull (pieces);
            peer.BitField.SetAll (true);

            peers = new List<PeerId> ();
            for (int i = 0; i < 5; i++)
                peers.Add (PeerId.CreateNull (pieces));
        }

        [Test]
        public void RarestPieceTest ()
        {
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < (i * 5) + 5; j++)
                    peers[i].BitField[j] = true;

            // No pieces should be selected, but we can check what was requested.
            picker.PickPiece (peer, peer.BitField, peers);
            Assert.AreEqual (6, checker.PickPieceBitfield.Count);

            // Two peers have piece 25
            Assert.AreEqual (25, checker.PickPieceBitfield[0].FirstTrue (), "#1");
            Assert.AreEqual (-1, checker.PickPieceBitfield[0].FirstFalse (25, torrentData.Pieces), "#2");

            // Three peers have piece 20
            Assert.AreEqual (20, checker.PickPieceBitfield[1].FirstTrue (), "#3");
            Assert.AreEqual (-1, checker.PickPieceBitfield[1].FirstFalse (20, torrentData.Pieces), "#4");

            // Three peers have piece 20
            Assert.AreEqual (15, checker.PickPieceBitfield[2].FirstTrue (), "#4");
            Assert.AreEqual (-1, checker.PickPieceBitfield[2].FirstFalse (15, torrentData.Pieces), "#6");

            // Three peers have piece 20
            Assert.AreEqual (10, checker.PickPieceBitfield[3].FirstTrue (), "#5");
            Assert.AreEqual (-1, checker.PickPieceBitfield[3].FirstFalse (10, torrentData.Pieces), "#8");
        }

        [Test]
        public void OnlyAvailablePiecesAllowed ()
        {
            // The bitfield representing the overall torrent shouldn't be used by the
            // rarest first picker, so set it all to true to make sure it has no impact.
            bitfield.SetAll (true);

            // Pretend the peer has 4 pieces we can choose.
            var available = new BitField (bitfield.Length)
                .Set (1, true)
                .Set (2, true)
                .Set (4, true)
                .Set (8, true);

            // Every other peer has all pieces except for piece '2'.
            for (int i = 0; i < 5; i++)
                peers[i].BitField.SetAll (true).Set (i, false);

            // Ensure that pieces which were not in the 'available' bitfield were not offered
            // as suggestions.
            foreach (var bf in checker.PickPieceBitfield)
                Assert.IsTrue (available.Clone ().Not ().And (bf).AllFalse, "#1");

            // Ensure at least one of the pieces in our bitfield *was* offered.
            foreach (var bf in checker.PickPieceBitfield)
                Assert.IsFalse (available.Clone ().And (bf).AllFalse, "#2");
        }
    }
}
