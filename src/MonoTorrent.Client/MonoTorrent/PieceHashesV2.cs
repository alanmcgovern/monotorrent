//
// PieceHashesV2.cs
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
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    class PieceHashesV2 : IPieceHashes
    {
        readonly int HashCodeLength;
        readonly IList<ITorrentFile> Files;
        readonly Dictionary<MerkleRoot, ReadOnlyMerkleTree> Layers;

        /// <summary>
        /// Number of Hashes (equivalent to number of Pieces)
        /// </summary>
        public int Count { get; }

        public bool HasV1Hashes => false;

        public bool HasV2Hashes => true;

        internal PieceHashesV2 (int pieceLength, IList<ITorrentFile> files, BEncodedDictionary layers)
            : this (pieceLength,
                  files,
                  layers.ToDictionary (kvp => MerkleRoot.FromMemory (kvp.Key.AsMemory ()), kvp => ReadOnlyMerkleTree.FromLayer (pieceLength, ((BEncodedString) kvp.Value).AsMemory ().Span, MerkleRoot.FromMemory (kvp.Key.AsMemory ()))))
        {
        }

        internal PieceHashesV2 (int pieceLength, IList<ITorrentFile> files, Dictionary<MerkleRoot, ReadOnlyMerkleTree> layers)
            => (Files, Layers, HashCodeLength, Count, PieceLayer) = (files, layers, 32, files.Last ().EndPieceIndex + 1, BitOps.CeilLog2 (pieceLength / 16384));

        int PieceLayer { get; }

        /// <summary>
        /// Returns the hash for a specific piece
        /// </summary>
        /// <param name="hashIndex">Piece/hash index to return</param>
        /// <returns>byte[] (length HashCodeLength) containing hashdata</returns>
        public ReadOnlyPieceHash GetHash (int hashIndex)
        {
            if (hashIndex < 0 || hashIndex > Count)
                throw new ArgumentOutOfRangeException (nameof (hashIndex), $"Value must be grater than or equal to '0' and less than '{Count}'");

            for (int i = 0; i < Files.Count; i++) {
                // Empty files have no root hash, as they have no data. Skip them.
                if (hashIndex < Files[i].StartPieceIndex || hashIndex > Files[i].EndPieceIndex || Files[i].Length == 0)
                    continue;

                // If the file has 2 or more pieces then we'll need to grab the appropriate sha from the layer
                if (Layers.TryGetValue (Files[i].PiecesRoot, out ReadOnlyMerkleTree? layers))
                    return new ReadOnlyPieceHash (ReadOnlyMemory<byte>.Empty, layers.GetHash (layers.PieceLayerIndex, hashIndex - Files[i].StartPieceIndex));

                // Otherwise, if the file is *exactly* one piece long 'PiecesRoot' is the hash!
                if (Files[i].EndPieceIndex != Files[i].StartPieceIndex)
                    throw new InvalidOperationException ("The file has two or more pieces but is missing the required piece layers");
                return new ReadOnlyPieceHash (ReadOnlyMemory<byte>.Empty, Files[i].PiecesRoot.AsMemory ());
            }
            throw new InvalidOperationException ("Requested a piece which does not exist");
        }

        public bool IsValid (ReadOnlyPieceHash hashes, int hashIndex)
        {
            return GetHash (hashIndex).V2Hash.Span.SequenceEqual (hashes.V2Hash.Span);
        }

        public bool TryGetV2Hashes (MerkleRoot piecesRoot, [NotNullWhen (true)] out ReadOnlyMerkleTree? layers)
        {
            return Layers.TryGetValue (piecesRoot, out layers);
        }

        public bool TryGetV2Hashes (MerkleRoot piecesRoot, int baseLayer, int index, int length, int proofCount, Span<byte> hashesAndProofsBuffer, out int bytesWritten)
        {
            bytesWritten = 0;

            // Basic sanity checks on the values passed in
            if (baseLayer != PieceLayer || index < 0 || length > 512 || length < 1)
                return false;

            // Check that the actual layer exists
            if (!Layers.TryGetValue (piecesRoot, out ReadOnlyMerkleTree? layers))
                return false;

            // Copy over the required hashes, starting at 'index' until the dest buffer is full.
            layers.CopyHashes (baseLayer, index, hashesAndProofsBuffer.Slice (0, length * 32));
            bytesWritten += length * 32;

            // The only time 'length' is equal to 1 is when the final request needed 1 piece. In this case we should
            // treat it as being a request of length '2' as we *should* have a padding hash to the right of the node we fetched.
            length = Math.Max (2, length);

            // The proofs buffer will be empty if the whole layer fits into the request
            var omittedHashes = BitOps.CeilLog2 (length) - 1;
            if (proofCount > 0)
                proofCount -= omittedHashes;

            // Reduce the size of the buffer by the number of omitted hashes.
            Span<byte> proofsBuffer = hashesAndProofsBuffer.Slice (bytesWritten, proofCount * 32);

            // The first proof layer is the one after the base layer, and we also skip ahead by the number of omitted hashes.
            var proofLayer = baseLayer + omittedHashes + 1;
            var proofLayerIndex = index / (1 << (proofLayer - baseLayer));
            while (proofsBuffer.Length > 0) {
                layers.GetHash (proofLayer, proofLayerIndex ^ 1).Span.CopyTo (proofsBuffer.Slice (0, HashCodeLength));
                proofLayerIndex /= 2;
                proofLayer++;
                proofsBuffer = proofsBuffer.Slice (HashCodeLength);
                bytesWritten += HashCodeLength;
            }

            return true;
        }
    }
}
