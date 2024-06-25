//
// MerkleLayers.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace MonoTorrent
{
    public class MerkleLayers
    {
        const int HashCodeLength = 32;

        List<Memory<byte>> Layers { get; }

        MerkleRoot ExpectedRoot { get; set; }

        public int PieceLayerIndex { get; }

        public MerkleLayers (MerkleRoot expectedRoot, int pieceLength, int pieceLayerHashCount)
        {
            if (pieceLayerHashCount == 1)
                throw new ArgumentException ("A merkletree must have 2 or more hashes");

            PieceLayerIndex = BitOps.CeilLog2 ((uint) pieceLength / 16384);
            Layers = new List<Memory<byte>> ();

            var finalLayer = BitOps.CeilLog2 ((uint) pieceLayerHashCount);
            for (int i = 0; i < PieceLayerIndex; i++)
                Layers.Add (Memory<byte>.Empty);
            for (int layer = PieceLayerIndex; layer <= PieceLayerIndex + finalLayer; layer++) {
                Layers.Add (new Memory<byte> (new byte[pieceLayerHashCount * 32]));
                pieceLayerHashCount = (pieceLayerHashCount + 1) / 2;
            }
            if (Layers[Layers.Count - 1].Length != 32)
                throw new InvalidOperationException ("Did not construct the merkle tree correctly");
            ExpectedRoot = expectedRoot;
        }

        public bool TryAppend (int baseLayer, int index, int length, ReadOnlySpan<byte> hashes, ReadOnlySpan<byte> proofs)
        {
            var fileHashes = Layers[baseLayer];
            var hashOffset = index * HashCodeLength;
            var hashLength = length * HashCodeLength;
            if (hashOffset > fileHashes.Length)
                return false;

            var actualLength = Math.Min ((fileHashes.Length / 32) - index, length);

            Span<byte> computedHash = stackalloc byte[32];
            using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);
            if (!MerkleHash.TryHash (hasher, hashes, baseLayer, proofs, index, length, computedHash, out int written))
                return false;
            if (!ExpectedRoot.IsEmpty && !computedHash.SequenceEqual (ExpectedRoot.Span))
                return false;
            if (ExpectedRoot.IsEmpty)
                ExpectedRoot = new MerkleRoot (computedHash);
            
            hashes.Slice (0, hashLength).CopyTo (fileHashes.Span.Slice (hashOffset, hashLength));
            return true;
        }

        public bool TryVerify ([NotNullWhen (true)] out ReadOnlyMerkleLayers? verifiedMerkleTree)
        {
            verifiedMerkleTree = null;
            int written = 0;
            using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);

            for (int layer = PieceLayerIndex; layer < Layers.Count - 1; layer++) {
                var src = Layers[layer];
                var dest = Layers[layer + 1];
                for (int i = 0; i < Layers[layer].Length / 64; i++) {
                    hasher.AppendData (src.Span.Slice (i * 64, 64));
                    if (!hasher.TryGetHashAndReset (dest.Slice (i * 32, 32).Span, out written) || written != 32)
                        return false;
                }
                if (src.Length % 64 == 32) {
                    hasher.AppendData (src.Span.Slice (src.Length - 32, 32));
                    // Math.Pow(2, layer) == 1 << layer
                    hasher.AppendData (MerkleHash.PaddingHashesByLayer[layer].Span);
                    if (!hasher.TryGetHashAndReset (dest.Slice (dest.Length - 32, 32).Span, out written) || written != 32)
                        return false;
                }
            }

            if (Layers[Layers.Count - 1].Span.SequenceEqual (ExpectedRoot.Span))
                verifiedMerkleTree = new ReadOnlyMerkleLayers (Layers.Select (t => (ReadOnlyMemory<byte>) t).ToList ().AsReadOnly (), PieceLayerIndex);

            return verifiedMerkleTree != null;
        }
    }
}
