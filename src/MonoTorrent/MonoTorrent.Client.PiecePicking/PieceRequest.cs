//
// PieceRequest.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2019 Alan McGovern
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

namespace MonoTorrent.Client.PiecePicking
{
    public sealed class PieceRequest : IEquatable<PieceRequest>
    {
        public int PieceIndex { get; }
        public int StartOffset { get; }
        public int RequestLength { get; }
        public IPieceRequester RequestedOff { get; }

        public PieceRequest (int pieceIndex, int startOffset, int requestLength, IPieceRequester requestedOff)
            => (PieceIndex, StartOffset, RequestLength, RequestedOff) = (pieceIndex, startOffset, requestLength, requestedOff);

        public override bool Equals (object obj)
            => Equals (obj as PieceRequest);

        public bool Equals (PieceRequest other)
        {
            return other != null
                && other.RequestedOff == RequestedOff
                && other.PieceIndex == PieceIndex
                && other.StartOffset == StartOffset
                && other.RequestLength == RequestLength;
        }

        public override int GetHashCode ()
            => PieceIndex;

        public static bool operator == (PieceRequest left, PieceRequest right)
            => left is null ? right is null : left.Equals (right);

        public static bool operator != (PieceRequest left, PieceRequest right)
            => !(left == right);
    }
}
