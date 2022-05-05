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
using System.Threading.Tasks;

using MonoTorrent.Client;

using NUnit.Framework;

namespace MonoTorrent.PiecePicking
{
    [TestFixture]
    public class PieceManagerTests
    {
        PeerId peer;
        List<PeerId> peers;
        PieceManager manager;
        TorrentManager torrentManager;

        [SetUp]
        public async Task Setup ()
        {
            int pieceCount = 40;
            int pieceLength = 256 * 1024;
            var torrentData = TestTorrentManagerInfo.Create (
                files: TorrentFileInfo.Create (pieceLength, ("File", pieceLength * pieceCount, "full/path/File")),
                pieceLength: pieceLength,
                size: pieceLength * pieceCount
            );
            peers = new List<PeerId> ();

            torrentManager = TestRig.CreateSingleFileManager (torrentData.TorrentInfo.Size, torrentData.TorrentInfo.PieceLength);
            await torrentManager.LoadFastResumeAsync (new FastResume (torrentManager.InfoHashes, new BitField (pieceCount).SetAll (true), new BitField (pieceCount).SetAll (false)));

            manager = new PieceManager (torrentManager);
            manager.Initialise ();

            peer = PeerId.CreateNull (pieceCount);
            for (int i = 0; i < 20; i++) {
                PeerId p = PeerId.CreateNull (pieceCount);
                p.SupportsFastPeer = true;
                peers.Add (p);
            }
        }


        [Test]
        public async Task RequestInEndgame_AllDoNotDownload ()
        {
            foreach (var file in torrentManager.Files)
                await torrentManager.SetFilePriorityAsync (file, Priority.DoNotDownload);

            torrentManager.MutableBitField.SetAll (true).Set (0, false);
            peers[0].MutableBitField.SetAll (true);
            peers[0].IsChoking = false;

            manager.AddPieceRequests (peers[0]);
            Assert.AreEqual (0, peers[0].AmRequestingPiecesCount, "#1");
            Assert.AreEqual (0, peers[0].MessageQueue.QueueLength, "#2");
        }

        [Test]
        public void RequestWhenSeeder ()
        {
            torrentManager.MutableBitField.SetAll (true);
            peers[0].MutableBitField.SetAll (true);
            peers[0].IsChoking = false;

            manager.AddPieceRequests (peers[0]);
            Assert.AreEqual (0, peers[0].AmRequestingPiecesCount, "#1");
            Assert.AreEqual (0, peers[0].MessageQueue.QueueLength, "#2");
        }
    }
}
