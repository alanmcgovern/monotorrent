//
// PieceManagerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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

using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client.PiecePicking
{
    [TestFixture]
    public class PieceManagerTests
    {
        class TestTorrentData : ITorrentData
        {
            public TorrentFile [] Files { get; set; }
            public int PieceLength { get; set; }
            public long Size { get; set; }

            public int BlocksPerPiece => PieceLength / Piece.BlockSize;
            public int PieceCount =>  (int) Math.Ceiling ((double)Size / PieceLength);
            public int TotalBlocks => (int) Math.Ceiling ((double)Size / Piece.BlockSize);
        }

        BitField bitfield;
        PeerId peer;
        List<PeerId> peers;
        MagnetLink magnetLink;
        PieceManager manager;
        TestTorrentData torrentData;
        TorrentManager torrentManager;

        [SetUp]
        public void Setup()
        {
            int pieceCount = 40;
            int pieceLength = 256 * 1024;
            bitfield = new BitField (pieceCount);
            torrentData = new TestTorrentData {
                Files = new [] { new TorrentFile ("File", pieceLength * pieceCount) },
                PieceLength = pieceLength,
                Size = pieceLength * pieceCount
            };
            peers = new List<PeerId>();

            magnetLink = new MagnetLink (new InfoHash (new byte[20]));
            torrentManager = new TorrentManager (magnetLink, "", new TorrentSettings (), "");
            manager = new PieceManager (torrentManager);
            manager.ChangePicker (new StandardPicker (), bitfield, torrentData);

            peer = PeerId.CreateNull (pieceCount);
            for (int i = 0; i < 20; i++) {
                PeerId p = PeerId.CreateNull (pieceCount);
                p.SupportsFastPeer = true;
                peers.Add(p);
            }
        }

        [Test]
        public void ReceiveAllPieces_PieceUnhashed()
        {
            peers[0].BitField.SetAll(true);
            peers[0].IsChoking = false;
            bitfield.SetAll (true).SetFalse (1);

            PieceRequest p;
            var requests = new List<PieceRequest> ();
            Piece piece = null;
            while ((p = manager.Picker.PickPiece(peers[0], peers[0].BitField, peers)) != null) {
                piece = manager.PieceDataReceived(peers[0], new PieceMessage (p.PieceIndex, p.StartOffset, p.RequestLength));
                if (requests.Any (t => t.PieceIndex == p.PieceIndex && t.RequestLength == p.RequestLength && t.StartOffset == p.StartOffset))
                    Assert.Fail ("We should not pick the same piece twice");
                requests.Add (p);
            }
            Assert.IsNull (manager.Picker.PickPiece(peers[0], peers[0].BitField, peers), "#1");
            Assert.IsTrue (piece.AllBlocksReceived, "#2");
        }

        [Test]
        public void RequestFastHaveEverything()
        {
            peer.SupportsFastPeer = true;
            peer.IsAllowedFastPieces.AddRange(new int[] { 1, 2, 3, 5, 8, 13, 21 });

            peer.BitField.SetAll(true);
            bitfield.SetAll(true);

            Assert.IsNull(manager.Picker.PickPiece(peer, peer.BitField, peers), "#1");
            manager.AddPieceRequests (peer);
            Assert.AreEqual (0, peer.AmRequestingPiecesCount, "#2");
            Assert.AreEqual (0, peer.QueueLength, "#3");
        }

        [Test]
        public void RequestInEndgame_AllDoNotDownload ()
        {
            manager.ChangePicker (torrentManager.CreateStandardPicker (), bitfield, torrentData);
            foreach (var file in torrentData.Files)
                file.Priority = Priority.DoNotDownload;

            bitfield.SetAll(true).Set (0, false);
            peers[0].BitField.SetAll(true);
            peers[0].IsChoking = false;

            manager.AddPieceRequests(peers[0]);
            Assert.AreEqual (0, peers[0].AmRequestingPiecesCount, "#1");
            Assert.AreEqual (0, peers[0].QueueLength, "#2");
        }

        [Test]
        public void RequestWhenSeeder()
        {
            bitfield.SetAll(true);
            peers[0].BitField.SetAll(true);
            peers[0].IsChoking = false;

            manager.AddPieceRequests(peers[0]);
            Assert.AreEqual (0, peers[0].AmRequestingPiecesCount, "#1");
            Assert.AreEqual (0, peers[0].QueueLength, "#2");
        }
    }
}
