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


using System.Collections.Generic;

using NUnit.Framework;

namespace MonoTorrent.Client.PiecePicking
{
    [TestFixture]
    public class RandomisedPickerTests
    {
        class TestTorrentData : ITorrentData
        {
            public IList<ITorrentFileInfo> Files { get; } = TorrentFileInfo.Create (64 * 1024, 64 * 1024 * 40);
            public int PieceLength { get; } = 64 * 1024;
            public long Size { get; } = 64 * 1024 * 40;
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
            picker.Initialise (new TestTorrentData ());
        }

        [Test]
        public void Pick ()
        {
            // Pretend only the 1st piece is available.
            var onePiece = new MutableBitField (seeder.BitField.Length).Set (0, true);
            var piece = picker.PickPiece (seeder, onePiece, new List<PeerId> ()).Value;

            // We should pick a random midpoint and select a piece starting from there.
            // If that fails we should wrap around to 0 and scan from the beginning.
            Assert.IsTrue (checker.Picks[0].startIndex > 0, "#1");
            Assert.IsTrue (checker.Picks[0].endIndex == seeder.BitField.Length - 1, "#2");

            Assert.AreEqual (0, checker.Picks[1].startIndex, "#3");
            Assert.AreEqual (checker.Picks[1].endIndex, checker.Picks[0].startIndex, "#4");

            foreach (var pick in checker.Picks)
                Assert.AreEqual (onePiece, pick.available, "#5");
        }

        [Test]
        public void SinglePieceBitfield ()
        {
            picker.PickPiece (seeder, new MutableBitField (1).SetAll (true), new List<PeerId> ());

            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.AreEqual (0, checker.Picks[0].startIndex, "#2");
            Assert.AreEqual (0, checker.Picks[0].endIndex, "#2");
        }

        [Test]
        public void SinglePieceRange ()
        {
            picker.PickPiece (seeder, seeder.BitField, new List<PeerId> (), 1, 12, 13);

            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.AreEqual (12, checker.Picks[0].startIndex, "#2");
            Assert.AreEqual (13, checker.Picks[0].endIndex, "#3");
        }

        [Test]
        public void TwoPieceRange ()
        {
            var onePiece = new MutableBitField (seeder.BitField.Length).Set (0, true);
            picker.PickPiece (seeder, onePiece, new List<PeerId> (), 1, 12, 14);

            Assert.AreEqual (2, checker.Picks.Count, "#1");
            Assert.AreEqual (13, checker.Picks[0].startIndex, "#2");
            Assert.AreEqual (14, checker.Picks[0].endIndex, "#3");

            Assert.AreEqual (12, checker.Picks[1].startIndex, "#4");
            Assert.AreEqual (13, checker.Picks[1].endIndex, "#5");
        }
    }
}
