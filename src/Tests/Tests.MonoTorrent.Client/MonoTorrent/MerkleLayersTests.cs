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
            var layers = new MerkleLayers (MerkleRoot.FromMemory (new byte[32]), 16_384, 10_000_000);
            Assert.IsFalse (layers.TryVerify (out _));
            Assert.IsFalse (layers.TryAppend (layers.PieceLayerIndex, 0, 1, new byte[16784], ReadOnlySpan<byte>.Empty));
            Assert.IsFalse (layers.TryAppend (20, 0, 1, new byte[16784], ReadOnlySpan<byte>.Empty));
        }

        [Test]
        public void ValidLargestLayer ()
        {
            var paddingHash = MerkleHash.PaddingHashesByLayer[16];

            // 3 million 16kB pieces... because why not?
            var layers = new MerkleLayers (MerkleRoot.FromMemory (paddingHash), 16_384, 1 << 16);
            Assert.IsTrue (layers.TryVerify (out _));

        }

    }
}
