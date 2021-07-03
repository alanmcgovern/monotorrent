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
using System.Linq;

using NUnit.Framework;

namespace MonoTorrent.Client.PiecePicking
{
    [TestFixture]
    public class PriorityPickerTests
    {
        class TestTorrentData : ITorrentData
        {
            IList<ITorrentFileInfo> ITorrentData.Files => new List<ITorrentFileInfo> (Files);

            public IList<TorrentFileInfo> Files { get; set; }
            public int PieceLength { get; set; }
            public int Pieces => (int) Math.Ceiling ((double) Size / PieceLength);
            public long Size { get; set; }

            public void SetAll (Priority priority)
            {
                foreach (TorrentFileInfo file in Files)
                    file.Priority = priority;
            }
        }

        PiecePickerFilterChecker checker;
        List<PeerId> peers;
        PriorityPicker picker;

        MutableBitField singleBitfield;
        TestTorrentData singleFile;
        PeerId singlePeer;

        MutableBitField multiBitfield;
        TestTorrentData multiFile;
        PeerId multiPeer;

        [SetUp]
        public void Setup ()
        {
            singleFile = CreateSingleFile ();
            singleBitfield = new MutableBitField (singleFile.PieceCount ()).SetAll (true);
            singlePeer = PeerId.CreateNull (singleBitfield.Length);

            multiFile = CreateMultiFile ();
            multiBitfield = new MutableBitField (multiFile.PieceCount ()).SetAll (true);
            multiPeer = PeerId.CreateNull (multiBitfield.Length);

            checker = new PiecePickerFilterChecker ();
            picker = new PriorityPicker (checker);
            peers = new List<PeerId> ();
        }

        static TestTorrentData CreateSingleFile ()
        {
            int pieceLength = Piece.BlockSize * 16;
            var size = pieceLength * 32 + 123;
            var files = TorrentFileInfo.Create (pieceLength, ("Single", size, "full/path/Single"));
            return new TestTorrentData {
                Files = files,
                PieceLength = pieceLength,
                Size = files.Single ().Length
            };
        }

        static TestTorrentData CreateMultiFile ()
        {
            int pieceLength = Piece.BlockSize * 16;

            long[] sizes = {
                pieceLength * 10,
                pieceLength * 7  + 123,
                pieceLength * 32 + 123,
                pieceLength * 16 + 543,
                pieceLength * 19 + 591,
                pieceLength * 22 + 591,
                pieceLength * 0  + 12, // 12 byte file!
                pieceLength * 7,
            };

            var files = TorrentFileInfo.Create (pieceLength, sizes);
            return new TestTorrentData {
                Files = files,
                PieceLength = pieceLength,
                Size = files.Sum (t => t.Length)
            };
        }

        [Test]
        public void MultiFile ()
        {
            picker.Initialise (multiFile);

            picker.PickPiece (multiPeer, multiBitfield, peers, 1, 0, multiBitfield.Length - 1);
            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.IsTrue (checker.Picks[0].available.AllTrue, "#2");
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#3");
        }

        [Test]
        public void MultiFile_ChangingPriority ()
        {
            multiFile.SetAll (Priority.DoNotDownload);
            picker.Initialise (multiFile);

            // Every time the priority is not 'DoNotDownload' and we try to pick a piece,
            // we should get a new bitfield.
            for (int i = 0; i < 10; i++) {
                multiFile.Files[2].Priority = i % 2 == 0 ? Priority.DoNotDownload : Priority.Normal;
                picker.PickPiece (multiPeer, multiBitfield, peers, 1, 0, multiBitfield.Length - 1);
                Assert.AreEqual ((i / 2) + (i % 2), checker.Picks.Count, "#1." + i);
            }
        }

        [Test]
        public void MultiFile_CheckInteresting ()
        {
            multiFile.SetAll (Priority.DoNotDownload);
            picker.Initialise (multiFile);

            Assert.IsFalse (picker.IsInteresting (multiPeer, multiBitfield), "#0");
            multiFile.Files[4].Priority = Priority.Lowest;
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#1");

            multiFile.Files[4].Priority = Priority.DoNotDownload;
            Assert.IsFalse (picker.IsInteresting (multiPeer, multiBitfield), "#2");

            multiFile.Files[4].Priority = Priority.High;
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#2");
        }

