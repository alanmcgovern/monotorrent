//
// NodeId.cs
//
// Authors:
//   Jérémie Laval <jeremie.laval@gmail.com>
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2008 Jérémie Laval, Alan McGovern
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
using System.Linq;
using System.Runtime.InteropServices;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    class NodeId : IEquatable<NodeId>, IComparable<NodeId>, IComparable
    {
        internal static readonly NodeId Minimum = NodeId.FromMemory (new byte[20]);
        internal static readonly NodeId Maximum = NodeId.FromMemory (Enumerable.Repeat ((byte) 255, 20).ToArray ());
        static readonly Random Random = new Random ();

        public static NodeId Create ()
        {
            var b = new byte[20];
            lock (Random)
                Random.NextBytes (b);
            return NodeId.FromMemory (b);
        }

        internal static NodeId FromMemory (ReadOnlyMemory<byte> memory)
            => new NodeId (memory);

        ReadOnlyMemory<byte> Bytes { get; }

        public ReadOnlySpan<byte> Span => Bytes.Span;

        internal NodeId (BigEndianBigInteger value)
        {
            var b = value.ToByteArray ();
            if (b.Length < 20) {
                byte[] newBytes = new byte[20];
                b.AsSpan ().CopyTo (newBytes.AsSpan ().Slice (newBytes.Length - b.Length, b.Length));
                b = newBytes;
            }

            if (b.Length != 20)
                throw new ArgumentException ("The provided value cannot be represented in 160bits", nameof (value));
            Bytes = b;
        }

        internal NodeId (InfoHash infoHash)
        {
            if (infoHash is null)
                throw new ArgumentNullException (nameof (infoHash));
            Bytes = infoHash.AsMemory ();
        }

        internal NodeId (BEncodedString value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));
            if (value.Span.Length != 20)
                throw new ArgumentException ("BEncodedString should be exactly 20 bytes", nameof (value));
            Bytes = value.AsMemory ();
        }

        NodeId (ReadOnlyMemory<byte> memory)
            => (Bytes) = memory;

        public ReadOnlyMemory<byte> AsMemory ()
            => Bytes;

        public int CompareTo (object? obj)
            => CompareTo (obj as NodeId);

        public int CompareTo (NodeId? other)
            => other is null ? 1 : Span.SequenceCompareTo (other.Span);

        public override bool Equals (object? obj)
            => obj is NodeId node && node == this;

        public bool Equals (NodeId? other)
            => this == other;

        public override int GetHashCode ()
            => MemoryMarshal.Read<int> (Span);

        public override string ToString ()
            => BitConverter.ToString (Span.ToArray ());

        internal static NodeId Median (NodeId min, NodeId max)
            => new NodeId ((new BigEndianBigInteger (min.Span) + new BigEndianBigInteger (max.Span)) / 2);

        public static NodeId operator ^ (NodeId left, NodeId right)
        {
            var clone = new byte[left.Span.Length];
            for (int i = 0; i < right.Span.Length; i++)
                clone[i] = (byte)(left.Span[i] ^ right.Span[i]);
            return NodeId.FromMemory (new ReadOnlyMemory<byte> (clone));
        }

        public static NodeId operator - (NodeId first, NodeId second)
        {
            return new NodeId (new BigEndianBigInteger (first.Span) - new BigEndianBigInteger (second.Span));
        }

        public static bool operator > (NodeId first, NodeId second)
        {
            return first.CompareTo (second) > 0;
        }

        public static bool operator > (NodeId first, int second)
        {
            return new BigEndianBigInteger (first.Span) > second;
        }

        public static bool operator < (NodeId first, NodeId second)
        {
            return first.CompareTo (second) < 0;
        }

        public static bool operator < (NodeId first, int second)
        {
            return new BigEndianBigInteger (first.Span) < second;
        }

        public static bool operator <= (NodeId first, NodeId second)
        {
            return first.CompareTo (second) <= 0;
        }

        public static bool operator >= (NodeId first, NodeId second)
        {
            return first.CompareTo (second) >= 0;
        }

        public static bool operator == (NodeId? first, NodeId? second)
        {
            if (first is null)
                return second is null;
            if (second is null)
                return false;
            return first.Span.SequenceEqual (second.Span);
        }

        public static bool operator != (NodeId? first, NodeId? second)
            => !(first == second);
    }
}
