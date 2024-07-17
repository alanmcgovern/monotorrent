//
// MerkleLayersTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2024 Alan McGovern
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
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class MerkleLayersTests
    {
        [Test]
        public void InvalidLargestLayer ()
        {
            // 3 million 16kB pieces... because why not?
            var layers = new MerkleTree (MerkleRoot.FromMemory (new byte[32]), 16_384, 10_000_000);
            Assert.IsFalse (layers.TryVerify (out _));

            Span<byte> proofs = new byte[(BitOps.CeilLog2 (10_000_000) - 10) * 32];
            Assert.IsFalse (layers.TryAppend (layers.PieceLayerIndex, 0, 1, proofs));
        }

        [Test]
        public void IncompleteProofLayers ()
        {
            // 3 million 16kB pieces... because why not?
            var layers = new MerkleTree (MerkleRoot.FromMemory (new byte[32]), 16_384, 1 << 11);

            // 1 byte short of 2 proofs
            Assert.IsFalse (layers.TryAppend (layers.PieceLayerIndex, 0, 1, new byte[63]));
            // 1 byte short of 100 proofs
            Assert.IsFalse (layers.TryAppend (layers.PieceLayerIndex, 0, 1, new byte[(100 * 32) - 1]));
        }

        [Test]
        public void NotPieceLayer ()
        {
            var layers = new MerkleTree (MerkleRoot.FromMemory (new byte[32]), 16_384, 100);
            Assert.Throws<NotSupportedException> (() => layers.TryAppend (layers.PieceLayerIndex - 1, 0, 1, default));
            Assert.Throws<NotSupportedException> (() => layers.TryAppend (layers.PieceLayerIndex + 1, 0, 1, default));
        }

        [Test]
        public void ValidLargestLayer ()
        {
            var paddingHash = MerkleTreeHasher.PaddingHashesByLayer[16];

            // 3 million 16kB pieces... because why not?
            var layers = new MerkleTree (MerkleRoot.FromMemory (paddingHash), 16_384, 1 << 16);
            Assert.IsTrue (layers.TryVerify (out _));

        }

    }
}
