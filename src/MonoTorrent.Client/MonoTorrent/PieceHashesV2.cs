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
using System.Linq;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    class PieceHashesV2 : IPieceHashes
    {
        readonly int HashCodeLength;
        readonly IList<ITorrentFile> Files;
        readonly BEncodedDictionary Layers;

        /// <summary>
        /// Number of Hashes (equivalent to number of Pieces)
        /// </summary>
        public int Count { get; }

        internal PieceHashesV2 (IList<ITorrentFile> files, BEncodedDictionary layers)
            => (Files, Layers, HashCodeLength, Count) = (files, layers, 32, files.Last ().EndPieceIndex + 1);

        /// <summary>
        /// Returns the hash for a specific piece
        /// </summary>
        /// <param name="hashIndex">Piece/hash index to return</param>
        /// <returns>byte[] (length HashCodeLength) containing hashdata</returns>
        public ReadOnlyMemory<byte> GetHash (int hashIndex)
        {
            if (hashIndex < 0 || hashIndex > Count)
                throw new ArgumentOutOfRangeException (nameof (hashIndex), $"Value must be grater than or equal to '0' and less than '{Count}'");

            for (int i = 0; i < Files.Count; i++) {
                if (hashIndex < Files[i].StartPieceIndex || hashIndex > Files[i].EndPieceIndex)
                    continue;
                var layer = (BEncodedString) Layers[BEncodedString.FromMemory (Files[i].PiecesRoot)];
                return layer.AsMemory ().Slice ((hashIndex - Files[i].StartPieceIndex) * HashCodeLength, HashCodeLength);
            }
            throw new InvalidOperationException ("Requested a piece which does not exist");
        }

        public bool IsValid (ReadOnlySpan<byte> hash, int hashIndex)
        {
            if (hash.Length != HashCodeLength)
                throw new ArgumentException ($"Hash must be {HashCodeLength} bytes in length", nameof (hash));

            return GetHash (hashIndex).Span.SequenceEqual (hash);
        }
    }
}
