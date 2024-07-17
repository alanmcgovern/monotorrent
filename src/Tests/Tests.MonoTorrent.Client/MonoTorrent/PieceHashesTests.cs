//
// PieceHashesTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2022 Alan McGovern
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
using System.Numerics;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent
{
    [TestFixture]
    public class PieceHashesTests
    {
        [Test]
        public void V2Only_NoHashes_IsValid ()
        {
            var hashes = new PieceHashes (null, null);
            Assert.IsFalse (hashes.IsValid (new ReadOnlyPieceHash (new byte[20], new byte[32]), 1));
        }

        [Test]
        public void V2Only_NoHashes_GetHash ()
        {
            var hashes = new PieceHashes (null, null).GetHash (5);
            Assert.IsTrue (hashes.V1Hash.IsEmpty);
            Assert.IsTrue (hashes.V2Hash.IsEmpty);
        }

        [Test]
        public void V2Only_NoHashes_Count ()
        {
            Assert.AreEqual (0, new PieceHashes (null, null).Count);
        }

        [Test]
        public void V2Only_WithEmptyFiles ()
        {
            var files = new[] {
                new TorrentFile("a", length: 0, startIndex: 0, endIndex: 0, 0, TorrentFileAttributes.None, 0),
                new TorrentFile("b", length: 1, startIndex: 0, endIndex: 0, 0, new MerkleRoot(Enumerable.Repeat<byte>(1, 32).ToArray ()), TorrentFileAttributes.None, 0),
                new TorrentFile("c", length: 2, startIndex: 1, endIndex: 1, 1, new MerkleRoot(Enumerable.Repeat<byte>(2, 32).ToArray ()), TorrentFileAttributes.None, 0),
            };

            var pieceHashesV2 = new PieceHashesV2 (Constants.BlockSize * 4, files, new BEncodedDictionary ());
            var hash = pieceHashesV2.GetHash (0);
            Assert.IsTrue (files[1].PiecesRoot.AsMemory ().Span.SequenceEqual (hash.V2Hash.Span));
            Assert.IsTrue (hash.V1Hash.IsEmpty);
        }

        [Test]
        public void V2Only_WithHugeFiles ()
        {
            int pieceSize = 16_384;
            int pieceCount = 1 << 16;
            var paddingHash = MerkleTreeHasher.PaddingHashesByLayer[BitOps.CeilLog2 (pieceCount)];

            var layers = new MerkleTree (MerkleRoot.FromMemory (paddingHash), pieceSize, pieceCount);
            Assert.IsTrue (layers.TryVerify (out ReadOnlyMerkleTree verifiedLayers));

            var files = new[] {
                new TorrentFile ("large", (long) pieceSize * pieceCount - 1, startIndex: 0, endIndex: pieceCount - 1, 0, verifiedLayers.Root, TorrentFileAttributes.None, 0)
            };

            var pieceHashesV2 = new PieceHashesV2 (Constants.BlockSize, files, new Dictionary<MerkleRoot, ReadOnlyMerkleTree> { { verifiedLayers.Root, verifiedLayers } } );
            Assert.IsTrue (MerkleTreeHasher.PaddingHashesByLayer[0].Span.SequenceEqual(pieceHashesV2.GetHash (pieceCount - 1).V2Hash.Span));
        }
    }
}
