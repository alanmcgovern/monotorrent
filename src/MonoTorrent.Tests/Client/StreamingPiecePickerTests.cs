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
            public TorrentFile[] Files { get; set; }
            public int PieceLength { get; set; }
            public long Size { get; set; }
        }

        BitField bitfield;
        TorrentData data;
        PeerId peer;
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
                Files = new[] { new TorrentFile ("Test", Piece.BlockSize * blocksPerPiece * pieceCount, 0, pieceCount - 1) },
                Size = Piece.BlockSize * blocksPerPiece * pieceCount - 10
            };

            peer = PeerId.CreateNull (pieceCount, seeder: true, isChoking: false, amInterested: true);
        }

        (StreamingPiecePicker picker, T basePicker) CreatePicker<T> ()
            where T : PiecePicker, new ()
        {
            var basePicker = new T ();
            var picker = new StreamingPiecePicker (basePicker);
            picker.Initialise (bitfield, data, Enumerable.Empty<Piece> ());
            return (picker, basePicker);
        }

        [Test]
        public void ReadDoesNotCancelRequests ()
        {
            (var picker, var checker) = CreatePicker<TestPicker> ();

            picker.PickPiece (peer, peer.BitField, Array.Empty<IPieceRequester> ());
            picker.ReadToPosition (data.Files[0], 0);
            picker.PickPiece (peer, peer.BitField, Array.Empty<IPieceRequester> ());
            Assert.IsFalse (checker.HasCancelledRequests);
        }

        [Test]
        public void SeekDoesCancelRequests ()
        {
            (var picker, var checker) = CreatePicker<TestPicker> ();

            PeerId[] peers = new [] {
                PeerId.CreateNull (pieceCount, seeder: true, isChoking: false, amInterested: true),
                PeerId.CreateNull (pieceCount, seeder: true, isChoking: false, amInterested: true),
                PeerId.CreateNull (pieceCount, seeder: true, isChoking: false, amInterested: true),
            };
            picker.PickPiece (peer, peer.BitField, peers);
            picker.SeekToPosition (data.Files[0], 0);
            picker.PickPiece (peer, peer.BitField, peers);
            Assert.IsTrue(checker.HasCancelledRequests);
            Assert.IsTrue (peers.All (p => checker.CancelledRequestsFrom.Contains (p)));
            Assert.IsTrue (checker.CancelledRequestsFrom.Contains (peer));
        }

        [Test]
        public void HighPriorityPreferred ()
        {
            (var picker, var checker) = CreatePicker<TestPicker> ();

            checker.ReturnNoPiece = false;
            picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            Assert.AreEqual (checker.PickedIndex.Single (), Tuple.Create (0, picker.HighPriorityCount - 1));
        }

        [Test]
        public void CheckPiecesPicker_Start ()
        {
            (var picker, var checker) = CreatePicker<StandardPicker> ();

            var requests = picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual (i / 4, requests[i].PieceIndex);
        }

        [Test]
        public void CheckPiecesPicker_Mid ()
        {
            (var picker, var checker) = CreatePicker<StandardPicker> ();
            picker.SeekToPosition (data.Files[0], data.PieceLength * 7);

            var requests = picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual (i / 4 + 7, requests[i].PieceIndex);
        }

        [Test]
        public void PickAfterHighPriorityDownloaded ()
        {
            (var streamingPicker, var checker) = CreatePicker<StandardPicker> ();

            streamingPicker.SeekToPosition (data.Files[0], 0);
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
            (var picker, var checker) = CreatePicker<StandardPicker> ();
            data.Files[0].Priority = Priority.DoNotDownload;
            picker.SeekToPosition (data.Files[0], 0);

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
