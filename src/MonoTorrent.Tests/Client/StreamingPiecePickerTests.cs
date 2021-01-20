//
// StreamingPiecePickerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
    public class StreamingPiecePickerTests
    {
        class TorrentData : ITorrentData
        {
            public IList<ITorrentFileInfo> Files { get; set; }
            public int PieceLength { get; set; }
            public long Size { get; set; }
        }

        BitField bitfield;
        TorrentData data;
        PeerId peer, otherPeer;
        int blocksPerPiece;
        int pieceCount;

        [SetUp]
        public void Setup ()
        {
            blocksPerPiece = 4;
            pieceCount = 40;

            bitfield = new BitField (pieceCount);
            data = new TorrentData {
                PieceLength = Piece.BlockSize * blocksPerPiece,
                Files = new[] { new TorrentFileInfo (new TorrentFile ("Test", Piece.BlockSize * blocksPerPiece * pieceCount, 0, pieceCount - 1)) },
                Size = Piece.BlockSize * blocksPerPiece * pieceCount - 10
            };

            peer = PeerId.CreateNull (pieceCount, seeder: true, isChoking: false, amInterested: true);
            otherPeer = PeerId.CreateNull (pieceCount, seeder: true, isChoking: false, amInterested: true);
        }

        (StreamingPiecePicker picker, PiecePickerFilterChecker checker) CreatePicker ()
        {
            var checker = new PiecePickerFilterChecker (new StandardPicker ());
            var picker = new StreamingPiecePicker (checker);
            picker.Initialise (bitfield, data, Enumerable.Empty<ActivePieceRequest> ());
            return (picker, checker);
        }

        [Test]
        public void HighPriorityPreferred ()
        {
            (var picker, var checker) = CreatePicker ();

            picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            Assert.AreEqual (checker.Picks[0].startIndex, 0);
            Assert.AreEqual (checker.Picks[0].endIndex, picker.HighPriorityCount - 1);
        }

        [Test]
        public void LowPriority_AlwaysHigherThanHighPriority ()
        {
            (var picker, _) = CreatePicker ();
            Assert.IsTrue (picker.SeekToPosition (data.Files[0], data.PieceLength * 5));
            picker.HighPriorityCount = 20;

            var remainingToDownload = bitfield
                .Clone ()
                .SetTrue ((picker.HighPriorityPieceIndex, picker.HighPriorityPieceIndex + picker.HighPriorityCount - 1))
                .Not ();

            IList<PieceRequest> req;
            List<PieceRequest> requests = new List<PieceRequest> ();
            peer.MaxPendingRequests = peer.MaxSupportedPendingRequests = 100000;
            while ((req = picker.PickPiece (peer, remainingToDownload, new List<PeerId> (), 1, 0, bitfield.Length - 1)) != null)
                requests.Add (req.Single ());

            Assert.IsTrue (requests.All (t => t.PieceIndex > picker.HighPriorityPieceIndex));
        }

        [Test]
        public void LowPriority_PeersDoNotSharePieceRequests ()
        {
            (var picker, _) = CreatePicker ();
            var alreadyDownloaded = bitfield
                .Clone ()
                .SetTrue ((picker.HighPriorityPieceIndex, picker.HighPriorityPieceIndex + picker.HighPriorityCount - 1))
                .Not ();

            var startPiece = picker.HighPriorityPieceIndex + picker.HighPriorityCount;
            var endPiece = bitfield.Length - 1;
            var pieces = picker.PickPiece (peer, alreadyDownloaded, new List<PeerId> (), 1, startPiece, endPiece);
            var otherPieces = picker.PickPiece (otherPeer, alreadyDownloaded, new List<PeerId> (), 1, startPiece, endPiece);
            Assert.AreNotEqual (pieces.Single ().PieceIndex, otherPieces.Single ().PieceIndex);
        }

        [Test]
        public void HighPriority_PeersSharePieceRequests ()
        {
            (var picker, var checker) = CreatePicker ();

            picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            Assert.AreEqual (checker.Picks[0].startIndex, 0);
            Assert.AreEqual (checker.Picks[0].endIndex, picker.HighPriorityCount - 1);
        }

        [Test]
        public void CheckPiecesPicker_Start ()
        {
            (var picker, _) = CreatePicker ();

            var requests = picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual (i / 4, requests[i].PieceIndex);
        }

        [Test]
        public void CheckPiecesPicker_Mid ()
        {
            (var picker, _) = CreatePicker ();
            Assert.IsTrue (picker.SeekToPosition (data.Files[0], data.PieceLength * 7));

            var requests = picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual (i / 4 + 7, requests[i].PieceIndex);
        }

        [Test]
        public void PickAfterHighPriorityDownloaded ()
        {
            (var streamingPicker, _) = CreatePicker ();

            Assert.IsFalse (streamingPicker.SeekToPosition (data.Files[0], 0));
            for (int i = 0; i < streamingPicker.HighPriorityCount; i++)
                bitfield.SetTrue ((0, streamingPicker.HighPriorityCount));

            var picker = new IgnoringPicker (bitfield, streamingPicker);
            var requests = picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            for (int i = 0; i < requests.Count; i++)
                Assert.Greater (requests[i].PieceIndex, streamingPicker.HighPriorityCount);
        }

        [Test]
        public void StreamingPickerSupportsPriority ()
        {
            (var picker, _) = CreatePicker ();
            data.Files[0].Priority = Priority.DoNotDownload;
            Assert.IsFalse (picker.SeekToPosition (data.Files[0], 0));

            // This should check the high priority range and low priority range
            Assert.IsNull (picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16));

            // This makes sure we check the low priority set by marking all the high priority
            // pieces are received.
            var ignoringPicker = new IgnoringPicker (bitfield, picker);
            for (int i = 0; i < picker.HighPriorityCount; i++)
                bitfield.SetTrue ((0, picker.HighPriorityCount));

            Assert.IsNull (ignoringPicker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16));
        }
    }
}
