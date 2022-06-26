//
// MerkleRoot.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
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
using System.Text;

namespace MonoTorrent
{
    public readonly struct MerkleRoot : IEquatable<MerkleRoot>
    {
        public static MerkleRoot Empty => new MerkleRoot ();

        public static MerkleRoot FromMemory (ReadOnlyMemory<byte> hash)
            => new MerkleRoot (hash);

        readonly ReadOnlyMemory<byte> Hash;

        public bool IsEmpty => Hash.IsEmpty;

        public ReadOnlySpan<byte> Span => Hash.Span;

        public MerkleRoot (Span<byte> hash)
            : this (new ReadOnlyMemory<byte> (hash.ToArray ()))
        {

        }

        private MerkleRoot (ReadOnlyMemory<byte> hash)
        {
            if (hash.Length != 32)
                throw new ArgumentException ("The hash must be exactly 32 bytes long");
            Hash = hash;
        }

        public ReadOnlyMemory<byte> AsMemory ()
            => Hash;

        public override bool Equals (object? obj)
            => obj is MerkleRoot other && Equals (other);

        public bool Equals (MerkleRoot other)
            => Hash.Span.SequenceEqual (other.Hash.Span);

        public override int GetHashCode ()
            => Hash.Length == 0 ? 0 : Hash.Span[0];

        public static bool operator == (MerkleRoot left, MerkleRoot right)
            => left.Hash.Span.SequenceEqual (right.Hash.Span);

        public static bool operator != (MerkleRoot left, MerkleRoot right)
            => !(left == right);
    }
}
