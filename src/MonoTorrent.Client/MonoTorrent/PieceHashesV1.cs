//
// PieceHashesV1.cs
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
using System.Diagnostics.CodeAnalysis;

namespace MonoTorrent
{
    class PieceHashesV1 : IPieceHashes
    {
        readonly int HashCodeLength;
        readonly ReadOnlyMemory<byte> HashData;

        public bool HasV1Hashes => true;

        public bool HasV2Hashes => false;

        /// <summary>
        /// Number of Hashes (equivalent to number of Pieces)
        /// </summary>
        public int Count => HashData.Length / HashCodeLength;

        internal PieceHashesV1 (ReadOnlyMemory<byte> hashData, int hashCodeLength)
            => (HashData, HashCodeLength) = (hashData, hashCodeLength);

        /// <summary>
        /// Returns the hash for a specific piece
        /// </summary>
        /// <param name="hashIndex">Piece/hash index to return</param>
        /// <returns>byte[] (length HashCodeLength) containing hashdata</returns>
        public ReadOnlyPieceHash GetHash (int hashIndex)
            => new ReadOnlyPieceHash (HashData.Slice (hashIndex * HashCodeLength, HashCodeLength), default);

        public bool IsValid (ReadOnlyPieceHash hashes, int hashIndex)
            => GetHash (hashIndex).V1Hash.Span.SequenceEqual (hashes.V1Hash.Span);

        public bool TryGetV2Hashes (MerkleRoot piecesRoot, [NotNullWhen (true)] out ReadOnlyMerkleTree? layers)
        {
            layers = null;
            return false;
        }
        public bool TryGetV2Hashes (MerkleRoot piecesRoot, int baseLayer, int index, int count, int proofCount, Span<byte> hasheAndProofsBuffer, out int bytesWritten)
        {
            bytesWritten = 0;
            return false;
        }
    }
}
