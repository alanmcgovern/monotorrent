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
        internal static readonly NodeId Minimum = new NodeId (new byte[20]);
        internal static readonly NodeId Maximum = new NodeId (Enumerable.Repeat ((byte) 255, 20).ToArray ());

        static readonly Random random = new Random ();

        public BigEndianBigInteger value;

        public byte[] Bytes { get; }

        internal NodeId (BigEndianBigInteger value)
        {
            Bytes = value.ToByteArray ();
            if (Bytes.Length < 20) {
                byte[] newBytes = new byte[20];
                Buffer.BlockCopy (Bytes, 0, newBytes, newBytes.Length - Bytes.Length, Bytes.Length);
                Bytes = newBytes;
            }

            if (Bytes.Length != 20)
                throw new ArgumentException ("The provided value cannot be represented in 160bits", nameof (value));
        }

        internal NodeId (byte[] value)
        {
            if (value.Length != 20)
                throw new ArgumentException ("The provided value cannot be represented in 160bits", nameof (value));

            Bytes = value;
        }

        internal NodeId (InfoHash infoHash)
            : this (infoHash.ToArray ())
        {

        }

        internal NodeId (BEncodedString value)
            : this (value.TextBytes)
        {

        }

        internal BEncodedString BencodedString ()
        {
            return new BEncodedString ((byte[]) Bytes.Clone ());
        }

        public int CompareTo (object obj)
        {
            return CompareTo (obj as NodeId);
        }

        public int CompareTo (NodeId other)
        {
            if (other is null)
                return 1;

            for (int i = 0; i < Bytes.Length; i++) {
                if (Bytes[i] != other.Bytes[i])
                    return Bytes[i] - other.Bytes[i];
            }
            return 0;
        }

        public override bool Equals (object obj)
        {
            return Equals (obj as NodeId);
        }

        public bool Equals (NodeId other)
        {
            return other != null && Toolbox.ByteMatch (Bytes, other.Bytes);
        }

        public override int GetHashCode ()
        {
            return Bytes[0] << 24
                        | Bytes[1] << 16
                        | Bytes[2] << 8
                        | Bytes[3];
        }

        public override string ToString ()
        {
            return BitConverter.ToString (Bytes);
        }

        internal static NodeId Median (NodeId min, NodeId max)
        {
            return new NodeId ((new BigEndianBigInteger (min.Bytes) + new BigEndianBigInteger (max.Bytes)) / 2);
        }

        public static NodeId operator ^ (NodeId left, NodeId right)
        {
            byte[] clone = (byte[]) left.Bytes.Clone ();
            for (int i = 0; i < right.Bytes.Length; i++)
                clone[i] ^= right.Bytes[i];
            return new NodeId (clone);
        }

        public static NodeId operator - (NodeId first, NodeId second)
        {
            return new NodeId (new BigEndianBigInteger (first.Bytes) - new BigEndianBigInteger (second.Bytes));
        }

        public static bool operator > (NodeId first, NodeId second)
        {
            return first.CompareTo (second) > 0;
        }

        public static bool operator > (NodeId first, int second)
        {
            return new BigEndianBigInteger (first.Bytes) > second;
        }

        public static bool operator < (NodeId first, NodeId second)
        {
            return first.CompareTo (second) < 0;
        }

        public static bool operator < (NodeId first, int second)
        {
            return new BigEndianBigInteger (first.Bytes) < second;
        }

        public static bool operator <= (NodeId first, NodeId second)
        {
            return first.CompareTo (second) <= 0;
        }

        public static bool operator >= (NodeId first, NodeId second)
        {
            return first.CompareTo (second) >= 0;
        }

        public static bool operator == (NodeId first, NodeId second)
        {
            return first is null ? second is null : first.CompareTo (second) == 0;
        }

        public static bool operator != (NodeId first, NodeId second)
        {
            return !(first == second);
        }

        public static NodeId Create ()
        {
            byte[] b = new byte[20];
            lock (random)
                random.NextBytes (b);
            return new NodeId (b);
        }
    }
}