        [Test]
        public void MultiFile_DoNotDownload ()
        {
            multiFile.SetAll (Priority.DoNotDownload);
            picker.Initialise (multiFile);

            picker.PickPiece (multiPeer, multiBitfield, peers, 1, 0, multiBitfield.Length - 1);
            Assert.AreEqual (0, checker.Picks.Count, "#1");
            Assert.IsFalse (picker.IsInteresting (multiPeer, multiBitfield), "#2");

            multiFile.SetAll (Priority.Immediate);
            picker.PickPiece (multiPeer, multiBitfield, peers, 1, 0, multiBitfield.Length - 1);
            Assert.AreEqual (1, checker.Picks.Count, "#3");
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#4");
        }

        [Test]
        public void MultiFile_EveryPriority ()
        {
            picker.Initialise (multiFile);

            multiFile.Files[0].Priority = Priority.Normal;
            multiFile.Files[1].Priority = Priority.DoNotDownload;
            multiFile.Files[2].Priority = Priority.Highest;
            multiFile.Files[3].Priority = Priority.High;
            multiFile.Files[4].Priority = Priority.Lowest;
            multiFile.Files[5].Priority = Priority.Low;
            multiFile.Files[6].Priority = Priority.DoNotDownload; // 12 byte file
            multiFile.Files[7].Priority = Priority.High; // 12 byte file

            picker.PickPiece (multiPeer, multiBitfield, peers, 1, 0, multiBitfield.Length - 1);
            Assert.AreEqual (5, checker.Picks.Count, "#1");

            // Make sure every downloadable file is available
            var bf = new MutableBitField (multiBitfield.Length);
            foreach (var file in multiFile.Files.Where (t => t.Priority != Priority.DoNotDownload)) {
                Assert.IsTrue (picker.IsInteresting (multiPeer, bf.SetAll (false).Set (file.StartPieceIndex, true)), "#2");
                Assert.IsTrue (picker.IsInteresting (multiPeer, bf.SetAll (false).Set (file.EndPieceIndex, true)), "#3");
            }

            // Make sure the not downloadable file is not available and
            // that everything was selected in priority order.
            Assert.IsFalse (picker.IsInteresting (multiPeer, bf.SetAll (false).Set (multiFile.Files[1].StartPieceIndex + 1, true)), "#4");
            Assert.IsFalse (picker.IsInteresting (multiPeer, bf.SetAll (false).Set (multiFile.Files[1].EndPieceIndex - 1, true)), "#5");

            bf = new MutableBitField (multiBitfield.Length).SetTrue (multiFile.Files[2].GetSelector ());
            Assert.AreEqual (bf, checker.Picks[0].available, "#6");

            bf = new MutableBitField (multiBitfield.Length).SetTrue (multiFile.Files[3].GetSelector ())
                .SetTrue (multiFile.Files[7].GetSelector ());
            Assert.AreEqual (bf, checker.Picks[1].available, "#7");

            bf = new MutableBitField (multiBitfield.Length).SetTrue (multiFile.Files[0].GetSelector ());
            Assert.AreEqual (bf, checker.Picks[2].available, "#8");

            bf = new MutableBitField (multiBitfield.Length).SetTrue (multiFile.Files[5].GetSelector ());
            Assert.AreEqual (bf, checker.Picks[3].available, "#9");

            bf = new MutableBitField (multiBitfield.Length).SetTrue (multiFile.Files[4].GetSelector ());
            Assert.AreEqual (bf, checker.Picks[4].available, "#10");
        }


        [Test]
        public void MultiFile_NoneAvailable ()
        {
            multiBitfield.SetAll (false);
            multiFile.SetAll (Priority.Highest);
            picker.Initialise (multiFile);

            picker.PickPiece (multiPeer, multiBitfield, peers, 1, 0, multiBitfield.Length - 1);
            Assert.AreEqual (0, checker.Picks.Count, "#1");
            Assert.IsFalse (picker.IsInteresting (multiPeer, multiBitfield), "#2");

            multiBitfield.SetAll (true);
            picker.PickPiece (multiPeer, multiBitfield, peers, 1, 0, multiBitfield.Length - 1);
            Assert.AreEqual (1, checker.Picks.Count, "#3");
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#4");
        }

