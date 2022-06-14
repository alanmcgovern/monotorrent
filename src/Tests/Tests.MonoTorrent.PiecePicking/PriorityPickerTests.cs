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

using MonoTorrent.Client;

using NUnit.Framework;

namespace MonoTorrent.PiecePicking
{
    [TestFixture]
    public class PriorityPickerTests
    {
        public void SetAll (IPieceRequesterData info, Priority priority)
        {
            foreach (var file in info.Files)
                Set (file, priority);
        }

        public void Set (ITorrentManagerFile file, Priority priority)
            => ((TorrentFileInfo) file).Priority = priority;

        PiecePickerFilterChecker checker;
        ReadOnlyBitField[] peers;
        PriorityPicker picker;

        BitField singleBitfield;
        IPieceRequesterData singleFile;
        PeerId singlePeer;

        BitField multiBitfield;
        IPieceRequesterData multiFile;
        PeerId multiPeer;

        [SetUp]
        public void Setup ()
        {
            singleFile = CreateSingleFile ();
            singleBitfield = new BitField (singleFile.PieceCount).SetAll (true);
            singlePeer = PeerId.CreateNull (singleBitfield.Length);

            multiFile = CreateMultiFile ();
            multiBitfield = new BitField (multiFile.PieceCount).SetAll (true);
            multiPeer = PeerId.CreateNull (multiBitfield.Length);

            checker = new PiecePickerFilterChecker ();
            picker = new PriorityPicker (checker);
            peers = new ReadOnlyBitField[0];
        }

        static IPieceRequesterData CreateSingleFile ()
        {
            int pieceLength = 1024 * 16 * 16;
            var size = pieceLength * 32 + 123;
            return TestTorrentManagerInfo.Create (
                pieceLength: pieceLength,
                size: size,
                files: TorrentFileInfo.Create (pieceLength, ("Single", size, "full/path/Single"))
            );
        }

        static IPieceRequesterData CreateMultiFile ()
        {
            int pieceLength = 1024 * 16 * 16;

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
            return TestTorrentManagerInfo.Create (
                files: files,
                pieceLength: pieceLength,
                size: files.Sum (t => t.Length)
            );
        }

        [Test]
        public void MultiFile ()
        {
            picker.Initialise (multiFile);

            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            picker.PickPiece (multiPeer, multiBitfield, peers, 0, multiBitfield.Length - 1, buffer);
            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.IsTrue (checker.Picks[0].available.AllTrue, "#2");
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#3");
        }

        [Test]
        public void MultiFile_ChangingPriority ()
        {
            SetAll (multiFile, Priority.DoNotDownload);
            picker.Initialise (multiFile);

            // Every time the priority is not 'DoNotDownload' and we try to pick a piece,
            // we should get a new bitfield.
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            for (int i = 0; i < 10; i++) {
                Set (multiFile.Files[2], i % 2 == 0 ? Priority.DoNotDownload : Priority.Normal);
                picker.PickPiece (multiPeer, multiBitfield, peers, 0, multiBitfield.Length - 1, buffer);
                Assert.AreEqual ((i / 2) + (i % 2), checker.Picks.Count, "#1." + i);
            }
        }

        [Test]
        public void MultiFile_CheckInteresting ()
        {
            SetAll (multiFile, Priority.DoNotDownload);
            picker.Initialise (multiFile);

            Assert.IsFalse (picker.IsInteresting (multiPeer, multiBitfield), "#0");
            Set (multiFile.Files[4], Priority.Lowest);
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#1");

            Set (multiFile.Files[4], Priority.DoNotDownload);
            Assert.IsFalse (picker.IsInteresting (multiPeer, multiBitfield), "#2");

            Set (multiFile.Files[4], Priority.High);
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#2");
        }

        [Test]
        public void MultiFile_DoNotDownload ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            SetAll (multiFile, Priority.DoNotDownload);
            picker.Initialise (multiFile);

            picker.PickPiece (multiPeer, multiBitfield, peers, 0, multiBitfield.Length - 1, buffer);
            Assert.AreEqual (0, checker.Picks.Count, "#1");
            Assert.IsFalse (picker.IsInteresting (multiPeer, multiBitfield), "#2");

