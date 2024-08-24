//
// TorrentTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Messages.Peer;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentV2Test
    {
        string HybridTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (TorrentV2Test).Assembly.Location), "MonoTorrent", "bittorrent-v2-hybrid-test.torrent");
        string V2OnlyTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (TorrentV2Test).Assembly.Location), "MonoTorrent", "bittorrent-v2-test.torrent");

        Torrent HybridTorrent;
        Torrent V2OnlyTorrent;

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            HybridTorrent = Torrent.Load (HybridTorrentPath);
            V2OnlyTorrent = Torrent.Load (V2OnlyTorrentPath);
        }

        [Test]
        public void LoadSingleFile ()
        {
            var torrent = Torrent.Load (Encoding.UTF8.GetBytes ("d4:infod9:file treed4:dir1d4:dir2d9:fileA.txtd0:d6:lengthi1024e11:pieces root32:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaeeeee12:piece lengthi32768e12:meta versioni2eeeee"));
            var file = torrent.Files.Single ();
            Assert.AreEqual (Path.Combine ("dir1", "dir2", "fileA.txt"), file.Path);
            Assert.AreEqual (0, file.StartPieceIndex);
            Assert.AreEqual (0, file.EndPieceIndex);
            Assert.AreEqual (0, file.OffsetInTorrent);

            var hash = Enumerable.Repeat ((byte) 'a', 32).ToArray ().AsMemory ();
            Assert.IsTrue (hash.Span.SequenceEqual (file.PiecesRoot.Span));
        }

        [Test]
        public void FileLengthExactlyPieceLength ()
        {
            var torrent = Torrent.Load (Encoding.UTF8.GetBytes ("d4:infod9:file treed4:dir1d4:dir2d9:fileA.txtd0:d6:lengthi32768e11:pieces root32:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaeeeee12:piece lengthi32768e12:meta versioni2eee"));
            var file = torrent.Files.Single ();
            Assert.AreEqual (Path.Combine ("dir1", "dir2", "fileA.txt"), file.Path);
            Assert.AreEqual (0, file.StartPieceIndex);
            Assert.AreEqual (0, file.EndPieceIndex);
            Assert.AreEqual (0, file.OffsetInTorrent);

            var hash = Enumerable.Repeat ((byte) 'a', 32).ToArray ().AsMemory ();
            Assert.IsTrue (hash.Span.SequenceEqual (file.PiecesRoot.Span));
        }

        [Test]
        public void LoadV2OnlyTorrent ()
        {
            // A v2 only torrent does not have a regular infohash
            Assert.IsNull (V2OnlyTorrent.InfoHashes.V1);
            Assert.IsNotNull (V2OnlyTorrent.InfoHashes.V2);

            Assert.IsTrue (V2OnlyTorrent.CreatePieceHashes ().GetHash (V2OnlyTorrent.PieceCount - 1).V1Hash.IsEmpty);
            Assert.IsFalse (V2OnlyTorrent.CreatePieceHashes ().GetHash (V2OnlyTorrent.PieceCount - 1).V2Hash.IsEmpty);
            Assert.IsFalse (V2OnlyTorrent.CreatePieceHashes ().GetHash (0).V2Hash.IsEmpty);

            Assert.AreEqual (InfoHash.FromHex ("caf1e1c30e81cb361b9ee167c4aa64228a7fa4fa9f6105232b28ad099f3a302e"), V2OnlyTorrent.InfoHashes.V2);
        }

        [Test]
        public void LoadHybridTorrent ()
        {
            Assert.IsNotNull (HybridTorrent.InfoHashes.V1);
            Assert.IsNotNull (HybridTorrent.InfoHashes.V2);
            Assert.IsFalse (HybridTorrent.CreatePieceHashes ().GetHash (0).V1Hash.IsEmpty);
            Assert.IsFalse (HybridTorrent.CreatePieceHashes ().GetHash (0).V2Hash.IsEmpty);
            Assert.AreEqual (HybridTorrent.Size, HybridTorrent.Files.Select (t => t.Length + t.Padding).Sum ());
        }

        [Test]
        public void BlocksPerPiece ()
        {
            foreach (var file in V2OnlyTorrent.Files) {
                var actualBlocks = Enumerable.Range (file.StartPieceIndex, file.EndPieceIndex - file.StartPieceIndex + 1)
                    .Select (V2OnlyTorrent.BlocksPerPiece)
                    .Sum ();
                var expectedBlocks = (file.Length + Constants.BlockSize - 1) / Constants.BlockSize;
                Assert.AreEqual (expectedBlocks, actualBlocks);
            }
        }

        [Test]
        public void ByteOffsetToPieceIndex ()
        {
            long runningTotal = 0;
            foreach (var file in V2OnlyTorrent.Files) {
                Assert.AreEqual (file.StartPieceIndex, V2OnlyTorrent.ByteOffsetToPieceIndex (runningTotal));
                Assert.AreEqual (file.EndPieceIndex, V2OnlyTorrent.ByteOffsetToPieceIndex (runningTotal + file.Length - 1));
                runningTotal += file.Length;
            }
        }

        [Test]
        public void BytesPerPiece ()
        {
            foreach (var file in V2OnlyTorrent.Files) {
                Assert.AreEqual (file.Length % V2OnlyTorrent.PieceLength, V2OnlyTorrent.BytesPerPiece (file.EndPieceIndex));
            }
        }

        [Test]
        public void PieceCount ()
        {
            Assert.AreEqual (V2OnlyTorrent.PieceCount, V2OnlyTorrent.Files.Last ().EndPieceIndex + 1);
            Assert.AreEqual (V2OnlyTorrent.PieceCount, ((ITorrentInfo) V2OnlyTorrent).PieceCount ());
        }

        [Test]
        public void PieceIndexToByteOffset ()
        {
            long runningTotal = 0;
            foreach (var file in V2OnlyTorrent.Files) {
                Assert.AreEqual (runningTotal, V2OnlyTorrent.PieceIndexToByteOffset (file.StartPieceIndex));
                Assert.AreEqual (runningTotal + file.Length - (file.Length % V2OnlyTorrent.PieceLength), V2OnlyTorrent.PieceIndexToByteOffset (file.EndPieceIndex));
                runningTotal += file.Length;
            }
        }

        [Test]
        public void PiecesRootNotNull ()
        {
            TorrentFileInfo wrapper = new TorrentFileInfo (V2OnlyTorrent.Files[0], @"c:\test.a");
            Assert.AreEqual (V2OnlyTorrent.Files[0].PiecesRoot, wrapper.PiecesRoot);
        }
    }
}