        [Test]
        public void MultiFile_Highest_RestNormal ()
        {
            multiFile.Files[1].Priority = Priority.Highest;
            picker.Initialise (multiFile);

            picker.PickPiece (multiPeer, multiBitfield, new List<PeerId> (), 1, 0, multiBitfield.Length - 1);
            Assert.AreEqual (2, checker.Picks.Count, "#1");
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#2");
            Assert.AreEqual (new MutableBitField (multiBitfield.Length).SetTrue (multiFile.Files[1].GetSelector ()), checker.Picks[0].available, "#3");

            var bf = new MutableBitField (multiBitfield.Length);
            foreach (var v in multiFile.Files.Except (new[] { multiFile.Files[1] }))
                bf.SetTrue (v.GetSelector ());

            Assert.AreEqual (bf, checker.Picks[1].available, "#4");
        }

        [Test]
        public void SingleFile ()
        {
            singleFile.Files[0].Priority = Priority.Lowest;
            picker.Initialise (singleFile);

            picker.PickPiece (singlePeer, singleBitfield, peers, 1, 0, singleBitfield.Length -1 );
            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.IsTrue (checker.Picks[0].available.AllTrue, "#2");
            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#2");
        }

        [Test]
        public void SingleFile_ChangingPriority ()
        {
            picker.Initialise (singleFile);

            // Every time the priority is not 'DoNotDownload' and we try to pick a piece,
            // we should get a new bitfield.
            for (int i = 0; i < 10; i++) {
                singleFile.Files[0].Priority = i % 2 == 0 ? Priority.DoNotDownload : Priority.Normal;
                picker.PickPiece (singlePeer, singleBitfield, peers, 1, 0, singleBitfield.Length - 1);
                Assert.AreEqual ((i / 2) + (i % 2), checker.Picks.Count, "#1." + i);
            }
        }

        [Test]
        public void SingleFile_CheckInteresting ()
        {
            picker.Initialise (singleFile);

            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#0");
            singleFile.Files[0].Priority = Priority.Lowest;
            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#1");

            singleFile.Files[0].Priority = Priority.DoNotDownload;
            Assert.IsFalse (picker.IsInteresting (singlePeer, singleBitfield), "#2");

            singleFile.Files[0].Priority = Priority.High;
            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#2");
        }

        [Test]
        public void SingleFile_DoNotDownload ()
        {
            singleFile.Files[0].Priority = Priority.DoNotDownload;
            picker.Initialise (singleFile);

            picker.PickPiece (singlePeer, singleBitfield, peers, 1, 0, singleBitfield.Length - 1);
            Assert.AreEqual (0, checker.Picks.Count, "#1");
            Assert.IsFalse (picker.IsInteresting (singlePeer, singleBitfield), "#2");
        }

        [Test]
        public void SingleFile_HighPriorityThenDoNotDownload ()
        {
            picker.Initialise (singleFile);

            singleFile.Files[0].Priority = Priority.High;
            picker.PickPiece (singlePeer, singleBitfield, peers, 1, 0, singleBitfield.Length - 1);
            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#2");

            singleFile.Files[0].Priority = Priority.DoNotDownload;
            picker.PickPiece (singlePeer, singleBitfield, peers, 1, 0, singleBitfield.Length - 1);
            Assert.AreEqual (1, checker.Picks.Count, "#3");
            Assert.IsFalse (picker.IsInteresting (singlePeer, singleBitfield), "#4");
        }


        [Test]
        public void SingleFile_NoneAvailable ()
        {
            singleBitfield.SetAll (false);
            singleFile.Files[0].Priority = Priority.Highest;
            picker.Initialise (singleFile);

            picker.PickPiece (singlePeer, singleBitfield, peers, 1, 0, singleBitfield.Length - 1);
            Assert.AreEqual (0, checker.Picks.Count, "#1");
            Assert.IsFalse (picker.IsInteresting (singlePeer, singleBitfield), "#2");
        }
    }
}