            SetAll (multiFile, Priority.Immediate);
            picker.PickPiece (multiPeer, multiBitfield, peers, 0, multiBitfield.Length - 1, buffer);
            Assert.AreEqual (1, checker.Picks.Count, "#3");
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#4");
        }

        [Test]
        public void MultiFile_EveryPriority ()
        {
            picker.Initialise (multiFile);

            Set (multiFile.Files[0], Priority.Normal);
            Set (multiFile.Files[1], Priority.DoNotDownload);
            Set (multiFile.Files[2], Priority.Highest);
            Set (multiFile.Files[3], Priority.High);
            Set (multiFile.Files[4], Priority.Lowest);
            Set (multiFile.Files[5], Priority.Low);
            Set (multiFile.Files[6], Priority.DoNotDownload); // 12 byte file
            Set (multiFile.Files[7], Priority.High); // 12 byte file

            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            picker.PickPiece (multiPeer, multiBitfield, peers, 0, multiBitfield.Length - 1, buffer);
            Assert.AreEqual (5, checker.Picks.Count, "#1");

            // Make sure every downloadable file is available
            var bf = new BitField (multiBitfield.Length);
            foreach (var file in multiFile.Files.Where (t => t.Priority != Priority.DoNotDownload)) {
                Assert.IsTrue (picker.IsInteresting (multiPeer, bf.SetAll (false).Set (file.StartPieceIndex, true)), "#2");
                Assert.IsTrue (picker.IsInteresting (multiPeer, bf.SetAll (false).Set (file.EndPieceIndex, true)), "#3");
            }

            // Make sure the not downloadable file is not available and
            // that everything was selected in priority order.
            Assert.IsFalse (picker.IsInteresting (multiPeer, bf.SetAll (false).Set (multiFile.Files[1].StartPieceIndex + 1, true)), "#4");
            Assert.IsFalse (picker.IsInteresting (multiPeer, bf.SetAll (false).Set (multiFile.Files[1].EndPieceIndex - 1, true)), "#5");

            bf = new BitField (multiBitfield.Length).SetTrue (((TorrentFileInfo)multiFile.Files[2]).GetSelector ());
            Assert.IsTrue (bf.SequenceEqual (checker.Picks[0].available), "#6");

            bf = new BitField (multiBitfield.Length).SetTrue (((TorrentFileInfo)multiFile.Files[3]).GetSelector ())
                .SetTrue (((TorrentFileInfo)multiFile.Files[7]).GetSelector ());
            Assert.IsTrue (bf.SequenceEqual (checker.Picks[1].available), "#7");

            bf = new BitField (multiBitfield.Length).SetTrue (((TorrentFileInfo)multiFile.Files[0]).GetSelector ());
            Assert.IsTrue (bf.SequenceEqual (checker.Picks[2].available), "#8");

            bf = new BitField (multiBitfield.Length).SetTrue (((TorrentFileInfo)multiFile.Files[5]).GetSelector ());
            Assert.IsTrue (bf.SequenceEqual (checker.Picks[3].available), "#9");

            bf = new BitField (multiBitfield.Length).SetTrue (((TorrentFileInfo)multiFile.Files[4]).GetSelector ());
            Assert.IsTrue (bf.SequenceEqual (checker.Picks[4].available), "#10");
        }


        [Test]
        public void MultiFile_NoneAvailable ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            multiBitfield.SetAll (false);
            SetAll (multiFile, Priority.Highest);
            picker.Initialise (multiFile);

            picker.PickPiece (multiPeer, multiBitfield, peers, 0, multiBitfield.Length - 1, buffer);
            Assert.AreEqual (0, checker.Picks.Count, "#1");
            Assert.IsFalse (picker.IsInteresting (multiPeer, multiBitfield), "#2");

