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

using MonoTorrent.Client;

using NUnit.Framework;

namespace MonoTorrent.PiecePicking
{
    [TestFixture]
    public class StandardPickerTests
    {
        BitField bitfield;
        PeerId peer;
        List<PeerId> peers;
        IPiecePicker picker;
        TestTorrentManagerInfo torrentData;

        [SetUp]
        public void Setup ()
        {
            int pieceCount = 40;
            int pieceLength = 256 * 1024;
            bitfield = new BitField (pieceCount);
            torrentData = TestTorrentManagerInfo.Create(
                files: TorrentFileInfo.Create (pieceLength, ("File", pieceLength * pieceCount, "Full/Path/File")),
                pieceLength: pieceLength,
                size: pieceLength * pieceCount
            );
            peers = new List<PeerId> ();

            picker = new StandardPicker ();
            picker.Initialise (torrentData);

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
                    Assert.IsTrue (Array.IndexOf (allowedFast, msg.Value.PieceIndex) > -1, "#2." + j);
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

            BlockInfo? request;
            var pieceRequests = new List<BlockInfo> ();
            while ((request = picker.PickPiece (id, id.BitField, new List<PeerId> ())) != null)
                pieceRequests.Add (request.Value);

            var expectedRequests = torrentData.TorrentInfo.BlocksPerPiece(1);
            Assert.AreEqual (expectedRequests * 2, pieceRequests.Count, "#1");
            Assert.IsTrue (pieceRequests.All (r => r.PieceIndex == 1 || r.PieceIndex == 2), "#2");
            for (int i = 0; i < expectedRequests; i++) {
                Assert.AreEqual (2, pieceRequests.Count (t => t.StartOffset == i * Constants.BlockSize && t.RequestLength == Constants.BlockSize), "#2." + i);
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
                    Assert.IsTrue (m.Value.PieceIndex == 2 || m.Value.PieceIndex == 8 || m.Value.PieceIndex == 13 || m.Value.PieceIndex == 21);
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
            Span<BlockInfo> buffer = stackalloc BlockInfo[1];

            bitfield[1] = true;
            Assert.AreEqual (1, picker.PickPiece (peers[0], new BitField (bitfield).Not (), peers, 0, 10, buffer));
            Assert.AreEqual (0, buffer[0].PieceIndex);

            peers[1].IsChoking = false;
            peers[1].BitField.SetAll (true);
            peers[1].RepeatedHashFails = peers[1].TotalHashFails = 1;
            Assert.AreEqual (1, picker.PickPiece (peers[1], new BitField (bitfield).Not (), peers, 0, 10, buffer));
            Assert.AreEqual (2, buffer[0].PieceIndex);
        }

        [Test]
        public void NoInterestingPieces ()
        {
            peer.IsChoking = false;
            Assert.IsNull (picker.PickPiece (peer, new ReadOnlyBitField (torrentData.TorrentInfo.PieceCount ()), peers));
        }

        [Test]
        public void CancelRequests ()
        {
            var messages = new List<BlockInfo> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            BlockInfo? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.PickPiece (peer, peer.BitField, peers);
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#0");
            picker.CancelRequests (peer);

            var messages2 = new HashSet<BlockInfo> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]));
        }

        [Test]
        public void PeerChoked_ReceivedOneBlock ()
        {
            var messages = new List<BlockInfo> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            var otherPeer = peers[1];
            otherPeer.BitField.SetAll (true);

            BlockInfo? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.PickPiece (peer, peer.BitField, peers);
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#0");
            picker.ValidatePiece (peer, new BlockInfo (messages[0].PieceIndex, messages[0].StartOffset, messages[0].RequestLength), out _, out _);
            messages.RemoveAt (0);
            picker.CancelRequests (peer);
            peer.IsChoking = true;

            otherPeer.IsChoking = true;
            Assert.IsNull (picker.PickPiece (otherPeer, otherPeer.BitField, peers));

            otherPeer.IsChoking = false;
            var messages2 = new HashSet<BlockInfo> ();
            while ((m = picker.PickPiece (otherPeer, otherPeer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]));
        }

        [Test]
        public void RepeatedHashFails_CannotContinueExisting ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            var otherPeer = peers[1];
            otherPeer.IsChoking = false;
            otherPeer.BitField.SetAll (true);
            otherPeer.RepeatedHashFails = otherPeer.TotalHashFails = 1;

            // Successfully receive one block, then abandon the piece by disconnecting.
            var request = picker.PickPiece (peer, peer.BitField, peers);
            picker.ValidatePiece (peer, new BlockInfo (request.Value.PieceIndex, request.Value.StartOffset, request.Value.RequestLength), out _, out _);
            request = picker.PickPiece (peer, peer.BitField, peers);
            picker.CancelRequests (peer);

            // Peers involved in repeated hash fails cannot continue incomplete pieces.
            var otherRequest = picker.PickPiece (otherPeer, otherPeer.BitField, peers);
            Assert.AreNotEqual (request.Value.PieceIndex, otherRequest.Value.PieceIndex, "#0");
        }

        [Test]
        public void DoesNotHavePiece_CannotContinueExisting ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            var otherPeer = peers[1];
            otherPeer.IsChoking = false;
            otherPeer.BitField.SetAll (true);

            // Successfully receive one block, then abandon the piece by disconnecting.
            var request = picker.PickPiece (peer, peer.BitField, peers);
            picker.ValidatePiece (peer, new BlockInfo (request.Value.PieceIndex, request.Value.StartOffset, request.Value.RequestLength), out _, out _);
            request = picker.PickPiece (peer, peer.BitField, peers);
            picker.CancelRequests (peer);
            otherPeer.BitField[request.Value.PieceIndex] = false;

            // We cannot request a block if the peer doesn't have it.
            var otherRequest = picker.PickPiece (otherPeer, otherPeer.BitField, peers);
            Assert.AreNotEqual (request.Value.PieceIndex, otherRequest.Value.PieceIndex, "#0");
        }

        [Test]
        public void PeerDisconnected_ReceivedOneBlock ()
        {
            var messages = new List<BlockInfo> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            var otherPeer = peers[1];
            otherPeer.BitField.SetAll (true);

            BlockInfo? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.PickPiece (peer, peer.BitField, peers);
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#0");
            picker.ValidatePiece (peer, new BlockInfo (messages[0].PieceIndex, messages[0].StartOffset, messages[0].RequestLength), out _, out _);
            messages.RemoveAt (0);
            picker.CancelRequests (peer);

            otherPeer.IsChoking = true;
            Assert.IsNull (picker.PickPiece (otherPeer, otherPeer.BitField, peers));

            otherPeer.IsChoking = false;
            var messages2 = new HashSet<BlockInfo> ();
            while ((m = picker.PickPiece (otherPeer, otherPeer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]));
        }

        [Test]
        public void RejectRequests ()
        {
            var messages = new List<BlockInfo> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            BlockInfo? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            foreach (BlockInfo message in messages)
                picker.RequestRejected (peer, message);

            var messages2 = new HashSet<BlockInfo> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void PeerChoked ()
        {
            var messages = new List<BlockInfo> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            BlockInfo? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.CancelRequests (peer);

            var messages2 = new HashSet<BlockInfo> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void ChokeThenClose ()
        {
            var messages = new List<BlockInfo> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            peer.SupportsFastPeer = true;

            BlockInfo? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.CancelRequests (peer);

            var messages2 = new HashSet<BlockInfo> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages2.Add (m.Value);

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void RequestBlocks_50 ()
        {
            Span<BlockInfo> buffer = stackalloc BlockInfo[50];
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var b = picker.PickPiece (peer, peer.BitField, peers, buffer);
            Assert.AreEqual (50, b, "#1");
        }

        [Test]
        public void RequestBlocks_All ()
        {
            Span<BlockInfo> buffer = stackalloc BlockInfo[torrentData.TotalBlocks];
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var b = picker.PickPiece (peer, peer.BitField, peers, buffer);
            Assert.AreEqual (torrentData.TotalBlocks, b, "#1");
        }

        [Test]
        public void RequestBlocks_TooMany ()
        {
            Span<BlockInfo> buffer = stackalloc BlockInfo[torrentData.TotalBlocks * 2];
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var b = picker.PickPiece (peer, peer.BitField, peers, buffer);
            Assert.AreEqual (torrentData.TotalBlocks, b, "#1");
        }

        [Test]
        public void InvalidFastPiece ()
        {
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange (new[] { 1, 2, 5, 55, 62, 235, 42624 });
            peer.BitField.SetAll (true);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0) * 3; i++) {
                var m = picker.PickPiece (peer, peer.BitField, peers).Value;
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
            Assert.IsTrue (picker.ValidatePiece (peer, new BlockInfo (message.Value.PieceIndex, message.Value.StartOffset, message.Value.RequestLength), out bool pieceComplete, out IList<IPeer> peersInvolved), "#1");
            picker.CancelRequests (peer);
            for (int i = 1; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++) {
                message = picker.PickPiece (peer, peer.BitField, peers);
                Assert.IsTrue (picker.ValidatePiece (peer, new BlockInfo (message.Value.PieceIndex, message.Value.StartOffset, message.Value.RequestLength), out pieceComplete, out peersInvolved), "#2." + i);
            }
            Assert.IsTrue (pieceComplete, "#3");
        }

        [Test]
        public void DoesntHaveFastPiece ()
        {
            Span<BlockInfo> buffer = stackalloc BlockInfo[1];
            peer.IsChoking = true;
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange (new[] { 1, 2, 3, 4 });
            peer.BitField.SetAll (true);
            picker = new StandardPicker ();
            picker.Initialise (torrentData);
            var requested = picker.PickPiece (peer, new ReadOnlyBitField (peer.BitField.Length), peers, 0, peer.BitField.Length - 1, buffer);
            Assert.AreEqual (0, requested);
        }


        [Test]
        public void DoesntHaveSuggestedPiece ()
        {
            Span<BlockInfo> buffer = stackalloc BlockInfo[1];
            peer.IsChoking = false;
            peer.SupportsFastPeer = true;
            peer.SuggestedPieces.AddRange (new[] { 1, 2, 3, 4 });
            peer.BitField.SetAll (true);
            picker = new StandardPicker ();
            picker.Initialise (torrentData);
            var requested = picker.PickPiece (peer, new ReadOnlyBitField (peer.BitField.Length), peers, 0, peer.BitField.Length - 1, buffer);
            Assert.AreEqual (0, requested);
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
        public void CreateFinalPiece ()
        {
            var piece = new StandardPicker.Piece (0, Constants.BlockSize * 3 + 1);
            Assert.AreEqual (4, piece.BlockCount);

            Assert.AreEqual (Constants.BlockSize * 3, piece.Blocks[3].StartOffset);
            Assert.AreEqual (1, piece.Blocks[3].RequestLength);
        }

        [Test]
        public void CreateNormalPiece ()
        {
            var piece = new StandardPicker.Piece (0, Constants.BlockSize * 3);
            Assert.AreEqual (3, piece.BlockCount);

            Assert.AreEqual (Constants.BlockSize * 0, piece.Blocks[0].StartOffset);
            Assert.AreEqual (Constants.BlockSize * 1, piece.Blocks[0].RequestLength);

            Assert.AreEqual (Constants.BlockSize * 1, piece.Blocks[1].StartOffset);
            Assert.AreEqual (Constants.BlockSize * 1, piece.Blocks[1].RequestLength);

            Assert.AreEqual (Constants.BlockSize * 2, piece.Blocks[2].StartOffset);
            Assert.AreEqual (Constants.BlockSize * 1, piece.Blocks[2].RequestLength);
        }

        [Test]
        public void CreatePiece_NotMultipleOf16KB ()
        {
            var totalSize = 2318336;
            var piece = new StandardPicker.Piece (0, totalSize);
            Assert.AreEqual (142, piece.BlockCount);

            for (int i = 0; i < piece.BlockCount - 1; i++)
                Assert.AreEqual (Constants.BlockSize, piece[i].RequestLength);
            Assert.AreEqual (totalSize - (Constants.BlockSize * 141), piece[141].RequestLength);
        }

        [Test]
        public void PickBundle ()
        {
            Span<BlockInfo> buffer = stackalloc BlockInfo[torrentData.TorrentInfo.BlocksPerPiece (0) * 5];
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            int requested;
            var messages = new List<BlockInfo> ();

            while ((requested = picker.PickPiece (peer, peer.BitField, peers, buffer)) > 0) {
                Assert.IsTrue (requested == torrentData.TorrentInfo.BlocksPerPiece (0) * 5
                              || (requested + messages.Count) == torrentData.TotalBlocks, "#1");
                for (int i = 0; i < requested; i++)
                    messages.Add (buffer[i]);
            }
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#2");
        }

        [Test]
        public void PickBundle_2 ()
        {
            peer.IsChoking = false;

            for (int i = 0; i < 7; i++)
                peer.BitField[i] = true;

            int requested;
            var messages = new List<BlockInfo> ();

            Span<BlockInfo> buffer = stackalloc BlockInfo[torrentData.TorrentInfo.BlocksPerPiece (0) * 7];
            while ((requested = picker.PickPiece (peer, peer.BitField, peers, buffer)) > 0) {
                Assert.IsTrue (requested == torrentData.TorrentInfo.BlocksPerPiece (0) * 5
                              || (requested + messages.Count) == torrentData.TorrentInfo.BlocksPerPiece (0) * 7, "#1");
                for (int i = 0; i < requested; i++)
                    messages.Add (buffer[i]);
            }
            Assert.AreEqual (torrentData.TorrentInfo.BlocksPerPiece (0) * 7, messages.Count, "#2");
        }

        [Test]
        public void PickBundle_3 ()
        {
            var messages = new List<BlockInfo> ();
            peers[2].IsChoking = false;
            peers[2].BitField.SetAll (true);
            messages.Add (picker.PickPiece (peers[2], peers[2].BitField, peers).Value);

            peer.IsChoking = false;

            for (int i = 0; i < 7; i++)
                peer.BitField[i] = true;

            int requested;
            BlockInfo? request;

            Span<BlockInfo> buffer = stackalloc BlockInfo[torrentData.TorrentInfo.BlocksPerPiece (0) * 5];
            while ((requested = picker.PickPiece (peer, peer.BitField, peers, buffer)) > 0) {
                for (int i = 0; i < requested; i++)
                    messages.Add (buffer[i]);
            }
            while ((request = picker.ContinueAnyExistingRequest (peer, 0, bitfield.Length - 1)) != null)
                messages.Add (request.Value);

            Assert.AreEqual (torrentData.TorrentInfo.BlocksPerPiece (0) * 7, messages.Count, "#2");
        }

        [Test]
        public void PickBundle4 ()
        {
            Span<BlockInfo> buffer = stackalloc BlockInfo[1];
            peers[0].IsChoking = false;
            peers[0].BitField.SetAll (true);

            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 4, 4, buffer);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 6, 6, buffer);

            buffer = stackalloc BlockInfo[20 * torrentData.TorrentInfo.BlocksPerPiece (0)];
            var b = picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), buffer);
            for (int i = 0; i < b; i++)
                Assert.IsTrue (buffer[i].PieceIndex > 6);
        }

        [Test]
        public void Pick20SequentialPieces ()
        {
            // As we want to pick 20 pieces, we ignore the first 5 available and choose from the group of 20.
            foreach (var i in Enumerable.Range (0, 5).Concat (Enumerable.Range (10, 20)))
                bitfield[i] = true;

            peers[0].IsChoking = false;

            Span<BlockInfo> buffer = stackalloc BlockInfo[20 * torrentData.TorrentInfo.BlocksPerPiece (0)];
            var b = picker.PickPiece (peers[0], bitfield, new List<PeerId> (), buffer);
            Assert.AreEqual (20 * torrentData.TorrentInfo.BlocksPerPiece (0), b);
            for (int i = 0; i < b; i++)
                Assert.IsTrue (buffer[i].PieceIndex >= 10 && buffer[i].PieceIndex < 30);
        }

        [Test]
        public void PickBundle6 ()
        {
            bitfield.SetAll (false);

            peers[0].IsChoking = false;
            peers[0].BitField.SetAll (true);

            Span<BlockInfo> buffer = stackalloc BlockInfo[1];
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 0, 0, buffer);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 1, 1, buffer);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 3, 3, buffer);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), 6, 6, buffer);

            buffer = stackalloc BlockInfo[2 * torrentData.TorrentInfo.BlocksPerPiece (0)];
            var b = picker.PickPiece (peers[0], peers[0].BitField, new List<PeerId> (), buffer);
            Assert.AreEqual (2 * torrentData.TorrentInfo.BlocksPerPiece (0), b);
            for (int i = 0; i < b; i++)
                Assert.IsTrue (buffer[i].PieceIndex >= 4 && buffer[i].PieceIndex < 6);
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
            var m1 = picker.PickPiece (peers[0], peers[0].BitField).Value;
            var m2 = picker.PickPiece (peers[1], peers[1].BitField).Value;
            Assert.AreNotEqual (m1.PieceIndex, m2.PieceIndex, "#1");
        }

        [Test]
        public void DupeRequests_PickSameBlockTwiceWhenAllRequested ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peers[0].BitField.SetAll (false).Set (3, true);

            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder1, 0, bitfield.Length));

            BlockInfo? req;
            var requests1 = new List<BlockInfo> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests1.Add (req.Value);

            // There are no pieces owned by this peer, so there's nothing to continue.
            Assert.IsNull (picker.ContinueExistingRequest (seeder2, 0, bitfield.Length));

            // Every block has been requested once and no duplicates are allowed.
            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder2, 0, bitfield.Length));

            // Every block has been requested once and no duplicates are allowed.
            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder2, 0, bitfield.Length, 1));

            var requests2 = new List<BlockInfo> ();
            while ((req = picker.ContinueAnyExistingRequest (seeder2, 0, bitfield.Length, 2)) != null)
                requests2.Add (req.Value);

            CollectionAssert.AreEquivalent (requests1, requests2);
        }

        [Test]
        public void DupeRequests_ValidateDupeThenPrimary ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peers[0].BitField.SetAll (false).Set (3, true);

            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder1, 0, bitfield.Length));

            BlockInfo? req;
            var requests1 = new List<BlockInfo> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests1.Add (req.Value);

            // This piece has been requested by both peers now.
            BlockInfo request = picker.ContinueAnyExistingRequest (seeder2, 0, bitfield.Length, 2).Value;

            // Validate the duplicate request first.
            Assert.IsTrue (picker.ValidatePiece (seeder2, request, out _, out _));
            // Now the primary will be discarded as we already received the block
            Assert.IsFalse (picker.ValidatePiece (seeder1, request, out _, out _));
        }

        [Test]
        public void DupeRequests_ValidatePrimaryThenDupe ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peers[0].BitField.SetAll (false).Set (3, true);

            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder1, 0, bitfield.Length));

            BlockInfo? req;
            var requests1 = new List<BlockInfo> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests1.Add (req.Value);

            // This piece has been requested by both peers now.
            BlockInfo request = picker.ContinueAnyExistingRequest (seeder2, 0, bitfield.Length, 2).Value;

            // Validate the primary request first
            Assert.IsTrue (picker.ValidatePiece (seeder1, request, out _, out _));
            // Now the duplicate will be discarded as we've already received the primary request.
            Assert.IsFalse (picker.ValidatePiece (seeder2, request, out _, out _));
        }

        [Test]
        public void DupeRequests_FinalBlock_ValidatePrimaryThenDupe ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peers[0].BitField.SetAll (false).Set (3, true);

            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder1, 0, bitfield.Length));

            BlockInfo? req;
            var requests = new List<BlockInfo> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests.Add (req.Value);
            for (int i = 0; i < requests.Count; i++)
                if (i != 2)
                    Assert.IsTrue (picker.ValidatePiece (seeder1, requests[i], out _, out _));

            // This should be the final unrequested block
            BlockInfo request = picker.ContinueAnyExistingRequest (seeder2, 0, bitfield.Length, 2).Value;
            Assert.AreEqual (requests[2], request);

            // Validate the primary request first
            Assert.IsTrue (picker.ValidatePiece (seeder1, request, out var complete, out var peersInvolved));
            Assert.IsTrue (complete);
            CollectionAssert.AreEqual (new[] { seeder1 }, peersInvolved);
            Assert.AreEqual (0, seeder1.AmRequestingPiecesCount);
            Assert.AreEqual (0, seeder2.AmRequestingPiecesCount);

            // Now the duplicate will be discarded as we've already received the primary request.
            Assert.IsFalse (picker.ValidatePiece (seeder2, request, out complete, out peersInvolved));
            Assert.IsFalse (complete);
            Assert.IsEmpty (peersInvolved);
        }

        [Test]
        public void DupeRequests_FinalBlock_ValidateDupleThenPrimary ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peers[0].BitField.SetAll (false).Set (3, true);

            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder1, 0, bitfield.Length));

            BlockInfo? req;
            var requests = new List<BlockInfo> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests.Add (req.Value);
            for (int i = 0; i < requests.Count; i++)
                if (i != 2)
                    Assert.IsTrue (picker.ValidatePiece (seeder1, requests[i], out _, out _));

            // This should be the final unrequested block
            BlockInfo request = picker.ContinueAnyExistingRequest (seeder2, 0, bitfield.Length, 2).Value;
            Assert.AreEqual (requests[2], request);

            // Validate the dupe request first
            Assert.IsTrue (picker.ValidatePiece (seeder2, request, out bool complete, out var peersInvolved));
            Assert.IsTrue (complete);
            CollectionAssert.AreEqual (new[] { seeder1, seeder2 }, peersInvolved);
            Assert.AreEqual (0, seeder1.AmRequestingPiecesCount);
            Assert.AreEqual (0, seeder2.AmRequestingPiecesCount);

            // Now the primary will be discarded as we've already received the primary request.
            Assert.IsFalse (picker.ValidatePiece (seeder1, request, out complete, out peersInvolved));
            Assert.IsFalse (complete);
            Assert.IsEmpty (peersInvolved);
        }

        [Test]
        public void DupeRequests_PeerCannotDuplicateOwnRequest ()
        {
            var seeder = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = new BitField (seeder.BitField).SetAll (false).Set (3, true);

            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder, 0, bitfield.Length - 1));

            BlockInfo? req;
            var requests = new List<BlockInfo> ();
            while ((req = picker.PickPiece (seeder, singlePiece)) != null)
                requests.Add (req.Value);

            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder, 0, bitfield.Length - 1));
            Assert.IsNull (picker.ContinueAnyExistingRequest (seeder, 0, bitfield.Length - 1, 2));
        }

        [Test]
        public void DupeRequests_CanRequestInTriplicate ()
        {
            var seeders = new IPeer[] {
                PeerId.CreateNull (bitfield.Length, true, false, true),
                PeerId.CreateNull (bitfield.Length, true, false, true),
                PeerId.CreateNull (bitfield.Length, true, false, true),
            };

            var queue = new Queue<IPeer> (seeders);
            var requests = seeders.ToDictionary (t => t, t => new List<BlockInfo> ());
            var singlePiece = new BitField (seeders[0].BitField).SetAll (false).Set (3, true);

            // Request an entire piece using 1 peer first to ensure we have collisions when
            // issuing duplicates. In the end all peers should have the same set though.
            while (true) {
                var req = picker.PickPiece (seeders[0], singlePiece)
                       ?? picker.ContinueAnyExistingRequest (seeders[0], 0, bitfield.Length - 1, 3);
                if (req.HasValue)
                    requests[seeders[0]].Add (req.Value);
                else
                    break;
            }
            Assert.AreEqual (torrentData.TorrentInfo.BlocksPerPiece (0), requests[seeders[0]].Count);

            while (queue.Count > 0) {
                var seeder = queue.Dequeue ();
                var req = picker.PickPiece (seeder, singlePiece)
                       ?? picker.ContinueAnyExistingRequest (seeder, 0, bitfield.Length - 1, 3);

                if (req.HasValue) {
                    queue.Enqueue (seeder);
                    requests[seeder].Add (req.Value);
                }
            }

            CollectionAssert.AreEquivalent (requests[seeders[0]], requests[seeders[1]]);
            CollectionAssert.AreEquivalent (requests[seeders[1]], requests[seeders[2]]);
            Assert.AreEqual (torrentData.TorrentInfo.BlocksPerPiece (0), requests.Values.First ().Count);
        }
    }
}
