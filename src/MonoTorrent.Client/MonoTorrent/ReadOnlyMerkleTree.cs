//
// ReadOnlyMerkleTree.cs
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
using System.Numerics;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public class ReadOnlyMerkleTree
    {
        public static ReadOnlyMerkleTree FromLayer (int pieceLength, ReadOnlySpan<byte> layer)
        {
            if (layer.Length % 32 != 0)
                throw new ArgumentException ("The layer span should be a multiple of 32 bytes as it consists of SHA256 hashes", nameof (layer));
            if (pieceLength < 0 || BitOps.PopCount ((uint) pieceLength) != 1)
                throw new ArgumentException ("Piece length must be positive and a power of 2", nameof (pieceLength));

            MerkleTree layers = new MerkleTree (MerkleRoot.Empty, pieceLength, layer.Length / 32);
            if (!layers.TryAppend (layers.PieceLayerIndex, 0, layer.Length / 32, layer))
                throw new InvalidOperationException ("Failed to append merkle layers to the three");
            if (!layers.TryVerify (out var verifiedHashes))
                throw new InvalidOperationException ("Failed to verify a layer when no expected hash was provided");
            return verifiedHashes;
        }

        public static ReadOnlyMerkleTree FromLayer (int pieceLength, ReadOnlySpan<byte> layer, MerkleRoot expectedRoot)
        {
            if (expectedRoot.IsEmpty)
                throw new ArgumentException ("The expected MerkleRoot cannot be empty", nameof (expectedRoot));

            var merkleTree = FromLayer (pieceLength, layer);
            if (merkleTree.Root != expectedRoot)
                throw new ArgumentException (nameof (expectedRoot), "The root of the generated merkle tree did not match the expected root.");

            return merkleTree;
        }

        IReadOnlyList<ReadOnlyMemory<byte>> Layers { get; }

        public int LayerCount => Layers.Count;

        public int PieceLayerIndex { get; }

        /// <summary>
        /// This is the number of hashes in the MerkleLayer, which is always a power of two. In the event the
        /// piece layer for a file has 5 hashes, the associated piece layer hash count would be 8.
        /// </summary>
        public int PieceLayerHashCount => (int) BitOps.RoundUpToPowerOf2 (Layers[PieceLayerIndex].Length / 32);

        public MerkleRoot Root => MerkleRoot.FromMemory (Layers[Layers.Count - 1]);

        internal ReadOnlyMerkleTree (IReadOnlyList<ReadOnlyMemory<byte>> layers, int pieceLayer)
        {
            Layers = layers;
            PieceLayerIndex = pieceLayer;
        }

        public ReadOnlyMemory<byte> GetHash (int layer, int index)
        {
            var hashes = Layers[layer];
            if ((index * 32) >= hashes.Length)
                return MerkleTreeHasher.PaddingHashesByLayer[layer];
            return hashes.Slice (index * 32, 32);
        }

        internal void CopyHashes (int layer, int index, Span<byte> dest)
        {
            var layerBytes = Layers[layer];
            var layerStartIndex = index * 32;
            var layerEndIndex = layerStartIndex + dest.Length;

            // Copy as much as we can from the actual layer.
            var count = Math.Min (layerEndIndex - layerStartIndex, layerBytes.Length - layerStartIndex);
            if (count > 0) {
                layerBytes.Slice (layerStartIndex, count).Span.CopyTo (dest);
                dest = dest.Slice (count);
            }

            // Now, copy in the padding hash until the buffer is full.
            while (dest.Length > 0) {
                MerkleTreeHasher.PaddingHashesByLayer[layer].Span.CopyTo (dest);
                dest = dest.Slice (32);
            }
        }

        internal ReadOnlyMemory<byte> GetHashes (int layer)
            => Layers[layer];
    }
}
