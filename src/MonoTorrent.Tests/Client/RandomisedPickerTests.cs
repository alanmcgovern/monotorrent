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
        TestPicker checker;
        PeerId peer;
        RandomisedPicker picker;
        int pieceCount;

        [SetUp]
        public void Setup ()
        {
            checker = new TestPicker ();
            picker = new RandomisedPicker (checker);

            pieceCount = 40;
            peer = PeerId.CreateNull (pieceCount);
            peer.BitField.SetAll (true);
        }

        [Test]
        public void Pick ()
        {
            picker.PickPiece (peer, peer.BitField, new List<PeerId> ());

            // We should pick a random midpoint and select a piece starting from there.
            // If that fails we should wrap around to 0 and scan from the beginning.
            Assert.IsTrue (checker.PickedIndex[0].Item1 > 0, "#1");
            Assert.IsTrue (checker.PickedIndex[0].Item2 == pieceCount, "#2");

            Assert.AreEqual (0, checker.PickedIndex[1].Item1, "#3");
            Assert.AreEqual (checker.PickedIndex[0].Item1, checker.PickedIndex[1].Item2, "#4");

            foreach (var bf in checker.PickPieceBitfield)
                Assert.IsTrue (bf.AllTrue, "#5");
        }

        [Test]
        public void SinglePieceBitfield ()
        {
            picker.PickPiece (peer, new BitField (1).SetAll (true), new List<PeerId> ());

            Assert.AreEqual (1, checker.PickPieceBitfield.Count, "#1");
            Assert.AreEqual (0, checker.PickedIndex[0].Item1, "#2");
            Assert.AreEqual (1, checker.PickedIndex[0].Item2, "#2");
        }

        [Test]
        public void SinglePieceRange ()
        {
            picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 1, 12, 13);

            Assert.AreEqual (1, checker.PickPieceBitfield.Count, "#1");
            Assert.AreEqual (12, checker.PickedIndex[0].Item1, "#2");
            Assert.AreEqual (13, checker.PickedIndex[0].Item2, "#3");
        }

        [Test]
        public void TwoPieceRange ()
        {
            picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 1, 12, 14);

            Assert.AreEqual (2, checker.PickPieceBitfield.Count, "#1");
            Assert.AreEqual (13, checker.PickedIndex[0].Item1, "#2");
            Assert.AreEqual (14, checker.PickedIndex[0].Item2, "#3");

            Assert.AreEqual (12, checker.PickedIndex[1].Item1, "#4");
            Assert.AreEqual (13, checker.PickedIndex[1].Item2, "#5");
        }
    }
}
