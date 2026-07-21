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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoTorrent
{
    public readonly struct MerkleRoot : IEquatable<MerkleRoot>
    {
        const int StorageLength = 32;
        [InlineArray (StorageLength)]
        struct Storage
        {
            internal byte _element;
        }
        readonly Storage Hash;

        public ReadOnlySpan<byte> Span
            => MemoryMarshal.CreateReadOnlySpan (in Hash._element, StorageLength);

        public static MerkleRoot FromMemory (ReadOnlyMemory<byte> hash)
            => new MerkleRoot (hash.Span);

        public MerkleRoot (ReadOnlySpan<byte> hash)
            => hash.CopyTo (Hash);

        public override bool Equals (object? obj)
            => obj is MerkleRoot other && Equals (other);

        public bool Equals (MerkleRoot other)
            => Span.SequenceEqual (other.Hash);

        public override int GetHashCode ()
        {
            var hashcode = new HashCode ();
            hashcode.AddBytes (Hash);
            return hashcode.ToHashCode ();
        }

        public override string ToString ()
            => Convert.ToHexString (Hash);

        public static bool operator == (MerkleRoot left, MerkleRoot right)
            => left.Span.SequenceEqual (right.Hash);

        public static bool operator != (MerkleRoot left, MerkleRoot right)
            => !(left == right);
    }
}
