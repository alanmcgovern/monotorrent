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
        TestRig rig;
        List<PeerId> peers;
        RarestFirstPicker rarest;
        TestPicker tester;


        [OneTimeSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile ();
        }

        [SetUp]
        public void Setup()
        {
            tester = new TestPicker();
            rarest = new RarestFirstPicker(tester);
            rarest.Initialise(rig.Manager.Bitfield, rig.Torrent, new List<Piece>());
            peers = new List<PeerId>();
            for (int i = 0; i < 5; i++)
                peers.Add(new PeerId(new Peer(new string((char)(i + 'a'), 20), new Uri("ipv4://aaa")), rig.Manager, NullConnection.Incoming));

            for (int i = 0; i < rig.Manager.Bitfield.Length; i++)
            {
                for (int j = 0; j < peers.Count; j++)
                    peers[j].BitField[i] = i % (j + 1) == 0;
            }
            peers[0].BitField.SetAll(true);
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [Test]
        public void RarestPieceTest()
        {
            rarest.PickPiece(peers[0], peers[0].BitField, peers);
            Assert.AreEqual(5, tester.PickPieceBitfield.Count, "#1");
            BitField bf = tester.PickPieceBitfield[0];
            int[] trueIndices = new int[] { 1, 7, 11, 13, 17, 19, 23, 29, 31, 37 };
            for (int i = 0; i < bf.Length; i++)
                if (Array.IndexOf<int>(trueIndices, i) > -1)
                    Assert.IsTrue(bf[i]);
                else
                    Assert.IsFalse(bf[i]);

            bf = tester.PickPieceBitfield[1];
            trueIndices = new int[] { 1, 5, 7, 11, 13, 17, 19, 23, 25, 29, 31, 35, 37 };
            for (int i = 0; i < bf.Length; i++)
                if (Array.IndexOf<int>(trueIndices, i) > -1)
                    Assert.IsTrue(bf[i]);
                else
                    Assert.IsFalse(bf[i]);
        }
    }
}
