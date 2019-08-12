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
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    class NodeId : IEquatable<NodeId>, IComparable<NodeId>, IComparable
    {
        internal static readonly NodeId Minimum = new NodeId(new byte[20]);
        internal static readonly NodeId Maximum = new NodeId(Enumerable.Repeat ((byte)255, 20).ToArray ());

        static readonly Random random = new Random();

        public byte[] Bytes { get; }
        public BigInteger Value { get; }

        internal NodeId(BigInteger value)
        {
            Value  = value;
            Bytes = value.GetBytes ();
            if (Bytes.Length < 20) {
                var newBytes = new byte[20];
                Buffer.BlockCopy (Bytes, 0, newBytes, newBytes.Length - Bytes.Length, Bytes.Length);
                Bytes = newBytes;
            }

            if (Bytes.Length != 20)
                throw new ArgumentException ("The provided value cannot be represented in 160bits", nameof (value));
        }

        internal NodeId(byte[] value)
        {
            if (value.Length != 20)
                throw new ArgumentException ("The provided value cannot be represented in 160bits", nameof (value));

            Bytes = value;
            Value  = new BigInteger(value);
        }

        internal NodeId(InfoHash infoHash)
            : this(infoHash.ToArray ())
        {

        }

        internal NodeId(BEncodedString value)
            : this(value.TextBytes)
        {

        }

        internal BEncodedString BencodedString()
            => new BEncodedString((byte[])Bytes.Clone ());

        public int CompareTo(object obj)
            => CompareTo(obj as NodeId);

        public int CompareTo(NodeId other)
        {
            if ((object)other == null)
                return 1;

            BigInteger.Sign s = Value.Compare(other.Value);
            if (s == BigInteger.Sign.Zero)
                return 0;
            else if (s == BigInteger.Sign.Positive)
                return 1;
            else return -1;
        }

        public override bool Equals(object obj)
            => Equals(obj as NodeId);

        public bool Equals(NodeId other)
            => Value.Equals (other?.Value);

        public override int GetHashCode()
            => Value.GetHashCode();

        public override string ToString()
            => Value.ToString();

        internal static NodeId Median (NodeId min, NodeId max)
            => new NodeId ((min.Value + max.Value) / 2);

        public static NodeId operator ^(NodeId left, NodeId right)
            => new NodeId (left.Value.Xor (right.Value));

        public static NodeId operator - (NodeId first, NodeId second)
            => new NodeId (first.Value - second.Value);

        public static bool operator >(NodeId first, NodeId second)
            => first.Value > second.Value;

        public static bool operator <(NodeId first, NodeId second)
            => first.Value < second.Value;

        public static bool operator <=(NodeId first, NodeId second)
            => first.Value <= second.Value;

        public static bool operator >=(NodeId first, NodeId second)
            => first.Value >= second.Value;

        public static bool operator ==(NodeId first, NodeId second)
            => first?.Value == second?.Value;

        public static bool operator !=(NodeId first, NodeId second)
            => first?.Value != second?.Value;

        public static NodeId Create()
        {
            byte[] b = new byte[20];
            lock (random)
                random.NextBytes(b);
            return new NodeId(b);
        }
    }
}
