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
using System.Collections.Generic;
using System.Text;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    public class NodeId : IEquatable<NodeId>, IComparable<NodeId>, IComparable
    {
        static readonly Random random = new Random();

        BigInteger value;

        internal byte[] Bytes
        {
            get { return value.GetBytes(); }
        }

        internal NodeId(byte[] value)
            : this(new BigInteger(value))
        {

        }

        internal NodeId(BigInteger value)
        {
            this.value = value;
        }

        internal NodeId(BEncodedString value)
            : this(new BigInteger(value.TextBytes))
        {

        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NodeId);
        }

        public bool Equals(NodeId other)
        {
            if ((object)other == null)
                return false;

            return value.Equals(other.value);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return value.ToString();
        }

        public int CompareTo(object obj)
        {
            return CompareTo(obj as NodeId);
        }

        public int CompareTo(NodeId other)
        {
            if ((object)other == null)
                return 1;

            BigInteger.Sign s = value.Compare(other.value);
            if (s == BigInteger.Sign.Zero)
                return 0;
            else if (s == BigInteger.Sign.Positive)
                return 1;
            else return -1;
        }

        internal NodeId Xor(NodeId right)
        {
            return new NodeId(value.Xor(right.value));
        }

        public static implicit operator NodeId(int value)
        {
            return new NodeId(new BigInteger((uint)value));
        }

        public static NodeId operator -(NodeId first)
        {
            return new NodeId(first.value);
        }

        public static NodeId operator -(NodeId first, NodeId second)
        {
            return new NodeId(first.value - second.value);
        }

        public static bool operator >(NodeId first, NodeId second)
        {
            return first.value > second.value;
        }

        public static bool operator <(NodeId first, NodeId second)
        {
            return first.value < second.value;
        }

        public static bool operator <=(NodeId first, NodeId second)
        {
            return first < second || first == second;
        }

        public static bool operator >=(NodeId first, NodeId second)
        {
            return first > second || first == second;
        }

        public static NodeId operator +(NodeId first, NodeId second)
        {
            return new NodeId(first.value + second.value);
        }

        public static NodeId operator /(NodeId first, NodeId second)
        {
            return new NodeId(first.value / second.value);
        }

        public static bool operator ==(NodeId first, NodeId second)
        {
            if ((object)first == null)
                return (object)second == null;
            if ((object)second == null)
                return false;
            return first.value == second.value;
        }

        public static bool operator !=(NodeId first, NodeId second)
        {
            return first.value != second.value;
        }

        internal BEncodedString BencodedString()
        {
            return new BEncodedString(value.GetBytes());
        }

        internal NodeId Pow(uint p)
        {
            value = BigInteger.Pow(value, p);
            return this;
        }

        public static NodeId Create()
        {
            byte[] b = new byte[20];
            lock (random)
                random.NextBytes(b);
            return new NodeId(new BigInteger(b));
        }
    }
}