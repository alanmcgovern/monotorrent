//
// EndGamePickerTests.cs
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
using System.Linq;

using NUnit.Framework;

namespace MonoTorrent.Client.PiecePicking
{
    [TestFixture]
    public class EndGamePickerTests
    {
        class TestTorrentData : ITorrentData
        {
            public IList<ITorrentFileInfo> Files { get; set; }
            public int PieceCount => (int) (Size / PieceLength);
            public int PieceLength { get; set; }
            public long Size { get; set; }
        }

        BitField bitfield;
        EndGamePicker picker;
        List<Piece> pieces;
        TestTorrentData torrentData;

        PeerId id;
        PeerId other;

        [SetUp]
        public void Setup ()
        {
            var pieceCount = 40;
            var pieceLength = 256 * 1024;

            torrentData = new TestTorrentData {
                Files = new[] { new TorrentFileInfo (new TorrentFile ("One File", pieceLength * pieceCount, 0, pieceCount)) },
                PieceLength = pieceLength,
                Size = pieceLength * pieceCount
            };

            bitfield = new BitField (torrentData.PieceCount)
                .SetAll (true)
                .Set (4, false)
                .Set (6, false)
                .Set (24, false)
                .Set (36, false);

            picker = new EndGamePicker ();
            pieces = new List<Piece> (new[] {
                new Piece(4, torrentData.PieceLength, torrentData.Size),
                new Piece(6, torrentData.PieceLength, torrentData.Size),
                new Piece(24, torrentData.PieceLength, torrentData.Size),
                new Piece(36, torrentData.PieceLength, torrentData.Size)
            });

            id = PeerId.CreateNull (torrentData.PieceCount);
            id.IsChoking = false;

            other = PeerId.CreateNull (torrentData.PieceCount);
            other.IsChoking = false;
        }

        [Test]
        public void CancelAllPendingWhenPieceReceived ()
        {
            id.BitField[0] = other.BitField[0] = true;
            picker.Initialise (bitfield, torrentData);

            var otherRequest = picker.PickPiece (other, other.BitField, new List<PeerId> ());

            PieceRequest message;
            bool pieceComplete = false;
            while (!pieceComplete && (message = picker.PickPiece (id, id.BitField, new List<PeerId> ())) != null) {
                Assert.IsTrue (picker.ValidatePiece (id, message.PieceIndex, message.StartOffset, message.RequestLength, out pieceComplete, out _));
            }

            Assert.AreEqual (0, id.AmRequestingPiecesCount, "#requesting");
            Assert.IsFalse (picker.ValidatePiece (other, otherRequest.PieceIndex, otherRequest.StartOffset, otherRequest.RequestLength, out pieceComplete, out _), "#1");
            Assert.IsFalse (pieceComplete, "#2");

            message = picker.PickPiece (other, other.BitField, new List<PeerId> ());
            Assert.AreEqual (0, message.PieceIndex, "#3");
        }

        [Test]
        public void CancelTest ()
        {
            var requests = new List<PieceRequest> ();
            foreach (Piece p in pieces) {
                for (int i = 0; i < p.BlockCount; i++) {
                    requests.Add (p.Blocks[i].CreateRequest (i % 2 == 0 ? id : other));
                }
            }

            Assert.AreNotEqual (0, id.AmRequestingPiecesCount);
            Assert.AreNotEqual (0, other.AmRequestingPiecesCount);

            picker.Initialise (bitfield, torrentData, requests);
            picker.CancelRequests (id);
            picker.CancelRequests (other);

            Assert.AreEqual (0, id.AmRequestingPiecesCount);
            Assert.AreEqual (0, other.AmRequestingPiecesCount);

            id.BitField[4] = true;
            Assert.IsNotNull (picker.PickPiece (id, id.BitField));
        }

        [Test]
        public void MultiPick ()
        {
            id.BitField.Set (pieces[0].Index, true);
            other.BitField.Set (pieces[0].Index, true);

            var requests = new List<PieceRequest> ();
            for (int i = 2; i < pieces[0].BlockCount; i++) {
                requests.Add (pieces[0].Blocks[i].CreateRequest (PeerId.CreateNull (torrentData.PieceCount)));
                pieces[0].Blocks[i].Received = true;
            }

            picker.Initialise (bitfield, torrentData, requests);

            // Pick blocks 1 and 2 for both peers
            while (picker.PickPiece (id, id.BitField, new List<PeerId> ()) != null) { }
            while (picker.PickPiece (other, id.BitField, new List<PeerId> ()) != null) { }

            Assert.AreEqual (2, id.AmRequestingPiecesCount, "#1");
            Assert.AreEqual (2, other.AmRequestingPiecesCount, "#1");

            if (!picker.ValidatePiece (id, pieces[0].Index, pieces[0][0].StartOffset, pieces[0][0].RequestLength, out _, out _))
                Assert.Fail ("I should've validated!");

            if (picker.ValidatePiece (other, pieces[0].Index, pieces[0][0].StartOffset, pieces[0][0].RequestLength, out _, out _))
                Assert.Fail ("I should not have validated!");

            Assert.AreEqual (1, id.AmRequestingPiecesCount, "#1");
            Assert.AreEqual (1, other.AmRequestingPiecesCount, "#1");
            Assert.IsTrue (pieces[0][0].Received, "#5");
            Assert.AreEqual (16, pieces[0].TotalRequested, "#6");
            Assert.AreEqual (15, pieces[0].TotalReceived, "#7");
        }

        [Test]
        public void HashFail ()
        {
            PieceRequest m;
            List<PieceRequest> requests = new List<PieceRequest> ();

            id.BitField[0] = true;
            picker.Initialise (bitfield, torrentData);

            while ((m = picker.PickPiece (id, id.BitField)) != null)
                requests.Add (m);

            foreach (var message in requests)
                Assert.IsTrue (picker.ValidatePiece (id, message.PieceIndex, message.StartOffset, message.RequestLength, out _, out _));

            Assert.IsNotNull (picker.PickPiece (id, id.BitField));
        }

        [Test]
        public void ReceivedPiecesAreNotRequested ()
        {
            var requests = new List<PieceRequest> ();
            for (int i = 2; i < pieces[0].BlockCount; i++) {
                requests.Add (pieces[0].Blocks[i].CreateRequest (PeerId.CreateNull (torrentData.PieceCount)));
                pieces[0].Blocks[i].Received = true;
            }

            picker.Initialise (bitfield, torrentData, requests);
            Assert.IsTrue (picker.Requests.All (t => !t.Received), "#1");
        }
    }
}
