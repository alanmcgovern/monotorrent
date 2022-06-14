//
// StandardPieceRequester.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
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
    public static class PieceRequesterExtensions
    {
        public static PieceSegment ToPieceSegment (this BlockInfo blockInfo)
            => new PieceSegment (blockInfo.PieceIndex, blockInfo.StartOffset / Constants.BlockSize);

        public static BlockInfo ToBlockInfo (this PieceSegment pieceSegment, IPieceRequesterData info)
        {
            var totalBlocks = info.SegmentsPerPiece (pieceSegment.PieceIndex);
            var size = pieceSegment.BlockIndex == totalBlocks - 1 ? info.BytesPerPiece (pieceSegment.PieceIndex) - (pieceSegment.BlockIndex) * Constants.BlockSize : Constants.BlockSize;
            return new BlockInfo (pieceSegment.PieceIndex, pieceSegment.BlockIndex * Constants.BlockSize, size);
        }

        public static Span<BlockInfo> ToBlockInfo (this Span<PieceSegment> segments, Span<BlockInfo> blocks, IPieceRequesterData info)
        {
            for (int i = 0; i < segments.Length; i++)
                blocks[i] = segments[i].ToBlockInfo (info);
            return blocks.Slice (0, segments.Length);
        }

        public static Span<PieceSegment> ToPieceSegment (this Span<BlockInfo> blocks, Span<PieceSegment> segments)
        {
            for (int i = 0; i < segments.Length; i++)
                segments[i] = blocks[i].ToPieceSegment ();
            return segments.Slice (0, segments.Length);
        }
    }
}
