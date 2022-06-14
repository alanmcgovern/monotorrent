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
using System.Linq;

using MonoTorrent.Client;

using NUnit.Framework;

namespace MonoTorrent.PiecePicking
{
    [TestFixture]
    public class RarestFirstPickerTests
    {
        BitField bitfield;
        PiecePickerFilterChecker checker;
        PeerId peer;
        ReadOnlyBitField[] peers;
        RarestFirstPicker picker;
        IPieceRequesterData torrentData;

        [SetUp]
        public void Setup ()
        {
            int pieceLength = 16 * Constants.BlockSize;
            int pieces = 40;
            int size = pieces * pieceLength;

            bitfield = new BitField (pieces);
            torrentData = TestTorrentManagerInfo.Create (
                files: TorrentFileInfo.Create (pieceLength, ("Test", size, "Full/Path/Test")),
                pieceLength: pieceLength,
                size: size
            );

            checker = new PiecePickerFilterChecker ();
            picker = new RarestFirstPicker (checker);
            picker.Initialise (torrentData);

            peer = PeerId.CreateNull (pieces);
            peer.BitField.SetAll (true);

            peers = new ReadOnlyBitField[5];
            for (int i = 0; i < 5; i++)
                peers[i] = new BitField (pieces);
        }

        [Test]
        public void RarestPieceTest ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            for (int i = 0; i < 5; i++) {
                var bf = new BitField (peers[i]);
                for (int j = 0; j < (i * 5) + 5; j++)
                    bf[j] = true;
                peers[i] = bf;
            }

            // No pieces should be selected, but we can check what was requested.
            picker.PickPiece (peer, peer.BitField, peers, 0, peer.BitField.Length - 1, buffer);
            Assert.AreEqual (6, checker.Picks.Count);

            // Two peers have piece 25
            Assert.AreEqual (25, checker.Picks[0].available.FirstTrue (), "#1");
            Assert.AreEqual (-1, checker.Picks[0].available.FirstFalse (25, torrentData.PieceCount - 1), "#2");;

            // Three peers have piece 20
            Assert.AreEqual (20, checker.Picks[1].available.FirstTrue (), "#3");
            Assert.AreEqual (-1, checker.Picks[1].available.FirstFalse (20, torrentData.PieceCount - 1), "#4");

            // Three peers have piece 20
            Assert.AreEqual (15, checker.Picks[2].available.FirstTrue (), "#4");
            Assert.AreEqual (-1, checker.Picks[2].available.FirstFalse (15, torrentData.PieceCount - 1), "#6");

            // Three peers have piece 20
            Assert.AreEqual (10, checker.Picks[3].available.FirstTrue (), "#5");
            Assert.AreEqual (-1, checker.Picks[3].available.FirstFalse (10, torrentData.PieceCount - 1), "#8");
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
                peers[i] = new BitField (peers[i]).SetAll (true).Set (i, false);

            // Ensure that pieces which were not in the 'available' bitfield were not offered
            // as suggestions.
            foreach (var pick in checker.Picks)
                Assert.IsTrue (new BitField (available).Not ().And (pick.available).AllFalse, "#1");

            // Ensure at least one of the pieces in our bitfield *was* offered.
            foreach (var pick in checker.Picks)
                Assert.IsFalse (new BitField (available).And (pick.available).AllFalse, "#2");
        }
    }
}
