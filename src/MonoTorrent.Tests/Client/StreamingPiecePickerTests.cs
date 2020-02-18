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
        TestPicker checker;
        TorrentData data;
        PeerId peer;
        StreamingPiecePicker picker;
        int pieceCount;

        [SetUp]
        public void Setup ()
        {
            pieceCount = 40;

            checker = new TestPicker ();
            picker = new StreamingPiecePicker (checker);

            bitfield = new BitField (pieceCount);
            data = new TorrentData {
                PieceLength = Piece.BlockSize * 4,
                Files = new[] { new TorrentFile ("Test", Piece.BlockSize * 4 * pieceCount) },
                Size = Piece.BlockSize * 4 * pieceCount - 10
            };
            picker.Initialise (bitfield, data, Array.Empty<Piece> ());

            peer = PeerId.CreateNull (pieceCount, seeder: true, isChoking: false, amInterested: true);
        }

        [Test]
        public void ReadDoesNotCancelRequests ()
        {
            picker.PickPiece (peer, peer.BitField, Array.Empty<IPieceRequester> ());
            picker.ReadToPosition (data.Files[0], 0);
            picker.PickPiece (peer, peer.BitField, Array.Empty<IPieceRequester> ());
            Assert.IsFalse (checker.HasCancelledRequests);
        }

        [Test]
        public void SeekDoesCancelRequests ()
        {
            picker.PickPiece (peer, peer.BitField, Array.Empty<IPieceRequester> ());
            picker.SeekToPosition (data.Files[0], 0);
            picker.PickPiece (peer, peer.BitField, Array.Empty<IPieceRequester> ());
            Assert.IsTrue(checker.HasCancelledRequests);
        }

        [Test]
        public void PickingSequential ()
        {
            checker.ReturnNoPiece = false;
            var requests = picker.PickPiece (peer, peer.BitField, new List<PeerId> (), 16);
            Assert.AreEqual (checker.PickedIndex.Single (), Tuple.Create (0, picker.HighPriorityCount - 1));
        }
    }
}
