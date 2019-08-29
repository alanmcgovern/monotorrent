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

using MonoTorrent.Client.PiecePicking;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class RandomisedPickerTests
    {
        PeerId id;
        RandomisedPicker picker;
        TestRig rig;
        TestPicker tester;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile();
            id = new PeerId(new Peer(new string('a', 20), new Uri("ipv4://BLAH")), rig.Manager, NullConnection.Incoming);
            for (int i = 0; i < id.BitField.Length; i += 2)
                id.BitField[i] = true;
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            tester = new TestPicker();
            picker = new RandomisedPicker(tester);
        }

        [Test]
        public void EnsureRandomlyPicked()
        {
            tester.ReturnNoPiece = false;
            while (picker.PickPiece(id, id.BitField, new List<PeerId>(), 1) != null) { }
            Assert.AreEqual(rig.Torrent.Pieces.Count, tester.PickedPieces.Count, "#1");
            List<int> pieces = new List<int>(tester.PickedPieces);
            pieces.Sort();
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != tester.PickedPieces[i])
                    return;
            Assert.Fail("The piece were picked in order");
        }
    }
}
