//
// BigEndianBigInteger.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using MonoTorrent.Client;

namespace MonoTorrent
{
    public struct BigEndianBigInteger : IComparable<BigEndianBigInteger>, IEquatable<BigEndianBigInteger>
    {
#if !NETSTANDARD2_1
        // If we are not targeting netstandard 2.1 then the methods we want to use may exist
        // at runtime even though they do not exist at compiletime. Let's do a quick reflection
        // check for the .ctor and ToByteArray overloads which allow us to ensure the byte[] is
        // always treated as big endian and unsigned.
        readonly static Func<byte[], BigInteger> OptimisedCtor;
        readonly static Func<BigInteger, byte[]> OptimisedToByteArray;

        static BigEndianBigInteger ()
        {
            Type readonlySpanByte = null;
            var ctor = typeof (BigInteger).GetConstructors ()
                .FirstOrDefault (c => {
                    var parameters = c.GetParameters ();
                    if (parameters.Length != 3)
                        return false;
                    if (!parameters [0].ParameterType.Name.Contains ("ReadOnlySpan"))
                        return false;
                    if (parameters [0].ParameterType.GenericTypeArguments.FirstOrDefault () != typeof (byte))
                        return false;

                    readonlySpanByte = parameters[0].ParameterType;
                    if (parameters [1].ParameterType != typeof (bool))
                        return false;
                    if (parameters [2].ParameterType != typeof (bool))
                        return false;
                    return true;
                });

            if (ctor == null) {
                OptimisedCtor = FallbackConstructor;
            } else {
                var arrayParam = Expression.Parameter (typeof (byte[]));

                OptimisedCtor = Expression.Lambda<Func<byte[], BigInteger>> (
                    Expression.New (ctor, Expression.New (readonlySpanByte.GetConstructor (new [] { typeof (byte[]) }), arrayParam), Expression.Constant (true), Expression.Constant (true)),
                    arrayParam
                ).Compile ();
           }

            var method = typeof (BigInteger).GetMethod ("ToByteArray", new [] { typeof (bool), typeof (bool) });
            if (method == null) {
                OptimisedToByteArray = FallbackToBigEndianByteArray;
            } else {
                var param = Expression.Parameter (typeof (BigInteger));
                OptimisedToByteArray = Expression.Lambda<Func<BigInteger, byte[]>> (
                    Expression.Call (param, method, Expression.Constant (true), Expression.Constant (true)),
                    param
                ).Compile ();
            }
        }

        internal static BigInteger FallbackConstructor(byte[] value)
        {
            var littleEndianArray = new byte[value.Length + 1];

            // Swap endian-ness and append a trailing '0' to ensure the value is treated as
            // a positive integer
            for (int i = 0; i < value.Length; i++)
                littleEndianArray[value.Length - 1 - i] = value[i];

            return new BigInteger(littleEndianArray);
        }

        internal static byte[] FallbackToBigEndianByteArray(BigEndianBigInteger value)
            => FallbackToBigEndianByteArray(value.Value);

        internal static byte[] FallbackToBigEndianByteArray(BigInteger value)
        {
            var littleEndianArray = value.ToByteArray();
            int count = littleEndianArray.Length;
            while (count > 0 && littleEndianArray[count - 1] == 0)
                count--;

            var result = new byte[count];
            for (int i = 0; i < count; i++)
                result[i] = littleEndianArray[count - i - 1];
            return result;
        }
#endif

        public static BigEndianBigInteger Parse (string value)
            => new BigEndianBigInteger (BigInteger.Parse (value));

        BigInteger Value { get; }

        public BigEndianBigInteger (BigInteger value)
        {
            Value = value;
        }

        public BigEndianBigInteger (int value)
        {
            Value = new BigInteger (value);
        }

        public BigEndianBigInteger (byte[] value)
        {
#if NETSTANDARD2_1
            Value = new BigInteger (littleEndianArray, true, true);
#else
            Value = OptimisedCtor (value);
#endif
        }

        public static BigEndianBigInteger operator +(BigEndianBigInteger left, BigEndianBigInteger right)
            => new BigEndianBigInteger (left.Value + right.Value);

        public static BigEndianBigInteger operator -(BigEndianBigInteger left, BigEndianBigInteger right)
            => new BigEndianBigInteger (left.Value - right.Value);

        public static BigEndianBigInteger operator /(BigEndianBigInteger left, int value)
            => new BigEndianBigInteger (left.Value / value);

       public static BigEndianBigInteger operator *(BigEndianBigInteger left, int value)
            => new BigEndianBigInteger (left.Value * value);

        public static BigEndianBigInteger operator << (BigEndianBigInteger value, int shift)
            => new BigEndianBigInteger (value.Value << shift);

        public static BigEndianBigInteger operator >> (BigEndianBigInteger value, int shift)
            => new BigEndianBigInteger (value.Value >> shift);

        public static bool operator >(BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value > right.Value;

        public static bool operator >(BigEndianBigInteger left, long right)
            => left.Value > right;

        public static bool operator >=(BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value >= right.Value;

        public static bool operator >=(BigEndianBigInteger left, long right)
            => left.Value >= right;

        public static bool operator <(BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value < right.Value;

        public static bool operator <(BigEndianBigInteger left, long value)
            => left.Value < value;

        public static bool operator <=(BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value <= right.Value;

        public static bool operator <=(BigEndianBigInteger left, long value)
            => left.Value <= value;

        public static bool operator ==(BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value == right.Value;

        public static bool operator !=(BigEndianBigInteger left, BigEndianBigInteger right)
            => left.Value != right.Value;

        public int CompareTo (BigEndianBigInteger other)
            => Value.CompareTo (other.Value);

        public override bool Equals (object obj)
            => obj is BigEndianBigInteger val && Equals(val);

        public bool Equals (BigEndianBigInteger other)
            => other.Value == Value;

        public override int GetHashCode ()
            => Value.GetHashCode ();

        public BigEndianBigInteger ModPow (BigEndianBigInteger exponent, BigEndianBigInteger modulus)
            => new BigEndianBigInteger (BigInteger.ModPow (Value, exponent.Value, modulus.Value));

        public byte[] ToByteArray ()
#if NETSTANDARD2_1
            => Value.ToByteArray (true, true);
#else
            => OptimisedToByteArray (Value);
#endif
    }
}
