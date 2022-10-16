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
        ReadOnlyBitField[] peers;
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
            peers = new ReadOnlyBitField[20];

            picker = new StandardPicker ();
            picker.Initialise (torrentData);

            peer = PeerId.CreateNull (pieceCount);
            peer.SupportsFastPeer = true;
            for (int i = 0; i < 20; i++) {
                peers[i] = new BitField (pieceCount);
            }
        }

        [Test]
        public void RequestFastSeeder ()
        {
            int[] allowedFast = { 1, 2, 3, 5, 8, 13, 21 };
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange ((int[]) allowedFast.Clone ());

            peer.BitField.SetAll (true); // Lets pretend he has everything
            for (int i = 0; i < 7; i++) {
                for (int j = 0; j < 16; j++) {
                    var msg = picker.PickPiece (peer, peer.BitField, peers);
                    Assert.IsNotNull (msg, "#1." + j);
                    Assert.IsTrue (Array.IndexOf (allowedFast, msg.Value.PieceIndex) > -1, "#2." + j);
                }
            }
            Assert.IsNull (picker.PickPiece (peer, peer.BitField, peers));
        }

        [Test]
        public void RequestEntireFastPiece ()
        {
            var id = peer;
            int[] allowedFast = { 1, 2 };
            id.SupportsFastPeer = true;
            id.IsAllowedFastPieces.AddRange ((int[]) allowedFast.Clone ());
            id.BitField.SetAll (true); // Lets pretend he has everything

            PieceSegment? request;
            var pieceRequests = new List<PieceSegment> ();
            while ((request = picker.PickPiece (id, id.BitField, Array.Empty<ReadOnlyBitField> ())) != null)
                pieceRequests.Add (request.Value);

            var expectedRequests = torrentData.TorrentInfo.BlocksPerPiece(1);
            Assert.AreEqual (expectedRequests * 2, pieceRequests.Count, "#1");
            Assert.IsTrue (pieceRequests.All (r => r.PieceIndex == 1 || r.PieceIndex == 2), "#2");
            for (int i = 0; i < expectedRequests; i++) {
                Assert.AreEqual (2, pieceRequests.Count (t => t.BlockIndex == i), "#2." + i);
            }
        }

        [Test]
        public void RequestFastNotSeeder ()
        {
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange (new[] { 1, 2, 3, 5, 8, 13, 21 });

            peer.BitField.SetAll (true);
            peer.BitField[1] = false;
            peer.BitField[3] = false;
            peer.BitField[5] = false;

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 16; j++) {
                    var m = picker.PickPiece (peer, peer.BitField, peers);
                    Assert.IsTrue (m.Value.PieceIndex == 2 || m.Value.PieceIndex == 8 || m.Value.PieceIndex == 13 || m.Value.PieceIndex == 21);
                }

            Assert.IsNull (picker.PickPiece (peer, peer.BitField, peers));
        }

        [Test]
        public void RequestChoked ()
        {
            Assert.IsNull (picker.PickPiece (peer, peer.BitField, peers));
        }

        [Test]
        public void StandardPicker_PickStandardPiece ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];

            bitfield[1] = true;
            Assert.AreEqual (1, picker.PickPiece (peer, new BitField (bitfield).Not (), peers, 0, 10, buffer));
            Assert.AreEqual (0, buffer[0].PieceIndex);

            var other = PeerId.CreateNull (peer.BitField.Length);
            other.IsChoking = false;
            other.BitField.SetAll (true);
            other.RepeatedHashFails = other.TotalHashFails = 1;
            Assert.AreEqual (1, picker.PickPiece (other, new BitField (bitfield).Not (), peers, 0, 10, buffer));
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
            var messages = new List<PieceSegment> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            PieceSegment? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.PickPiece (peer, peer.BitField, peers);
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#0");
            picker.CancelRequests (peer, 0, peer.BitField.Length - 1, stackalloc PieceSegment[peer.AmRequestingPiecesCount]);

            var messages2 = new HashSet<PieceSegment> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]));
        }

        [Test]
        public void PeerChoked_ReceivedOneBlock ()
        {
            var messages = new List<PieceSegment> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            var otherPeer = PeerId.CreateNull (peer.BitField.Length);
            otherPeer.BitField.SetAll (true);

            PieceSegment? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.PickPiece (peer, peer.BitField, peers);
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#0");
            picker.ValidatePiece (peer, messages[0], out _, new HashSet<IRequester> ());
            messages.RemoveAt (0);
            picker.CancelRequests (peer, 0, peer.BitField.Length - 1, stackalloc PieceSegment[peer.AmRequestingPiecesCount]);
            peer.IsChoking = true;

            otherPeer.IsChoking = true;
            Assert.IsNull (picker.PickPiece (otherPeer, otherPeer.BitField, peers));

            otherPeer.IsChoking = false;
            var messages2 = new HashSet<PieceSegment> ();
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

            var otherPeer = PeerId.CreateNull (peer.BitField.Length);
            otherPeer.IsChoking = false;
            otherPeer.BitField.SetAll (true);
            otherPeer.RepeatedHashFails = otherPeer.TotalHashFails = 1;

            // Successfully receive one block, then abandon the piece by disconnecting.
            var request = picker.PickPiece (peer, peer.BitField, peers);
            picker.ValidatePiece (peer, request.Value, out _, new HashSet<IRequester> ());
            request = picker.PickPiece (peer, peer.BitField, peers);
            picker.CancelRequests (peer, 0, peer.BitField.Length - 1, stackalloc PieceSegment[peer.AmRequestingPiecesCount]);

            // Peers involved in repeated hash fails cannot continue incomplete pieces.
            var otherRequest = picker.PickPiece (otherPeer, otherPeer.BitField, peers);
            Assert.AreNotEqual (request.Value.PieceIndex, otherRequest.Value.PieceIndex, "#0");
        }

        [Test]
        public void DoesNotHavePiece_CannotContinueExisting ()
        {
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            var otherPeer = PeerId.CreateNull (peer.BitField.Length);
            otherPeer.IsChoking = false;
            otherPeer.BitField.SetAll (true);

            // Successfully receive one block, then abandon the piece by disconnecting.
            var request = picker.PickPiece (peer, peer.BitField, peers);
            picker.ValidatePiece (peer, request.Value, out _, new HashSet<IRequester> ());
            request = picker.PickPiece (peer, peer.BitField, peers);
            picker.CancelRequests (peer, 0, peer.BitField.Length - 1, stackalloc PieceSegment[peer.AmRequestingPiecesCount]);
            otherPeer.BitField[request.Value.PieceIndex] = false;

            // We cannot request a block if the peer doesn't have it.
            var otherRequest = picker.PickPiece (otherPeer, otherPeer.BitField, peers);
            Assert.AreNotEqual (request.Value.PieceIndex, otherRequest.Value.PieceIndex, "#0");
        }

        [Test]
        public void PeerDisconnected_ReceivedOneBlock ()
        {
            var messages = new List<PieceSegment> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            var otherPeer = PeerId.CreateNull (peer.BitField.Length);
            otherPeer.BitField.SetAll (true);

            PieceSegment? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.PickPiece (peer, peer.BitField, peers);
            Assert.AreEqual (torrentData.TotalBlocks, messages.Count, "#0");
            picker.ValidatePiece (peer, messages[0], out _, new HashSet<IRequester> ());
            messages.RemoveAt (0);
            picker.CancelRequests (peer, 0, peer.BitField.Length - 1, stackalloc PieceSegment[peer.AmRequestingPiecesCount]);

            otherPeer.IsChoking = true;
            Assert.IsNull (picker.PickPiece (otherPeer, otherPeer.BitField, peers));

            otherPeer.IsChoking = false;
            var messages2 = new HashSet<PieceSegment> ();
            while ((m = picker.PickPiece (otherPeer, otherPeer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]));
        }

        [Test]
        public void RejectRequests ()
        {
            var messages = new List<PieceSegment> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            PieceSegment? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            foreach (PieceSegment message in messages)
                picker.RequestRejected (peer, message);

            var messages2 = new HashSet<PieceSegment> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void PeerChoked ()
        {
            var messages = new List<PieceSegment> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            PieceSegment? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.CancelRequests (peer, 0, peer.BitField.Length - 1, stackalloc PieceSegment[peer.AmRequestingPiecesCount]);

            var messages2 = new HashSet<PieceSegment> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                Assert.IsTrue (messages2.Add (m.Value));

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void ChokeThenClose ()
        {
            var messages = new List<PieceSegment> ();
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            peer.SupportsFastPeer = true;

            PieceSegment? m;
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages.Add (m.Value);

            picker.CancelRequests (peer, 0, peer.BitField.Length - 1, stackalloc PieceSegment[peer.AmRequestingPiecesCount]);

            var messages2 = new HashSet<PieceSegment> ();
            while ((m = picker.PickPiece (peer, peer.BitField, peers)) != null)
                messages2.Add (m.Value);

            Assert.AreEqual (messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue (messages2.Contains (messages[i]), "#2." + i);
        }

        [Test]
        public void RequestBlocks_50 ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[50];
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var b = picker.PickPiece (peer, peer.BitField, peers, buffer);
            Assert.AreEqual (50, b, "#1");
        }

        [Test]
        public void RequestBlocks_All ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[torrentData.TotalBlocks];
            peer.IsChoking = false;
            peer.BitField.SetAll (true);
            var b = picker.PickPiece (peer, peer.BitField, peers, buffer);
            Assert.AreEqual (torrentData.TotalBlocks, b, "#1");
        }

        [Test]
        public void RequestBlocks_TooMany ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[torrentData.TotalBlocks * 2];
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
            Assert.IsTrue (picker.ValidatePiece (peer, message.Value, out bool pieceComplete, new HashSet<IRequester> ()), "#1");
            picker.CancelRequests (peer, 0, peer.BitField.Length - 1, stackalloc PieceSegment[peer.AmRequestingPiecesCount]);
            for (int i = 1; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++) {
                message = picker.PickPiece (peer, peer.BitField, peers);
                Assert.IsTrue (picker.ValidatePiece (peer, message.Value, out pieceComplete, new HashSet<IRequester> ()), "#2." + i);
            }
            Assert.IsTrue (pieceComplete, "#3");
        }

        [Test]
        public void DoesntHaveFastPiece ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
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
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
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
        public void PickBundle ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[torrentData.TorrentInfo.BlocksPerPiece (0) * 5];
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            int requested;
            var messages = new List<PieceSegment> ();

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
            var messages = new List<PieceSegment> ();

            Span<PieceSegment> buffer = stackalloc PieceSegment[torrentData.TorrentInfo.BlocksPerPiece (0) * 7];
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
            var messages = new List<PieceSegment> ();
            var otherPeer = PeerId.CreateNull (peer.BitField.Length);
            otherPeer.IsChoking = false;
            otherPeer.BitField.SetAll (true);
            messages.Add (picker.PickPiece (otherPeer, otherPeer.BitField, peers).Value);

            peer.IsChoking = false;

            for (int i = 0; i < 7; i++)
                peer.BitField[i] = true;

            int requested;

            Span<PieceSegment> buffer = stackalloc PieceSegment[torrentData.TorrentInfo.BlocksPerPiece (0) * 5];
            while ((requested = picker.PickPiece (peer, peer.BitField, peers, buffer)) > 0) {
                for (int i = 0; i < requested; i++)
                    messages.Add (buffer[i]);
            }
            while (picker.ContinueAnyExistingRequest (peer, peer.BitField, 0, bitfield.Length - 1, out PieceSegment segment))
                messages.Add (segment);

            Assert.AreEqual (torrentData.TorrentInfo.BlocksPerPiece (0) * 7, messages.Count, "#2");
        }

        [Test]
        public void PickBundle4 ()
        {
            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peer, peer.BitField, Array.Empty<ReadOnlyBitField> (), 4, 4, buffer);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peer, peer.BitField, Array.Empty<ReadOnlyBitField> (), 6, 6, buffer);

            buffer = stackalloc PieceSegment[20 * torrentData.TorrentInfo.BlocksPerPiece (0)];
            var b = picker.PickPiece (peer, peer.BitField, Array.Empty<ReadOnlyBitField> (), buffer);
            for (int i = 0; i < b; i++)
                Assert.IsTrue (buffer[i].PieceIndex > 6);
        }

        [Test]
        public void Pick20SequentialPieces ()
        {
            // As we want to pick 20 pieces, we ignore the first 5 available and choose from the group of 20.
            foreach (var i in Enumerable.Range (0, 5).Concat (Enumerable.Range (10, 20)))
                bitfield[i] = true;

            peer.IsChoking = false;

            Span<PieceSegment> buffer = stackalloc PieceSegment[20 * torrentData.TorrentInfo.BlocksPerPiece (0)];
            var b = picker.PickPiece (peer, bitfield, Array.Empty<ReadOnlyBitField> (), buffer);
            Assert.AreEqual (20 * torrentData.TorrentInfo.BlocksPerPiece (0), b);
            for (int i = 0; i < b; i++)
                Assert.IsTrue (buffer[i].PieceIndex >= 10 && buffer[i].PieceIndex < 30);
        }

        [Test]
        public void PickBundle6 ()
        {
            bitfield.SetAll (false);

            peer.IsChoking = false;
            peer.BitField.SetAll (true);

            Span<PieceSegment> buffer = stackalloc PieceSegment[1];
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peer, peer.BitField, Array.Empty<ReadOnlyBitField> (), 0, 0, buffer);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peer, peer.BitField, Array.Empty<ReadOnlyBitField> (), 1, 1, buffer);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peer, peer.BitField, Array.Empty<ReadOnlyBitField> (), 3, 3, buffer);
            for (int i = 0; i < torrentData.TorrentInfo.BlocksPerPiece (0); i++)
                picker.PickPiece (peer, peer.BitField, Array.Empty<ReadOnlyBitField> (), 6, 6, buffer);

            buffer = stackalloc PieceSegment[2 * torrentData.TorrentInfo.BlocksPerPiece (0)];
            var b = picker.PickPiece (peer, peer.BitField, Array.Empty<ReadOnlyBitField> (), buffer);
            Assert.AreEqual (2 * torrentData.TorrentInfo.BlocksPerPiece (0), b);
            for (int i = 0; i < b; i++)
                Assert.IsTrue (buffer[i].PieceIndex >= 4 && buffer[i].PieceIndex < 6);
        }

        [Test]
        public void FastPieceTest ()
        {
            var fastPeers = new PeerId[2];
            for (int i = 0; i < 2; i++) {
                fastPeers[i] = PeerId.CreateNull (peer.BitField.Length); 
                fastPeers[i].BitField.SetAll (true);
                fastPeers[i].SupportsFastPeer = true;
                fastPeers[i].IsAllowedFastPieces.Add (5);
                fastPeers[i].IsAllowedFastPieces.Add (6);
            }
            var m1 = picker.PickPiece (fastPeers[0], fastPeers[0].BitField).Value;
            var m2 = picker.PickPiece (fastPeers[1], fastPeers[1].BitField).Value;
            Assert.AreNotEqual (m1.PieceIndex, m2.PieceIndex, "#1");
        }

        [Test]
        public void DupeRequests_PickSameBlockTwiceWhenAllRequested ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peer.BitField.SetAll (false).Set (3, true);

            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder1, seeder1.BitField, 0, bitfield.Length, out _));

            PieceSegment? req;
            var requests1 = new List<PieceSegment> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests1.Add (req.Value);

            // There are no pieces owned by this peer, so there's nothing to continue.
            Assert.IsFalse (picker.ContinueExistingRequest (seeder2, 0, bitfield.Length, out _));

            // Every block has been requested once and no duplicates are allowed.
            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder2, seeder2.BitField,  0, bitfield.Length, out _));

            // Every block has been requested once and no duplicates are allowed.
            Assert.False (picker.ContinueAnyExistingRequest (seeder2, seeder2.BitField, 0, bitfield.Length, 1, out _));

            var requests2 = new List<PieceSegment> ();
            while (picker.ContinueAnyExistingRequest (seeder2, seeder2.BitField, 0, bitfield.Length, 2, out PieceSegment segment))
                requests2.Add (segment);

            CollectionAssert.AreEquivalent (requests1, requests2);
        }

        [Test]
        public void DupeRequests_ValidateDupeThenPrimary ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peer.BitField.SetAll (false).Set (3, true);

            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder1, seeder1.BitField, 0, bitfield.Length, out _));

            PieceSegment? req;
            var requests1 = new List<PieceSegment> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests1.Add (req.Value);

            // This piece has been requested by both peers now.
            Assert.IsTrue (picker.ContinueAnyExistingRequest (seeder2, seeder2.BitField, 0, bitfield.Length, 2, out PieceSegment request));

            // Validate the duplicate request first.
            Assert.IsTrue (picker.ValidatePiece (seeder2, request, out _, new HashSet<IRequester> ()));
            // Now the primary will be discarded as we already received the block
            Assert.IsFalse (picker.ValidatePiece (seeder1, request, out _, new HashSet<IRequester> ()));
        }

        [Test]
        public void RequestFromPrimaryAfterCancellingDuplicates ()
        {
            // Request a piece using two peers. Cancel requests from the first peer.
            // Validate the piece using the second peer. Request a piece from the first peer.
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peer.BitField.SetAll (false).Set (3, true);

            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder1, seeder1.BitField, 0, bitfield.Length, out _));

            PieceSegment? req;
            var requests1 = new List<PieceSegment> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests1.Add (req.Value);

            var requests2 = new List<PieceSegment> ();
            while (picker.ContinueAnyExistingRequest (seeder2, seeder2.BitField, 0, 7, 2, out PieceSegment segment))
                requests2.Add (segment);

            Assert.AreEqual (requests1.Count, requests2.Count);

            Assert.AreEqual (requests1.Count, picker.CancelRequests (seeder1, 0, 7, new PieceSegment[requests1.Count]));

            foreach (var r in requests2)
                Assert.IsTrue (picker.ValidatePiece (seeder2, r, out _, new HashSet<IRequester> ()));

            picker.PickPiece (seeder1, seeder1.BitField);
        }

        [Test]
        public void DupeRequests_ValidatePrimaryThenDupe ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peer.BitField.SetAll (false).Set (3, true);

            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder1, seeder1.BitField, 0, bitfield.Length, out _));

            PieceSegment? req;
            var requests1 = new List<PieceSegment> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests1.Add (req.Value);

            // This piece has been requested by both peers now.
            Assert.IsTrue (picker.ContinueAnyExistingRequest (seeder2, seeder2.BitField, 0, bitfield.Length, 2, out PieceSegment request));

            // Validate the primary request first
            Assert.IsTrue (picker.ValidatePiece (seeder1, request, out _, new HashSet<IRequester> ()));
            // Now the duplicate will be discarded as we've already received the primary request.
            Assert.IsFalse (picker.ValidatePiece (seeder2, request, out _, new HashSet<IRequester> ()));
        }

        [Test]
        public void DupeRequests_SecondaryFulfillsAllRequests_ThenRequestFromPrimary ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peer.BitField.SetAll (false).Set (3, true);


            PieceSegment? req;
            var requests1 = new List<PieceSegment> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests1.Add (req.Value);

            var requests2 = new List<PieceSegment> ();
            while (picker.ContinueAnyExistingRequest (seeder2, singlePiece, 0, seeder2.BitField.Length - 1, 2, out PieceSegment request))
                requests2.Add (request);

            Assert.AreEqual (requests1.Count, requests2.Count);
            Assert.IsTrue (requests1.All (t => t.PieceIndex == 3));
            Assert.IsTrue (requests2.All (t => t.PieceIndex == 3));

            // Validate the secondary requests first
            foreach (var v in requests2)
                picker.ValidatePiece (seeder2, v, out _, new HashSet<IRequester> ());

            Assert.AreEqual (3, picker.PickPiece (seeder1, singlePiece).Value.PieceIndex);
        }

        [Test]
        public void DupeRequests_FinalBlock_ValidatePrimaryThenDupe ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peer.BitField.SetAll (false).Set (3, true);

            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder1, seeder1.BitField, 0, bitfield.Length, out _));

            PieceSegment? req;
            var requests = new List<PieceSegment> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests.Add (req.Value);
            for (int i = 0; i < requests.Count; i++)
                if (i != 2)
                    Assert.IsTrue (picker.ValidatePiece (seeder1, requests[i], out _, new HashSet<IRequester> ()));

            // This should be the final unrequested block
            Assert.IsTrue (picker.ContinueAnyExistingRequest (seeder2, seeder2.BitField, 0, bitfield.Length, 2, out PieceSegment request));
            Assert.AreEqual (requests[2], request);

            // Validate the primary request first
            var peersInvolved = new HashSet<IRequester> ();
            Assert.IsTrue (picker.ValidatePiece (seeder1, request, out var complete, peersInvolved));
            Assert.IsTrue (complete);
            CollectionAssert.AreEqual (new[] { seeder1 }, peersInvolved);
            Assert.AreEqual (0, seeder1.AmRequestingPiecesCount);
            Assert.AreEqual (0, seeder2.AmRequestingPiecesCount);

            // Now the duplicate will be discarded as we've already received the primary request.
            peersInvolved.Clear ();
            Assert.IsFalse (picker.ValidatePiece (seeder2, request, out complete, peersInvolved));
            Assert.IsFalse (complete);
            Assert.IsEmpty (peersInvolved);
        }

        [Test]
        public void DupeRequests_FinalBlock_ValidateDupeThenPrimary ()
        {
            var seeder1 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var seeder2 = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = peer.BitField.SetAll (false).Set (3, true);

            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder1, seeder1.BitField, 0, bitfield.Length, out _));

            PieceSegment? req;
            var requests = new List<PieceSegment> ();
            while ((req = picker.PickPiece (seeder1, singlePiece)) != null)
                requests.Add (req.Value);
            for (int i = 0; i < requests.Count; i++)
                if (i != 2)
                    Assert.IsTrue (picker.ValidatePiece (seeder1, requests[i], out _, new HashSet<IRequester> ()));

            // This should be the final unrequested block
            Assert.IsTrue (picker.ContinueAnyExistingRequest (seeder2, seeder2.BitField, 0, bitfield.Length, 2, out PieceSegment request));
            Assert.AreEqual (requests[2], request);

            // Validate the dupe request first
            var peersInvolved = new HashSet<IRequester> ();
            Assert.IsTrue (picker.ValidatePiece (seeder2, request, out bool complete, peersInvolved));
            Assert.IsTrue (complete);
            CollectionAssert.AreEqual (new[] { seeder1, seeder2 }, peersInvolved);
            Assert.AreEqual (0, seeder1.AmRequestingPiecesCount);
            Assert.AreEqual (0, seeder2.AmRequestingPiecesCount);

            // Now the primary will be discarded as we've already received the primary request.
            peersInvolved.Clear ();
            Assert.IsFalse (picker.ValidatePiece (seeder1, request, out complete, peersInvolved));
            Assert.IsFalse (complete);
            Assert.IsEmpty (peersInvolved);
        }

        [Test]
        public void DupeRequests_PeerCannotDuplicateOwnRequest ()
        {
            var seeder = PeerId.CreateNull (bitfield.Length, true, false, true);
            var singlePiece = new BitField (seeder.BitField).SetAll (false).Set (3, true);

            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder, seeder.BitField, 0, bitfield.Length - 1, out _));

            PieceSegment? req;
            var requests = new List<PieceSegment> ();
            while ((req = picker.PickPiece (seeder, singlePiece)) != null)
                requests.Add (req.Value);

            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder, seeder.BitField, 0, bitfield.Length - 1, out _));
            Assert.IsFalse (picker.ContinueAnyExistingRequest (seeder, seeder.BitField, 0, bitfield.Length - 1, 2, out _));
        }

        [Test]
        public void DupeRequests_CanRequestInTriplicate ()
        {
            var seeders = new PeerId[] {
                PeerId.CreateNull (bitfield.Length, true, false, true),
                PeerId.CreateNull (bitfield.Length, true, false, true),
                PeerId.CreateNull (bitfield.Length, true, false, true),
            };

            var queue = new Queue<PeerId> (seeders);
            var requests = seeders.ToDictionary (t => t, t => new List<PieceSegment> ());
            var singlePiece = new BitField (seeders[0].BitField).SetAll (false).Set (3, true);

            // Request an entire piece using 1 peer first to ensure we have collisions when
            // issuing duplicates. In the end all peers should have the same set though.
            while (true) {
                var req = picker.PickPiece (seeders[0], singlePiece);
                if (!req.HasValue && picker.ContinueAnyExistingRequest (seeders[0], seeders[0].BitField, 0, bitfield.Length - 1, 3, out PieceSegment other))
                    req = other;

                if (req.HasValue)
                    requests[seeders[0]].Add (req.Value);
                else
                    break;
            }
            Assert.AreEqual (torrentData.TorrentInfo.BlocksPerPiece (0), requests[seeders[0]].Count);

            while (queue.Count > 0) {
                var seeder = queue.Dequeue ();
                PieceSegment? req = picker.PickPiece (seeder, singlePiece);
                if (req == null)
                    if (picker.ContinueAnyExistingRequest (seeder, seeder.BitField, 0, bitfield.Length - 1, 3, out PieceSegment other))
                        req = other;

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
