//
// ReadOnlyMerkleLayers.cs
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

namespace MonoTorrent
{
    public class ReadOnlyMerkleLayers
    {
        public static ReadOnlyMerkleLayers? FromLayer(int pieceLength, MerkleRoot expectedRoot, ReadOnlySpan<byte> layer)
        {
            MerkleLayers layers = new MerkleLayers (expectedRoot, pieceLength, layer.Length / 32);
            if (!layers.TryAppend (layers.PieceLayerIndex, 0, layer.Length / 32, layer, ReadOnlySpan<byte>.Empty))
                return null;
            if (!layers.TryVerify (out var verifiedHashes))
                return null;
            return verifiedHashes;

        }
        public ReadOnlyMemory<byte> Root => Layers[Layers.Count - 1];
        public IReadOnlyList<ReadOnlyMemory<byte>> Layers { get; }
        public ReadOnlyMemory<byte> PieceLayer => Layers[PieceLayerIndex];
        public int PieceLayerIndex { get; }

        internal ReadOnlyMerkleLayers (IReadOnlyList<ReadOnlyMemory<byte>> layers, int pieceLayer)
        {
            Layers = layers;
            PieceLayerIndex = pieceLayer;
        }

        public ReadOnlyMemory<byte> GetHash (int layerIndex, int offset)
        {
            var layer = Layers[layerIndex];
            if ((offset * 32) >= layer.Length)
                return MerkleHash.PaddingHashes[(int) Math.Pow (2, layerIndex) * 16384];
            return layer.Slice (offset * 32, 32);
        }
    }
}
