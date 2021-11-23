//
// StreamingPieceRequesterTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
    public class StreamingPieceRequesterTests
    {
        class TorrentData : ITorrentData
        {
            public IList<ITorrentFileInfo> Files { get; } = TorrentFileInfo.Create (Constants.BlockSize * 8, 1024 * 1024 * 8);
            public InfoHash InfoHash => new InfoHash (new byte[20]);
            public string Name => "Test Torrent";
            public int PieceLength => Constants.BlockSize * 8;
            public long Size => Files[0].Length;
        }

        [Test]
        public void PickFromBeforeHighPrioritySet ()
        {
            var data = new TorrentData ();
            var ignoringBitfield = new MutableBitField (data.PieceCount ())
                .SetAll (true)
                .Set (0, false);

            var requester = new StreamingPieceRequester ();
            requester.Initialise (data, new[] { ignoringBitfield });
            requester.SeekToPosition (data.Files[0], data.PieceLength * 3);

            var peer = PeerId.CreateNull (ignoringBitfield.Length, true, false, true);
            requester.AddRequests (peer, Array.Empty<IPeerWithMessaging> ());
            Assert.AreEqual (2, peer.AmRequestingPiecesCount);

            var requests = peer.Requests;
            Assert.AreEqual (2, requests.Count);
            Assert.IsTrue (requests.All (r => r.PieceIndex == 0));
        }

        [Test]
        public void PickHighestPriority ()
        {
            var data = new TorrentData ();
            var ignoringBitfield = new MutableBitField (data.PieceCount ())
                .SetAll (false);

            var requester = new StreamingPieceRequester ();
            requester.Initialise (data, new[] { ignoringBitfield });
            requester.SeekToPosition (data.Files[0], data.PieceLength * 3);

            var peer = PeerId.CreateNull (ignoringBitfield.Length, true, false, true);
            requester.AddRequests (peer, Array.Empty<IPeerWithMessaging> ());
            Assert.AreEqual (4, peer.AmRequestingPiecesCount);

            var requests = peer.Requests;
            Assert.AreEqual (4, requests.Count);
            Assert.IsTrue (requests.All (r => r.PieceIndex == 3));
        }
    }
}
