//
// RandomisedPickerTests.cs
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
    public class RandomisedPickerTests
    {
        TestTorrentManagerInfo CreateOnePieceTorrentData ()
        {
            return TestTorrentManagerInfo.Create (
                size: 64 * 1024,
                pieceLength: 64 * 1024,
                files: TorrentFileInfo.Create (64 * 1024, 64 * 1024)
            );
        }

        TestTorrentManagerInfo CreateTestTorrentData ()
        {
            return TestTorrentManagerInfo.Create (
                size: 64 * 1024 * 40,
                pieceLength: 64 * 1024,
                files: TorrentFileInfo.Create (64 * 1024, 64 * 1024 * 40)
            );
        }

        PiecePickerFilterChecker checker;
        PeerId seeder;
        RandomisedPicker picker;

        [SetUp]
        public void Setup ()
        {
            checker = new PiecePickerFilterChecker (new StandardPicker ());
            picker = new RandomisedPicker (checker);
            seeder = PeerId.CreateNull (40, true, false, true);
            picker.Initialise (TestTorrentManagerInfo.Create (pieceLength: Constants.BlockSize * 2, size: Constants.BlockSize * 2 * 40));
        }

        [Test]
        public void Pick ()
        {
            // Pretend only the 1st piece is available.
            var onePiece = new BitField (seeder.BitField.Length).Set (0, true);
            var piece = picker.PickPiece (seeder, onePiece, Array.Empty<ReadOnlyBitField> ()).Value;

            // We should pick a random midpoint and select a piece starting from there.
            // If that fails we should wrap around to 0 and scan from the beginning.
            Assert.IsTrue (checker.Picks[0].startIndex > 0, "#1");
            Assert.IsTrue (checker.Picks[0].endIndex == seeder.BitField.Length - 1, "#2");

            Assert.AreEqual (0, checker.Picks[1].startIndex, "#3");
            Assert.AreEqual (checker.Picks[1].endIndex, checker.Picks[0].startIndex, "#4");

            foreach (var pick in checker.Picks)
                Assert.IsTrue (onePiece.SequenceEqual (pick.available), "#5");
        }

        [Test]
        public void SinglePieceBitfield ()
        {
            picker.Initialise (CreateOnePieceTorrentData ());
            picker.PickPiece (seeder, new BitField (1).SetAll (true), Array.Empty<ReadOnlyBitField> ());

            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.AreEqual (0, checker.Picks[0].startIndex, "#2");
            Assert.AreEqual (0, checker.Picks[0].endIndex, "#2");
        }

        [Test]
        public void SinglePieceRange ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            picker.PickPiece (seeder, seeder.BitField, Array.Empty<ReadOnlyBitField> (), 12, 13, buffer);

            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.AreEqual (12, checker.Picks[0].startIndex, "#2");
            Assert.AreEqual (13, checker.Picks[0].endIndex, "#3");
        }

        [Test]
        public void TwoPieceRange ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            var onePiece = new BitField (seeder.BitField.Length).Set (0, true);
            picker.PickPiece (seeder, onePiece, Array.Empty<ReadOnlyBitField> (), 12, 14, buffer);

            Assert.AreEqual (2, checker.Picks.Count, "#1");
            Assert.AreEqual (13, checker.Picks[0].startIndex, "#2");
            Assert.AreEqual (14, checker.Picks[0].endIndex, "#3");

            Assert.AreEqual (12, checker.Picks[1].startIndex, "#4");
            Assert.AreEqual (13, checker.Picks[1].endIndex, "#5");
        }
    }
}
