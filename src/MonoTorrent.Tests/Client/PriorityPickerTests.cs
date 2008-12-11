//
// PriorityPickerTests.cs
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
using MonoTorrent.Client.PiecePicking;
using NUnit.Framework;
using MonoTorrent.Common;
using MonoTorrent.Client;

namespace MonoTorrent.Tests.Client
{
    [TestFixture]
    public class PriorityPickerTests
    {
        //static void Main()
        //{
        //    PriorityPickerTests t = new PriorityPickerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.PriorityMix();
        //}

        PeerId id;
        PriorityPicker picker;
        TestRig rig;
        TestPicker tester;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            rig = new TestRig("");
            id = new PeerId(new Peer(new string('a', 20), new Uri("tcp://BLAH")), rig.Manager);
            id.BitField.SetAll(true);
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            tester = new TestPicker();
            picker = new PriorityPicker(tester);
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>(), new BitField(rig.Pieces));
            foreach (TorrentFile file in rig.Torrent.Files)
                file.Priority = Priority.Normal;
        }

        [Test]
        public void AllAllowed()
        {
            picker.PickPiece(id, id.BitField, new List<PeerId>(), 0, rig.Pieces, 1);
            Assert.AreEqual(1, tester.PickPieceBitfield.Count, "#1");
            Assert.IsTrue(tester.PickPieceBitfield[0].AllTrue, "#2");
        }

        [Test]
        public void HighPriority()
        {
            rig.Torrent.Files[0].Priority = Priority.High;
            rig.Torrent.Files[1].Priority = Priority.High;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 0, rig.Pieces, 1);
            Assert.AreEqual(2, tester.PickPieceBitfield.Count, "#1");
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i <= rig.Torrent.Files[1].EndPieceIndex)
                    Assert.IsTrue(tester.PickPieceBitfield[0][i], "#2");
                else
                    Assert.IsFalse(tester.PickPieceBitfield[0][i], "#2");
            }

            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i < rig.Torrent.Files[1].EndPieceIndex)
                    Assert.IsFalse(tester.PickPieceBitfield[1][i], "#2");
                else
                    Assert.IsTrue(tester.PickPieceBitfield[1][i], "#2");
            }
        }

        [Test]
        public void DoNotDownload()
        {
            rig.Torrent.Files[0].Priority = Priority.DoNotDownload;
            rig.Torrent.Files[1].Priority = Priority.DoNotDownload;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 0, rig.Pieces, 1);
            Assert.AreEqual(2, tester.PickPieceBitfield.Count, "#1");
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i < rig.Torrent.Files[1].EndPieceIndex)
                    Assert.IsFalse(tester.PickPieceBitfield[0][i], "#2");
                else
                    Assert.IsTrue(tester.PickPieceBitfield[0][i], "#2");
            }
        }

        [Test]
        public void PriorityMix()
        {
            BitField bf;
            TorrentFile file;

            rig.Torrent.Files[0].Priority = Priority.Immediate;
            rig.Torrent.Files[1].Priority = Priority.Low;
            rig.Torrent.Files[2].Priority = Priority.DoNotDownload;
            rig.Torrent.Files[3].Priority = Priority.High;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 0, rig.Pieces, 1);

            Assert.AreEqual(4, tester.PickPieceBitfield.Count, "#1");

            bf = tester.PickPieceBitfield[0];
            file = rig.Torrent.Files[0];
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i >= file.StartPieceIndex && i <= file.EndPieceIndex)
                    Assert.IsTrue(bf[i]);
                else
                    Assert.IsFalse(bf[i]);
            }

            bf = tester.PickPieceBitfield[1];
            file = rig.Torrent.Files[3];
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i >= file.StartPieceIndex && i <= file.EndPieceIndex)
                    Assert.IsTrue(bf[i]);
                else
                    Assert.IsFalse(bf[i]);
            }

            bf = tester.PickPieceBitfield[2];
            file = rig.Torrent.Files[1];
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i >= file.StartPieceIndex && i <= file.EndPieceIndex)
                    Assert.IsTrue(bf[i]);
                else
                    Assert.IsFalse(bf[i]);
            }

            Assert.IsTrue(tester.PickPieceBitfield[3].AllFalse);
        }
    }
}
