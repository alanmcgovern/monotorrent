//
// PieceHashes.cs
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
using System.Diagnostics.CodeAnalysis;

namespace MonoTorrent
{
    class PieceHashes : IPieceHashes
    {
        PieceHashesV1? V1 { get; }
        PieceHashesV2? V2 { get; }

        public int Count => V1?.Count ?? V2?.Count ?? 0;

        public bool HasV1Hashes => V1 != null;

        public bool HasV2Hashes => V2 != null;

        internal PieceHashes (PieceHashesV1? v1Hashes, PieceHashesV2? v2Hashes)
            => (V1, V2) = (v1Hashes, v2Hashes);

        public ReadOnlyPieceHash GetHash (int hashIndex)
        {
            var v1 = V1 is null ? default : V1.GetHash (hashIndex);
            var v2 = V2 is null ? default : V2.GetHash (hashIndex);
            return new ReadOnlyPieceHash (v1.V1Hash, v2.V2Hash);
        }

        public bool IsValid (ReadOnlyPieceHash hashes, int hashIndex)
        {
            return (V1 is null || V1.IsValid (hashes, hashIndex))
                && (V2 is null || V2.IsValid (hashes, hashIndex))
                && !(V1 is null && V2 is null);
        }
        public bool TryGetV2Hashes (MerkleRoot piecesRoot, [NotNullWhen (true)] out ReadOnlyMerkleTree? layers)
        {
            layers = null;
            return V2 == null ? false : V2.TryGetV2Hashes (piecesRoot, out layers);
        }
        public bool TryGetV2Hashes (MerkleRoot piecesRoot, int baseLayer, int index, int count, int proofCount, Span<byte> hashesAndProofsBuffer, out int bytesWritten)
        {
            bytesWritten = 0;
            return V2 == null ? false : V2.TryGetV2Hashes (piecesRoot, baseLayer, index, count, proofCount, hashesAndProofsBuffer, out bytesWritten);
        }
    }
}
