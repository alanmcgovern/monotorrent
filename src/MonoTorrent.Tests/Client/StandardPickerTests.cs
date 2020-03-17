//
// StandardPickerTests.cs
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
    public class StandardPickerTests
    {
        class TestTorrentData : ITorrentData
        {
            public TorrentFile[] Files { get; set; }
            public int PieceLength { get; set; }
            public long Size { get; set; }

            public int BlocksPerPiece => PieceLength / Piece.BlockSize;
            public int PieceCount => (int) Math.Ceiling ((double) Size / PieceLength);
            public int TotalBlocks => (int) Math.Ceiling ((double) Size / Piece.BlockSize);
        }

        BitField bitfield;
        PeerId peer;
        List<PeerId> peers;
        PiecePicker picker;
        TestTorrentData torrentData;

        [SetUp]
        public void Setup ()
        {
            int pieceCount = 40;
            int pieceLength = 256 * 1024;
            bitfield = new BitField (pieceCount);
            torrentData = new TestTorrentData {
                Files = new[] { new TorrentFile ("File", pieceLength * pieceCount) },
                PieceLength = pieceLength,
                Size = pieceLength * pieceCount
            };
            peers = new List<PeerId> ();

            picker = new StandardPicker ();
            picker.Initialise (bitfield, torrentData, Enumerable.Empty<Piece> ());

            peer = PeerId.CreateNull (pieceCount);
            for (int i = 0; i < 20; i++) {
                PeerId p = PeerId.CreateNull (pieceCount);
                p.SupportsFastPeer = true;
                peers.Add (p);
            }
        }

        [Test]
        public void RequestFastSeeder ()
        {
            int[] allowedFast = { 1, 2, 3, 5, 8, 13, 21 };
            peers[0].SupportsFastPeer = true;
            peers[0].IsAllowedFastPieces.AddRange ((int[]) allowedFast.Clone ());

            peers[0].BitField.SetAll (true); // Lets pretend he has everything
            for (int i = 0; i < 7; i++) {
                for (int j = 0; j < 16; j++) {
                    var msg = picker.PickPiece (peers[0], peers[0].BitField, peers);
                    Assert.IsNotNull (msg, "#1." + j);
                    Assert.IsTrue (Array.IndexOf (allowedFast, msg.PieceIndex) > -1, "#2." + j);
                }
            }
            Assert.IsNull (picker.PickPiece (peers[0], peers[0].BitField, peers));
        }

        [Test]
        public void RequestEntireFastPiece ()
        {
            var id = peers[0];
            int[] allowedFast = { 1, 2 };
            id.SupportsFastPeer = true;
            id.IsAllowedFastPieces.AddRange ((int[]) allowedFast.Clone ());
            id.BitField.SetAll (true); // Lets pretend he has everything

            PieceRequest request;
            var pieceRequests = new List<PieceRequest> ();
            while ((request = picker.PickPiece (id, id.BitField, new List<PeerId> ())) != null)
                pieceRequests.Add (request);

            var expectedRequests = torrentData.PieceLength / Piece.BlockSize;
            Assert.AreEqual (expectedRequests * 2, pieceRequests.Count, "#1");
            Assert.IsTrue (pieceRequests.All (r => r.PieceIndex == 1 || r.PieceIndex == 2), "#2");
            for (int i = 0; i < expectedRequests; i++) {
                Assert.AreEqual (2, pieceRequests.Count (t => t.StartOffset == i * Piece.BlockSize && t.RequestLength == Piece.BlockSize), "#2." + i);
            }
        }

        [Test]
        public void RequestFastNotSeeder ()
        {
            peers[0].SupportsFastPeer = true;
            peers[0].IsAllowedFastPieces.AddRange (new[] { 1, 2, 3, 5, 8, 13, 21 });

            peers[0].BitField.SetAll (true);
            peers[0].BitField[1] = false;
            peers[0].BitField[3] = false;
            peers[0].BitField[5] = false;

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 16; j++) {
                    var m = picker.PickPiece (peers[0], peers[0].BitField, peers);
                    Assert.IsTrue (m.PieceIndex == 2 || m.PieceIndex == 8 || m.PieceIndex == 13 || m.PieceIndex == 21);
                }

            Assert.IsNull (picker.PickPiece (peers[0], peers[0].BitField, peers));
        }

        [Test]
        public void RequestChoked ()
        {
            Assert.IsNull (picker.PickPiece (peers[0], peers[0].BitField, peers));
        }

        [Test]
        public void StandardPicker_PickStandardPiece ()
        {
            peers[0].IsChoking = false;
            peers[0].BitField.SetAll (true);

            bitfield[1] = true;
            var message = picker.PickPiece (peers[0], bitfield.Clone ().Not (), peers, 1, 0, 10);
            Assert.AreEqual (0, message[0].PieceIndex);

            peers[1].IsChoking = false;
            peers[1].BitField.SetAll (true);
            peers[1].Peer.HashedPiece (false);
            message = picker.PickPiece (peers[1], bitfield.Clone ().Not (), peers, 1, 0, 10);
            Assert.AreEqual (2, message[0].PieceIndex);
        }

        [Test]
        public void NoInterestingPieces ()
        {
            peer.IsChoking = false;
            Assert.IsNull (picker.PickPiece (peer, new BitField (torrentData.PieceCount), peers));
        }

        [Test]
        public void CancelRequests ()
        {
            var messages = new List<PieceRequest> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            PieceRequest m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m);

            picker.PickPiece (peer, peer.BitField, peers);
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#0");
            picker.CancelRequests (peer);

            var messages2 = new List<PieceRequest> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages2.Add (m);

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]));
        }

        [Test]
        public void RejectRequests ()
        {
            var messages = new List<PieceRequest> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            PieceRequest m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m);

            foreach (PieceRequest message in messages)
                picker.CancelRequest (peer, message.PieceIndex, message.StartOffset, message.RequestLength);

            var messages2 = new List<PieceRequest> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages2.Add (m);

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void PeerChoked ()
        {
            var messages = new List<PieceRequest> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            PieceRequest m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m);

            picker.CancelRequests (peer);

            var messages2 = new List<PieceRequest> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages2.Add (m);

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void ChokeThenClose ()
        {
            var messages = new List<PieceRequest> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            peer.SupportsFastPeer = true;

            PieceRequest m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m);

            picker.CancelRequests (peer);

            var messages2 = new List<PieceRequest> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages2.Add (m);

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void RequestBlocks_50 ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var b = picker.PickPiece (peer, peer.BitField, peers, 50);
            Assert.AreEqual (50, b.Count, "#1");
        }

        [Test]
        public void RequestBlocks_All ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var b = picker.PickPiece (peer, peer.BitField, peers, torrentData.TotalBlocks);
            Assert.AreEqual (torrentData.TotalBlocks, b.Count, "#1");
        }

        [Test]
        public void RequestBlocks_TooMany ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var b = picker.PickPiece (peer, peer.BitField, peers, torrentData.TotalBlocks * 2);
            Assert.AreEqual (torrentData.TotalBlocks, b.Count, "#1");
        }

        [Test]
        public void InvalidFastPiece ()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange (new[] { 1, 2, 5, 55, 62, 235, 42624 });
            peer.BitField.SetAll (true);
            for (int i = 0; i < torrentData.BlocksPerPiece * 3; i++) {
                var m = picker.PickPiece (peer, peer.BitField, peers);
                Assert.IsNotNull (m, "#1." + i);
                Assert.IsTrue (m.PieceIndex == 1 || m.PieceIndex == 2 || m.PieceIndex == 5, "#2");
            }

            for (int i = 0; i < 10; i++)
                Assert.IsNull (picker.PickPiece (peer, peer.BitField, peers), "#3");
        }

        [Test]
        public void CompletePartialTest ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var message = picker.PickPiece (peer, peer.BitField, peers);
            Assert.IsTrue (picker.ValidatePiece (peer, message.PieceIndex, message.StartOffset, message.RequestLength, out Piece piece), "#1");
            picker.CancelRequests (peer);
            for (int i = 0; i < piece.BlockCount; i++) {
                message = picker.PickPiece (peer, peer.BitField, peers);
                Assert.IsTrue (picker.ValidatePiece (peer, message.PieceIndex, message.StartOffset, message.RequestLength, out _), "#2." + i);
            }
            Assert.IsTrue (piece.AllBlocksRequested, "#3");
            Assert.IsTrue (piece.AllBlocksReceived, "#4");
        }

        [Test]
        public void DoesntHaveFastPiece ()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange (new[] { 1, 2, 3, 4 });
            peer.BitField.SetAll (true);
            picker = new StandardPicker ();
            picker.Initialise (bitfield, torrentData, new List<Piece> ());
            var bundle = picker.PickPiece (peer, new BitField (peer.BitField.Length), peers, 1, 0, peer.BitField.Length);
            Assert.IsNull (bundle);
        }


        [Test]
        public void DoesntHaveSuggestedPiece ()
        {
            peer.IsChoking = false;
            peer.SupportsFastPeer = true;
            peer.SuggestedPieces.AddRange (new[] { 1, 2, 3, 4 });
            peer.BitField.SetAll (true);
            picker = new StandardPicker ();
            picker.Initialise (bitfield, torrentData, new List<Piece> ());
            var bundle = picker.PickPiece (peer, new BitField (peer.BitField.Length), peers, 1, 0, peer.BitField.Length);
            Assert.IsNull (bundle);
        }

        [Test]
        public void InvalidSuggestPiece ()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.SuggestedPieces.AddRange (new[] { 1, 2, 5, 55, 62, 235, 42624 });
            peer.BitField.SetAll (true);
            picker.PickPiece (peer, peer.BitField, peers);
        }

        [Test]
        public void PickBundle ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            IList<PieceRequest> bundle;
            var messages = new List<PieceRequest> ();

            while ((bundle = picker.PickPiece (peer, peer.BitField, peers, torrentData.BlocksPerPiece * 5)) != null) {
                Assert.IsTrue (bundle.Count == torrentData.BlocksPerPiece * 5
                              || (bundle.Count + messages.Count) == torrentData.TotalBlocks, "#1");
                messages.AddRange (bundle);
            }
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#2");
        }

        [Test]
        public void PickBundle_2 ()
        {
            peer.IsChoking = false;

            for (int i = 0; i < 7; i++)
                peer.BitField[i] = true;

            IList<PieceRequest> bundle;
            var messages = new List<PieceRequest> ();

            while ((bundle = picker.PickPiece (peer, peer.BitField, peers, torrentData.BlocksPerPiece * 5)) != null) {
                Assert.IsTrue (bundle.Count == torrentData.BlocksPerPiece * 5
                              || (bundle.Count + messages.Count) == torrentData.BlocksPerPiece * 7, "#1");
                messages.AddRange (bundle);
            }
            Assert.AreEqual (torrentData.BlocksPerPiece * 7, messages.Count, "#2");
        }

        [Test]
        public void PickBundle_3 ()
        {
            var messages = new List<PieceRequest> ();
            peers[2].IsChoking = false;
            peers[2].BitField.SetAll (true);
            messages.Add (picker.PickPiece (peers[2], peers[2].BitField, peers));

            peer.IsChoking = false;

            for (int i = 0; i < 7; i++)
                peer.BitField[i] = true;

            IList<PieceRequest> bundle;

            while ((bundle = picker.PickPiece (peer, peer.BitField, peers, torrentData.BlocksPerPiece * 5)) != null)
                messages.AddRange (bundle);

            Assert.AreEqual (torrentData.BlocksPerPiece * 7, messages.Count, "#2");
        }

        [Test]
        public void PickBundle4 ()
        {
            peers[0].IsChoking = false;
            peers[0].BitField.SetAll (true);

            for (int i = 0; i < torrentData.BlocksPerPiece; i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 1, 4, 4);
            for (int i = 0; i < torrentData.BlocksPerPiece; i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 1, 6, 6);

            var b = picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 20 * torrentData.BlocksPerPiece);
            foreach (PieceRequest m in b)
                Assert.IsTrue (m.PieceIndex > 6);
        }

        [Test]
        public void Pick20SequentialPieces ()
        {
            // As we want to pick 20 pieces, we ignore the first 5 available and choose from the group of 20.
            foreach (var i in Enumerable.Range (0, 5).Concat (Enumerable.Range (10, 20)))
                bitfield[i] = true;

            peers[0].IsChoking = false;

            var b = picker.PickPiece (peers[0], bitfield, new List<PeerId> (), 20 * torrentData.BlocksPerPiece);
            Assert.AreEqual (20 * torrentData.BlocksPerPiece, b.Count);
            foreach (PieceRequest m in b)
                Assert.IsTrue (m.PieceIndex >= 10 && m.PieceIndex < 30);
        }

        [Test]
        public void PickBundle6 ()
        {
            bitfield.SetAll (false);

            peers[0].IsChoking = false;
            peers[0].BitField.SetAll (true);

            for (int i = 0; i < torrentData.BlocksPerPiece; i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 1, 0, 0);
            for (int i = 0; i < torrentData.BlocksPerPiece; i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 1, 1, 1);
            for (int i = 0; i < torrentData.BlocksPerPiece; i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 1, 3, 3);
            for (int i = 0; i < torrentData.BlocksPerPiece; i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 1, 6, 6);

            var b = picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 2 * torrentData.BlocksPerPiece);
            Assert.AreEqual (2 * torrentData.BlocksPerPiece, b.Count);
            foreach (PieceRequest m in b)
                Assert.IsTrue (m.PieceIndex >= 4 && m.PieceIndex < 6);
        }

        [Test]
        public void FastPieceTest ()
        {
            for (int i = 0; i < 2; i++) {
                peers[i].BitField.SetAll (true);
                peers[i].SupportsFastPeer = true;
                peers[i].IsAllowedFastPieces.Add (5);
                peers[i].IsAllowedFastPieces.Add (6);
            }
            var m1 = picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> ());
            var m2 = picker.PickPiece (peers[1], peers[1].BitField, new List<PeerId> ());
            Assert.AreNotEqual (m1.PieceIndex, m2.PieceIndex, "#1");
        }
    }
}