            multiBitfield.SetAll (true);
            picker.PickPiece (multiPeer, multiBitfield, peers, 0, multiBitfield.Length - 1, buffer);
            Assert.AreEqual (1, checker.Picks.Count, "#3");
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#4");
        }

        [Test]
        public void MultiFile_Highest_RestNormal ()
        {
            Set (multiFile.Files[1], Priority.Highest);
            picker.Initialise (multiFile);

            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            picker.PickPiece (multiPeer, multiBitfield, Array.Empty<ReadOnlyBitField> (), 0, multiBitfield.Length - 1, buffer);
            Assert.AreEqual (2, checker.Picks.Count, "#1");
            Assert.IsTrue (picker.IsInteresting (multiPeer, multiBitfield), "#2");
            Assert.IsTrue (new BitField (multiBitfield.Length).SetTrue (((TorrentFileInfo) multiFile.Files[1]).GetSelector ()).SequenceEqual (checker.Picks[0].available), "#3");

            var bf = new BitField (multiBitfield.Length);
            foreach (var v in multiFile.Files.Except (new[] { multiFile.Files[1] }))
                bf.SetTrue (((TorrentFileInfo) v).GetSelector ());

            Assert.IsTrue (bf.SequenceEqual (checker.Picks[1].available), "#4");
        }

        [Test]
        public void SingleFile ()
        {
            Set (singleFile.Files[0], Priority.Lowest);
            picker.Initialise (singleFile);

            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            picker.PickPiece (singlePeer, singleBitfield, peers, 0, singleBitfield.Length - 1, buffer);
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
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            for (int i = 0; i < 10; i++) {
                Set (singleFile.Files[0], i % 2 == 0 ? Priority.DoNotDownload : Priority.Normal);
                picker.PickPiece (singlePeer, singleBitfield, peers, 0, singleBitfield.Length - 1, buffer);
                Assert.AreEqual ((i / 2) + (i % 2), checker.Picks.Count, "#1." + i);
            }
        }

        [Test]
        public void SingleFile_CheckInteresting ()
        {
            picker.Initialise (singleFile);

            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#0");
            Set (singleFile.Files[0], Priority.Lowest);
            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#1");

            Set (singleFile.Files[0], Priority.DoNotDownload);
            Assert.IsFalse (picker.IsInteresting (singlePeer, singleBitfield), "#2");

            Set (singleFile.Files[0], Priority.High);
            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#2");
        }

        [Test]
        public void SingleFile_DoNotDownload ()
        {
            Set (singleFile.Files[0], Priority.DoNotDownload);
            picker.Initialise (singleFile);

            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            picker.PickPiece (singlePeer, singleBitfield, peers, 0, singleBitfield.Length - 1, buffer);
            Assert.AreEqual (0, checker.Picks.Count, "#1");
            Assert.IsFalse (picker.IsInteresting (singlePeer, singleBitfield), "#2");
        }

        [Test]
        public void SingleFile_HighPriorityThenDoNotDownload ()
        {
            picker.Initialise (singleFile);
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];

            Set (singleFile.Files[0], Priority.High);
            picker.PickPiece (singlePeer, singleBitfield, peers, 0, singleBitfield.Length - 1, buffer);
            Assert.AreEqual (1, checker.Picks.Count, "#1");
            Assert.IsTrue (picker.IsInteresting (singlePeer, singleBitfield), "#2");

            Set (singleFile.Files[0], Priority.DoNotDownload);
            picker.PickPiece (singlePeer, singleBitfield, peers, 0, singleBitfield.Length - 1, buffer);
            Assert.AreEqual (1, checker.Picks.Count, "#3");
            Assert.IsFalse (picker.IsInteresting (singlePeer, singleBitfield), "#4");
        }


        [Test]
        public void SingleFile_NoneAvailable ()
        {
            singleBitfield.SetAll (false);
            Set (singleFile.Files[0], Priority.Highest);
            picker.Initialise (singleFile);

            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            picker.PickPiece (singlePeer, singleBitfield, peers, 0, singleBitfield.Length - 1, buffer);
            Assert.AreEqual (0, checker.Picks.Count, "#1");
            Assert.IsFalse (picker.IsInteresting (singlePeer, singleBitfield), "#2");
        }
    }
}
