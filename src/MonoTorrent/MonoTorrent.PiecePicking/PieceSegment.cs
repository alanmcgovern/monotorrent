//
// BlockInfo.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2021 Alan McGovern
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

namespace MonoTorrent.PiecePicking
{
    public readonly struct PieceSegment : IEquatable<PieceSegment>
    {
        public static PieceSegment Invalid = new PieceSegment (invalid: -1);

        public int PieceIndex { get; }
        public int BlockIndex { get; }

        PieceSegment (int invalid)
        {
            PieceIndex = -1;
            BlockIndex = -1;
        }

        public PieceSegment (int pieceIndex, int blockIndex)
        {
            if (pieceIndex < 0)
                throw new ArgumentOutOfRangeException (nameof (pieceIndex));
            if (blockIndex < 0)
                throw new ArgumentOutOfRangeException (nameof (blockIndex));

            (PieceIndex, BlockIndex) = (pieceIndex, blockIndex);
        }

        public override bool Equals (object? obj)
            => obj is PieceSegment req && Equals (req);

        public bool Equals (PieceSegment other)
            => other.PieceIndex == PieceIndex
            && other.BlockIndex == BlockIndex;

        public override int GetHashCode ()
            => PieceIndex;

        public static bool operator == (PieceSegment left, PieceSegment right)
            => left.Equals (right);

        public static bool operator != (PieceSegment left, PieceSegment right)
            => !left.Equals (right);

        public override string ToString ()
            => $"Piece: {PieceIndex} - Offset {BlockIndex}";
    }
}
