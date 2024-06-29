//
// BigEndianBigInteger.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
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
using System.Numerics;

namespace MonoTorrent
{
    struct BigEndianBigInteger : IComparable<BigEndianBigInteger>, IEquatable<BigEndianBigInteger>
    {
        public static BigEndianBigInteger Parse (string value)
            => new BigEndianBigInteger (BigInteger.Parse (value));

        readonly BigInteger Value;

#if NETSTANDARD2_0 || NET472
        public BigEndianBigInteger (ReadOnlySpan<byte> span)
        {
            var littleEndianArray = new byte[span.Length + 1];

            // Swap endian-ness and append a trailing '0' to ensure the value is treated as
            // a positive integer
            for (int i = 0; i < span.Length; i++)
                littleEndianArray[span.Length - 1 - i] = span[i];

            Value = new BigInteger (littleEndianArray);
        }
        public byte[] ToByteArray ()
        {
            byte[] littleEndianArray = Value.ToByteArray ();
            int count = littleEndianArray.Length;
            while (count > 0 && littleEndianArray[count - 1] == 0)
                count--;

            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
                result[i] = littleEndianArray[count - i - 1];
            return result;
        }
#else
        public BigEndianBigInteger (ReadOnlySpan<byte> span)
            => Value = new BigInteger (span, true, true);

        public byte[] ToByteArray ()
            => Value.ToByteArray (true, true);
#endif

        public BigEndianBigInteger (BigInteger value)
            => Value = value;

        public BigEndianBigInteger (int value)
            => Value = new BigInteger (value);

        public static BigEndianBigInteger operator + (BigEndianBigInteger left, BigEndianBigInteger right)
            => new BigEndianBigInteger (left.Value + right.Value);

        public static BigEndianBigInteger operator - (BigEndianBigInteger left, BigEndianBigInteger right)
            => new BigEndianBigInteger (left.Value - right.Value);

        public static BigEndianBigInteger operator / (BigEndianBigInteger left, int value)
            => new BigEndianBigInteger (left.Value / value);

        public static BigEndianBigInteger operator * (BigEndianBigInteger left, int value)
            => new BigEndianBigInteger (left.Value * value);

        public static BigEndianBigInteger operator << (BigEndianBigInteger value, int shift)
            => new BigEndianBigInteger (value.Value << shift);

        public static BigEndianBigInteger operator >> (BigEndianBigInteger value, int shift)
            => new BigEndianBigInteger (value.Value >> shift);

        public static bool operator > (BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value > right.Value;

        public static bool operator > (BigEndianBigInteger left, long right)
            => left.Value > right;

        public static bool operator >= (BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value >= right.Value;

        public static bool operator >= (BigEndianBigInteger left, long right)
            => left.Value >= right;

        public static bool operator < (BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value < right.Value;

        public static bool operator < (BigEndianBigInteger left, long value)
            => left.Value < value;

        public static bool operator <= (BigEndianBigInteger left, BigEndianBigInteger right)
           => left.Value <= right.Value;

        public static bool operator <= (BigEndianBigInteger left, long value)
            => left.Value <= value;

        public static bool operator == (BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value == right.Value;

        public static bool operator != (BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value != right.Value;

        public int CompareTo (BigEndianBigInteger other)
            => Value.CompareTo (other.Value);

        public override bool Equals (object? obj)
            => obj is BigEndianBigInteger val && Equals (val);

        public bool Equals (BigEndianBigInteger other)
            => other.Value == Value;

        public override int GetHashCode ()
            => Value.GetHashCode ();

        public BigEndianBigInteger ModPow (BigEndianBigInteger exponent, BigEndianBigInteger modulus)
            => new BigEndianBigInteger (BigInteger.ModPow (Value, exponent.Value, modulus.Value));
    }
}
