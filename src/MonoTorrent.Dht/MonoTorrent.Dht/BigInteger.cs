//
// BigInteger.cs - Big Integer implementation
//
// Authors:
//	Ben Maurer
//	Chew Keong TAN
//	Sebastien Pouliot <sebastien@ximian.com>
//	Pieter Philippaerts <Pieter@mentalis.org>
//
// Copyright (c) 2003 Ben Maurer
// All rights reserved
//
// Copyright (c) 2002 Chew Keong TAN
// All rights reserved.
//
// Copyright (C) 2004, 2007 Novell, Inc (http://www.novell.com)
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
using System.Security.Cryptography;

namespace MonoTorrent.Dht
{

    internal class BigInteger
    {

        #region Data Storage

        /// <summary>
        /// The Length of this BigInteger
        /// </summary>
        uint length = 1;

        /// <summary>
        /// The data for this BigInteger
        /// </summary>
        uint[] data;

        #endregion

        #region Constants

        /// <summary>
        /// Default length of a BigInteger in bytes
        /// </summary>
        const uint DEFAULT_LEN = 20;


        public enum Sign : int
        {
            Negative = -1,
            Zero = 0,
            Positive = 1
        };

        #region Exception Messages
        const string WouldReturnNegVal = "Operation would return a negative value";
        #endregion

        #endregion

        #region Constructors

        public BigInteger()
        {
            data = new uint[DEFAULT_LEN];
            this.length = DEFAULT_LEN;
        }

        public BigInteger(uint ui)
        {
            data = new uint[] { ui };
        }

        public BigInteger(Sign sign, uint len)
        {
            this.data = new uint[len];
            this.length = len;
        }

        public BigInteger(BigInteger bi)
        {
            this.data = (uint[])bi.data.Clone();
            this.length = bi.length;
        }

        public BigInteger(BigInteger bi, uint len)
        {

            this.data = new uint[len];

            for (uint i = 0; i < bi.length; i++)
                this.data[i] = bi.data[i];

            this.length = bi.length;
        }

        #endregion

        #region Conversions

        public BigInteger(byte[] inData)
        {
            length = (uint)inData.Length >> 2;
            int leftOver = inData.Length & 0x3;

            // length not multiples of 4
            if (leftOver != 0) length++;

            data = new uint[length];

            for (int i = inData.Length - 1, j = 0; i >= 3; i -= 4, j++)
            {
                data[j] = (uint)(
                    (inData[i - 3] << (3 * 8)) |
                    (inData[i - 2] << (2 * 8)) |
                    (inData[i - 1] << (1 * 8)) |
                    (inData[i])
                    );
            }

            switch (leftOver)
            {
                case 1: data[length - 1] = (uint)inData[0]; break;
                case 2: data[length - 1] = (uint)((inData[0] << 8) | inData[1]); break;
                case 3: data[length - 1] = (uint)((inData[0] << 16) | (inData[1] << 8) | inData[2]); break;
            }

            this.Normalize();
        }


        public static implicit operator BigInteger(uint value)
        {
            return (new BigInteger(value));
        }

        #endregion

        #region Operators

        public static BigInteger operator +(BigInteger bi1, BigInteger bi2)
        {
            if (bi1 == 0)
                return new BigInteger(bi2);
            else if (bi2 == 0)
                return new BigInteger(bi1);
            else
                return Kernel.AddSameSign(bi1, bi2);
        }

        public static BigInteger operator -(BigInteger bi1, BigInteger bi2)
        {
            if (bi2 == 0)
                return new BigInteger(bi1);

            if (bi1 == 0)
                throw new ArithmeticException(WouldReturnNegVal);

            switch (Kernel.Compare(bi1, bi2))
            {

                case Sign.Zero:
                    return 0;

                case Sign.Positive:
                    return Kernel.Subtract(bi1, bi2);

                case Sign.Negative:
                    throw new ArithmeticException(WouldReturnNegVal);
                default:
                    throw new Exception();
            }
        }

        public static int operator %(BigInteger bi, int i)
        {
            if (i > 0)
                return (int)Kernel.DwordMod(bi, (uint)i);
            else
                return -(int)Kernel.DwordMod(bi, (uint)-i);
        }

        public static uint operator %(BigInteger bi, uint ui)
        {
            return Kernel.DwordMod(bi, (uint)ui);
        }

        public static BigInteger operator %(BigInteger bi1, BigInteger bi2)
        {
            return Kernel.multiByteDivide(bi1, bi2)[1];
        }

        public static BigInteger operator /(BigInteger bi, int i)
        {
            if (i > 0)
                return Kernel.DwordDiv(bi, (uint)i);

            throw new ArithmeticException(WouldReturnNegVal);
        }

        public static BigInteger operator /(BigInteger bi1, BigInteger bi2)
        {
            return Kernel.multiByteDivide(bi1, bi2)[0];
        }

        public static BigInteger operator *(BigInteger bi1, BigInteger bi2)
        {
            if (bi1 == 0 || bi2 == 0) return 0;

            //
            // Validate pointers
            //
            if (bi1.data.Length < bi1.length) throw new IndexOutOfRangeException("bi1 out of range");
            if (bi2.data.Length < bi2.length) throw new IndexOutOfRangeException("bi2 out of range");

            BigInteger ret = new BigInteger(Sign.Positive, bi1.length + bi2.length);

            Kernel.Multiply(bi1.data, 0, bi1.length, bi2.data, 0, bi2.length, ret.data, 0);

            ret.Normalize();
            return ret;
        }

        public static BigInteger operator *(BigInteger bi, int i)
        {
            if (i < 0) throw new ArithmeticException(WouldReturnNegVal);
            if (i == 0) return 0;
            if (i == 1) return new BigInteger(bi);

            return Kernel.MultiplyByDword(bi, (uint)i);
        }

        public static BigInteger operator <<(BigInteger bi1, int shiftVal)
        {
            return Kernel.LeftShift(bi1, shiftVal);
        }

        public static BigInteger operator >>(BigInteger bi1, int shiftVal)
        {
            return Kernel.RightShift(bi1, shiftVal);
        }

        #endregion

        #region Bitwise

        public int BitCount()
        {
            this.Normalize();

            uint value = data[length - 1];
            uint mask = 0x80000000;
            uint bits = 32;

            while (bits > 0 && (value & mask) == 0)
            {
                bits--;
                mask >>= 1;
            }
            bits += ((length - 1) << 5);

            return (int)bits;
        }


        public bool TestBit(int bitNum)
        {
            if (bitNum < 0) throw new IndexOutOfRangeException("bitNum out of range");

            uint bytePos = (uint)bitNum >> 5;             // divide by 32
            byte bitPos = (byte)(bitNum & 0x1F);    // get the lowest 5 bits

            uint mask = (uint)1 << bitPos;
            return ((this.data[bytePos] | mask) == this.data[bytePos]);
        }

        public void SetBit(uint bitNum, bool value)
        {
            uint bytePos = bitNum >> 5;             // divide by 32

            if (bytePos < this.length)
            {
                uint mask = (uint)1 << (int)(bitNum & 0x1F);
                if (value)
                    this.data[bytePos] |= mask;
                else
                    this.data[bytePos] &= ~mask;
            }
        }

        public byte[] GetBytes()
        {
            if (this == 0) return new byte[1];

            int numBits = BitCount();
            int numBytes = numBits >> 3;
            if ((numBits & 0x7) != 0)
                numBytes++;

            byte[] result = new byte[numBytes];

            int numBytesInWord = numBytes & 0x3;
            if (numBytesInWord == 0) numBytesInWord = 4;

            int pos = 0;
            for (int i = (int)length - 1; i >= 0; i--)
            {
                uint val = data[i];
                for (int j = numBytesInWord - 1; j >= 0; j--)
                {
                    result[pos + j] = (byte)(val & 0xFF);
                    val >>= 8;
                }
                pos += numBytesInWord;
                numBytesInWord = 4;
            }
            return result;
        }

        #endregion

        #region Compare

        public static bool operator ==(BigInteger bi1, uint ui)
        {
            if (bi1.length != 1) bi1.Normalize();
            return bi1.length == 1 && bi1.data[0] == ui;
        }

        public static bool operator !=(BigInteger bi1, uint ui)
        {
            if (bi1.length != 1) bi1.Normalize();
            return !(bi1.length == 1 && bi1.data[0] == ui);
        }

        public static bool operator ==(BigInteger bi1, BigInteger bi2)
        {
            // we need to compare with null
            if ((bi1 as object) == (bi2 as object))
                return true;
            if (null == bi1 || null == bi2)
                return false;
            return Kernel.Compare(bi1, bi2) == 0;
        }

        public static bool operator !=(BigInteger bi1, BigInteger bi2)
        {
            // we need to compare with null
            if ((bi1 as object) == (bi2 as object))
                return false;
            if (null == bi1 || null == bi2)
                return true;
            return Kernel.Compare(bi1, bi2) != 0;
        }

        public static bool operator >(BigInteger bi1, BigInteger bi2)
        {
            return Kernel.Compare(bi1, bi2) > 0;
        }

        public static bool operator <(BigInteger bi1, BigInteger bi2)
        {
            return Kernel.Compare(bi1, bi2) < 0;
        }

        public static bool operator >=(BigInteger bi1, BigInteger bi2)
        {
            return Kernel.Compare(bi1, bi2) >= 0;
        }

        public static bool operator <=(BigInteger bi1, BigInteger bi2)
        {
            return Kernel.Compare(bi1, bi2) <= 0;
        }

        public Sign Compare(BigInteger bi)
        {
            return Kernel.Compare(this, bi);
        }

        #endregion

        #region Formatting

        public string ToString(uint radix)
        {
            return ToString(radix, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        }

        public string ToString(uint radix, string characterSet)
        {
            if (characterSet.Length < radix)
                throw new ArgumentException("charSet length less than radix", "characterSet");
            if (radix == 1)
                throw new ArgumentException("There is no such thing as radix one notation", "radix");

            if (this == 0) return "0";
            if (this == 1) return "1";

            string result = "";

            BigInteger a = new BigInteger(this);

            while (a != 0)
            {
                uint rem = Kernel.SingleByteDivideInPlace(a, radix);
                result = characterSet[(int)rem] + result;
            }

            return result;
        }

        #endregion

        #region Misc

        /// <summary>
        ///     Normalizes this by setting the length to the actual number of
        ///     uints used in data and by setting the sign to Sign.Zero if the
        ///     value of this is 0.
        /// </summary>
        private void Normalize()
        {
            // Normalize length
            while (length > 0 && data[length - 1] == 0) length--;

            // Check for zero
            if (length == 0)
                length++;
        }

        #endregion

        #region Object Impl

        public override int GetHashCode()
        {
            uint val = 0;

            for (uint i = 0; i < this.length; i++)
                val ^= this.data[i];

            return (int)val;
        }

        public override string ToString()
        {
            return ToString(10);
        }

        public override bool Equals(object o)
        {
            if (o == null) return false;
            if (o is int) return (int)o >= 0 && this == (uint)o;

            return Kernel.Compare(this, (BigInteger)o) == 0;
        }

        #endregion

        #region Number Theory

        public BigInteger ModPow(BigInteger exp, BigInteger n)
        {
            ModulusRing mr = new ModulusRing(n);
            return mr.Pow(this, exp);
        }

        #endregion



        public sealed class ModulusRing
        {

            BigInteger mod, constant;

            public ModulusRing(BigInteger modulus)
            {
                this.mod = modulus;

                // calculate constant = b^ (2k) / m
                uint i = mod.length << 1;

                constant = new BigInteger(Sign.Positive, i + 1);
                constant.data[i] = 0x00000001;

                constant = constant / mod;
            }

            public void BarrettReduction(BigInteger x)
            {
                BigInteger n = mod;
                uint k = n.length,
                    kPlusOne = k + 1,
                    kMinusOne = k - 1;

                // x < mod, so nothing to do.
                if (x.length < k) return;

                BigInteger q3;

                //
                // Validate pointers
                //
                if (x.data.Length < x.length) throw new IndexOutOfRangeException("x out of range");

                // q1 = x / b^ (k-1)
                // q2 = q1 * constant
                // q3 = q2 / b^ (k+1), Needs to be accessed with an offset of kPlusOne

                // TODO: We should the method in HAC p 604 to do this (14.45)
                q3 = new BigInteger(Sign.Positive, x.length - kMinusOne + constant.length);
                Kernel.Multiply(x.data, kMinusOne, x.length - kMinusOne, constant.data, 0, constant.length, q3.data, 0);

                // r1 = x mod b^ (k+1)
                // i.e. keep the lowest (k+1) words

                uint lengthToCopy = (x.length > kPlusOne) ? kPlusOne : x.length;

                x.length = lengthToCopy;
                x.Normalize();

                // r2 = (q3 * n) mod b^ (k+1)
                // partial multiplication of q3 and n

                BigInteger r2 = new BigInteger(Sign.Positive, kPlusOne);
                Kernel.MultiplyMod2p32pmod(q3.data, (int)kPlusOne, (int)q3.length - (int)kPlusOne, n.data, 0, (int)n.length, r2.data, 0, (int)kPlusOne);

                r2.Normalize();

                if (r2 <= x)
                {
                    Kernel.MinusEq(x, r2);
                }
                else
                {
                    BigInteger val = new BigInteger(Sign.Positive, kPlusOne + 1);
                    val.data[kPlusOne] = 0x00000001;

                    Kernel.MinusEq(val, r2);
                    Kernel.PlusEq(x, val);
                }

                while (x >= n)
                    Kernel.MinusEq(x, n);
            }

            public BigInteger Multiply(BigInteger a, BigInteger b)
            {
                if (a == 0 || b == 0) return 0;

                if (a > mod)
                    a %= mod;

                if (b > mod)
                    b %= mod;

                BigInteger ret = a * b;
                BarrettReduction(ret);

                return ret;
            }

            public BigInteger Difference(BigInteger a, BigInteger b)
            {
                Sign cmp = Kernel.Compare(a, b);
                BigInteger diff;

                switch (cmp)
                {
                    case Sign.Zero:
                        return 0;
                    case Sign.Positive:
                        diff = a - b; break;
                    case Sign.Negative:
                        diff = b - a; break;
                    default:
                        throw new Exception();
                }

                if (diff >= mod)
                {
                    if (diff.length >= mod.length << 1)
                        diff %= mod;
                    else
                        BarrettReduction(diff);
                }
                if (cmp == Sign.Negative)
                    diff = mod - diff;
                return diff;
            }

            public BigInteger Pow(BigInteger a, BigInteger k)
            {
                BigInteger b = new BigInteger(1);
                if (k == 0)
                    return b;

                BigInteger A = a;
                if (k.TestBit(0))
                    b = a;

                int bitCount = k.BitCount();
                for (int i = 1; i < bitCount; i++)
                {
                    A = Multiply(A, A);
                    if (k.TestBit(i))
                        b = Multiply(A, b);
                }
                return b;
            }


            public BigInteger Pow(uint b, BigInteger exp)
            {
                return Pow(new BigInteger(b), exp);
            }

        }

        private sealed class Kernel
        {

            #region Addition/Subtraction

            /// <summary>
            /// Adds two numbers with the same sign.
            /// </summary>
            /// <param name="bi1">A BigInteger</param>
            /// <param name="bi2">A BigInteger</param>
            /// <returns>bi1 + bi2</returns>
            public static BigInteger AddSameSign(BigInteger bi1, BigInteger bi2)
            {
                uint[] x, y;
                uint yMax, xMax, i = 0;

                // x should be bigger
                if (bi1.length < bi2.length)
                {
                    x = bi2.data;
                    xMax = bi2.length;
                    y = bi1.data;
                    yMax = bi1.length;
                }
                else
                {
                    x = bi1.data;
                    xMax = bi1.length;
                    y = bi2.data;
                    yMax = bi2.length;
                }

                BigInteger result = new BigInteger(Sign.Positive, xMax + 1);

                uint[] r = result.data;

                ulong sum = 0;

                // Add common parts of both numbers
                do
                {
                    sum = ((ulong)x[i]) + ((ulong)y[i]) + sum;
                    r[i] = (uint)sum;
                    sum >>= 32;
                } while (++i < yMax);

                // Copy remainder of longer number while carry propagation is required
                bool carry = (sum != 0);

                if (carry)
                {

                    if (i < xMax)
                    {
                        do
                            carry = ((r[i] = x[i] + 1) == 0);
                        while (++i < xMax && carry);
                    }

                    if (carry)
                    {
                        r[i] = 1;
                        result.length = ++i;
                        return result;
                    }
                }

                // Copy the rest
                if (i < xMax)
                {
                    do
                        r[i] = x[i];
                    while (++i < xMax);
                }

                result.Normalize();
                return result;
            }

            public static BigInteger Subtract(BigInteger big, BigInteger small)
            {
                BigInteger result = new BigInteger(Sign.Positive, big.length);

                uint[] r = result.data, b = big.data, s = small.data;
                uint i = 0, c = 0;

                do
                {

                    uint x = s[i];
                    if (((x += c) < c) | ((r[i] = b[i] - x) > ~x))
                        c = 1;
                    else
                        c = 0;

                } while (++i < small.length);

                if (i == big.length) goto fixup;

                if (c == 1)
                {
                    do
                        r[i] = b[i] - 1;
                    while (b[i++] == 0 && i < big.length);

                    if (i == big.length) goto fixup;
                }

                do
                    r[i] = b[i];
                while (++i < big.length);

            fixup:

                result.Normalize();
                return result;
            }

            public static void MinusEq(BigInteger big, BigInteger small)
            {
                uint[] b = big.data, s = small.data;
                uint i = 0, c = 0;

                do
                {
                    uint x = s[i];
                    if (((x += c) < c) | ((b[i] -= x) > ~x))
                        c = 1;
                    else
                        c = 0;
                } while (++i < small.length);

                if (i == big.length) goto fixup;

                if (c == 1)
                {
                    do
                        b[i]--;
                    while (b[i++] == 0 && i < big.length);
                }

            fixup:

                // Normalize length
                while (big.length > 0 && big.data[big.length - 1] == 0) big.length--;

                // Check for zero
                if (big.length == 0)
                    big.length++;

            }

            public static void PlusEq(BigInteger bi1, BigInteger bi2)
            {
                uint[] x, y;
                uint yMax, xMax, i = 0;
                bool flag = false;

                // x should be bigger
                if (bi1.length < bi2.length)
                {
                    flag = true;
                    x = bi2.data;
                    xMax = bi2.length;
                    y = bi1.data;
                    yMax = bi1.length;
                }
                else
                {
                    x = bi1.data;
                    xMax = bi1.length;
                    y = bi2.data;
                    yMax = bi2.length;
                }

                uint[] r = bi1.data;

                ulong sum = 0;

                // Add common parts of both numbers
                do
                {
                    sum += ((ulong)x[i]) + ((ulong)y[i]);
                    r[i] = (uint)sum;
                    sum >>= 32;
                } while (++i < yMax);

                // Copy remainder of longer number while carry propagation is required
                bool carry = (sum != 0);

                if (carry)
                {

                    if (i < xMax)
                    {
                        do
                            carry = ((r[i] = x[i] + 1) == 0);
                        while (++i < xMax && carry);
                    }

                    if (carry)
                    {
                        r[i] = 1;
                        bi1.length = ++i;
                        return;
                    }
                }

                // Copy the rest
                if (flag && i < xMax - 1)
                {
                    do
                        r[i] = x[i];
                    while (++i < xMax);
                }

                bi1.length = xMax + 1;
                bi1.Normalize();
            }

            #endregion

            #region Compare

            /// <summary>
            /// Compares two BigInteger
            /// </summary>
            /// <param name="bi1">A BigInteger</param>
            /// <param name="bi2">A BigInteger</param>
            /// <returns>The sign of bi1 - bi2</returns>
            public static Sign Compare(BigInteger bi1, BigInteger bi2)
            {
                //
                // Step 1. Compare the lengths
                //
                uint l1 = bi1.length, l2 = bi2.length;

                while (l1 > 0 && bi1.data[l1 - 1] == 0) l1--;
                while (l2 > 0 && bi2.data[l2 - 1] == 0) l2--;

                if (l1 == 0 && l2 == 0) return Sign.Zero;

                // bi1 len < bi2 len
                if (l1 < l2) return Sign.Negative;
                // bi1 len > bi2 len
                else if (l1 > l2) return Sign.Positive;

                //
                // Step 2. Compare the bits
                //

                uint pos = l1 - 1;

                while (pos != 0 && bi1.data[pos] == bi2.data[pos]) pos--;

                if (bi1.data[pos] < bi2.data[pos])
                    return Sign.Negative;
                else if (bi1.data[pos] > bi2.data[pos])
                    return Sign.Positive;
                else
                    return Sign.Zero;
            }

            #endregion

            #region Division

            #region Dword

            /// <summary>
            /// Performs n / d and n % d in one operation.
            /// </summary>
            /// <param name="n">A BigInteger, upon exit this will hold n / d</param>
            /// <param name="d">The divisor</param>
            /// <returns>n % d</returns>
            public static uint SingleByteDivideInPlace(BigInteger n, uint d)
            {
                ulong r = 0;
                uint i = n.length;

                while (i-- > 0)
                {
                    r <<= 32;
                    r |= n.data[i];
                    n.data[i] = (uint)(r / d);
                    r %= d;
                }
                n.Normalize();

                return (uint)r;
            }

            public static uint DwordMod(BigInteger n, uint d)
            {
                ulong r = 0;
                uint i = n.length;

                while (i-- > 0)
                {
                    r <<= 32;
                    r |= n.data[i];
                    r %= d;
                }

                return (uint)r;
            }

            public static BigInteger DwordDiv(BigInteger n, uint d)
            {
                BigInteger ret = new BigInteger(Sign.Positive, n.length);

                ulong r = 0;
                uint i = n.length;

                while (i-- > 0)
                {
                    r <<= 32;
                    r |= n.data[i];
                    ret.data[i] = (uint)(r / d);
                    r %= d;
                }
                ret.Normalize();

                return ret;
            }

            public static BigInteger[] DwordDivMod(BigInteger n, uint d)
            {
                BigInteger ret = new BigInteger(Sign.Positive, n.length);

                ulong r = 0;
                uint i = n.length;

                while (i-- > 0)
                {
                    r <<= 32;
                    r |= n.data[i];
                    ret.data[i] = (uint)(r / d);
                    r %= d;
                }
                ret.Normalize();

                BigInteger rem = (uint)r;

                return new BigInteger[] { ret, rem };
            }

            #endregion

            #region BigNum

            public static BigInteger[] multiByteDivide(BigInteger bi1, BigInteger bi2)
            {
                if (Kernel.Compare(bi1, bi2) == Sign.Negative)
                    return new BigInteger[2] { 0, new BigInteger(bi1) };

                bi1.Normalize(); bi2.Normalize();

                if (bi2.length == 1)
                    return DwordDivMod(bi1, bi2.data[0]);

                uint remainderLen = bi1.length + 1;
                int divisorLen = (int)bi2.length + 1;

                uint mask = 0x80000000;
                uint val = bi2.data[bi2.length - 1];
                int shift = 0;
                int resultPos = (int)bi1.length - (int)bi2.length;

                while (mask != 0 && (val & mask) == 0)
                {
                    shift++; mask >>= 1;
                }

                BigInteger quot = new BigInteger(Sign.Positive, bi1.length - bi2.length + 1);
                BigInteger rem = (bi1 << shift);

                uint[] remainder = rem.data;

                bi2 = bi2 << shift;

                int j = (int)(remainderLen - bi2.length);
                int pos = (int)remainderLen - 1;

                uint firstDivisorByte = bi2.data[bi2.length - 1];
                ulong secondDivisorByte = bi2.data[bi2.length - 2];

                while (j > 0)
                {
                    ulong dividend = ((ulong)remainder[pos] << 32) + (ulong)remainder[pos - 1];

                    ulong q_hat = dividend / (ulong)firstDivisorByte;
                    ulong r_hat = dividend % (ulong)firstDivisorByte;

                    do
                    {

                        if (q_hat == 0x100000000 ||
                            (q_hat * secondDivisorByte) > ((r_hat << 32) + remainder[pos - 2]))
                        {
                            q_hat--;
                            r_hat += (ulong)firstDivisorByte;

                            if (r_hat < 0x100000000)
                                continue;
                        }
                        break;
                    } while (true);

                    //
                    // At this point, q_hat is either exact, or one too large
                    // (more likely to be exact) so, we attempt to multiply the
                    // divisor by q_hat, if we get a borrow, we just subtract
                    // one from q_hat and add the divisor back.
                    //

                    uint t;
                    uint dPos = 0;
                    int nPos = pos - divisorLen + 1;
                    ulong mc = 0;
                    uint uint_q_hat = (uint)q_hat;
                    do
                    {
                        mc += (ulong)bi2.data[dPos] * (ulong)uint_q_hat;
                        t = remainder[nPos];
                        remainder[nPos] -= (uint)mc;
                        mc >>= 32;
                        if (remainder[nPos] > t) mc++;
                        dPos++; nPos++;
                    } while (dPos < divisorLen);

                    nPos = pos - divisorLen + 1;
                    dPos = 0;

                    // Overestimate
                    if (mc != 0)
                    {
                        uint_q_hat--;
                        ulong sum = 0;

                        do
                        {
                            sum = ((ulong)remainder[nPos]) + ((ulong)bi2.data[dPos]) + sum;
                            remainder[nPos] = (uint)sum;
                            sum >>= 32;
                            dPos++; nPos++;
                        } while (dPos < divisorLen);

                    }

                    quot.data[resultPos--] = (uint)uint_q_hat;

                    pos--;
                    j--;
                }

                quot.Normalize();
                rem.Normalize();
                BigInteger[] ret = new BigInteger[2] { quot, rem };

                if (shift != 0)
                    ret[1] >>= shift;

                return ret;
            }

            #endregion

            #endregion

            #region Shift
            public static BigInteger LeftShift(BigInteger bi, int n)
            {
                if (n == 0) return new BigInteger(bi, bi.length + 1);

                int w = n >> 5;
                n &= ((1 << 5) - 1);

                BigInteger ret = new BigInteger(Sign.Positive, bi.length + 1 + (uint)w);

                uint i = 0, l = bi.length;
                if (n != 0)
                {
                    uint x, carry = 0;
                    while (i < l)
                    {
                        x = bi.data[i];
                        ret.data[i + w] = (x << n) | carry;
                        carry = x >> (32 - n);
                        i++;
                    }
                    ret.data[i + w] = carry;
                }
                else
                {
                    while (i < l)
                    {
                        ret.data[i + w] = bi.data[i];
                        i++;
                    }
                }

                ret.Normalize();
                return ret;
            }

            public static BigInteger RightShift(BigInteger bi, int n)
            {
                if (n == 0) return new BigInteger(bi);

                int w = n >> 5;
                int s = n & ((1 << 5) - 1);

                BigInteger ret = new BigInteger(Sign.Positive, bi.length - (uint)w + 1);
                uint l = (uint)ret.data.Length - 1;

                if (s != 0)
                {

                    uint x, carry = 0;

                    while (l-- > 0)
                    {
                        x = bi.data[l + w];
                        ret.data[l] = (x >> n) | carry;
                        carry = x << (32 - n);
                    }
                }
                else
                {
                    while (l-- > 0)
                        ret.data[l] = bi.data[l + w];

                }
                ret.Normalize();
                return ret;
            }

            #endregion

            #region Multiply

            public static BigInteger MultiplyByDword(BigInteger n, uint f)
            {
                BigInteger ret = new BigInteger(Sign.Positive, n.length + 1);

                uint i = 0;
                ulong c = 0;

                do
                {
                    c += (ulong)n.data[i] * (ulong)f;
                    ret.data[i] = (uint)c;
                    c >>= 32;
                } while (++i < n.length);
                ret.data[i] = (uint)c;
                ret.Normalize();
                return ret;

            }

            /// <summary>
            /// Multiplies the data in x [xOffset:xOffset+xLen] by
            /// y [yOffset:yOffset+yLen] and puts it into
            /// d [dOffset:dOffset+xLen+yLen].
            /// </summary>
            /// <remarks>
            /// This code is unsafe! It is the caller's responsibility to make
            /// sure that it is safe to access x [xOffset:xOffset+xLen],
            /// y [yOffset:yOffset+yLen], and d [dOffset:dOffset+xLen+yLen].
            /// </remarks>
            public static unsafe void Multiply(uint[] x, uint xOffset, uint xLen, uint[] y, uint yOffset, uint yLen, uint[] d, uint dOffset)
            {
                fixed (uint* xx = x, yy = y, dd = d)
                {
                    uint* xP = xx + xOffset,
                        xE = xP + xLen,
                        yB = yy + yOffset,
                        yE = yB + yLen,
                        dB = dd + dOffset;

                    for (; xP < xE; xP++, dB++)
                    {

                        if (*xP == 0) continue;

                        ulong mcarry = 0;

                        uint* dP = dB;
                        for (uint* yP = yB; yP < yE; yP++, dP++)
                        {
                            mcarry += ((ulong)*xP * (ulong)*yP) + (ulong)*dP;

                            *dP = (uint)mcarry;
                            mcarry >>= 32;
                        }

                        if (mcarry != 0)
                            *dP = (uint)mcarry;
                    }
                }
            }

            /// <summary>
            /// Multiplies the data in x [xOffset:xOffset+xLen] by
            /// y [yOffset:yOffset+yLen] and puts the low mod words into
            /// d [dOffset:dOffset+mod].
            /// </summary>
            /// <remarks>
            /// This code is unsafe! It is the caller's responsibility to make
            /// sure that it is safe to access x [xOffset:xOffset+xLen],
            /// y [yOffset:yOffset+yLen], and d [dOffset:dOffset+mod].
            /// </remarks>
            public static unsafe void MultiplyMod2p32pmod(uint[] x, int xOffset, int xLen, uint[] y, int yOffest, int yLen, uint[] d, int dOffset, int mod)
            {
                fixed (uint* xx = x, yy = y, dd = d)
                {
                    uint* xP = xx + xOffset,
                        xE = xP + xLen,
                        yB = yy + yOffest,
                        yE = yB + yLen,
                        dB = dd + dOffset,
                        dE = dB + mod;

                    for (; xP < xE; xP++, dB++)
                    {

                        if (*xP == 0) continue;

                        ulong mcarry = 0;
                        uint* dP = dB;
                        for (uint* yP = yB; yP < yE && dP < dE; yP++, dP++)
                        {
                            mcarry += ((ulong)*xP * (ulong)*yP) + (ulong)*dP;

                            *dP = (uint)mcarry;
                            mcarry >>= 32;
                        }

                        if (mcarry != 0 && dP < dE)
                            *dP = (uint)mcarry;
                    }
                }
            }


            #endregion

            #region Number Theory

            public static BigInteger gcd(BigInteger a, BigInteger b)
            {
                BigInteger x = a;
                BigInteger y = b;

                BigInteger g = y;

                while (x.length > 1)
                {
                    g = x;
                    x = y % x;
                    y = g;

                }
                if (x == 0) return g;

                // TODO: should we have something here if we can convert to long?

                //
                // Now we can just do it with single precision. I am using the binary gcd method,
                // as it should be faster.
                //

                uint yy = x.data[0];
                uint xx = y % yy;

                int t = 0;

                while (((xx | yy) & 1) == 0)
                {
                    xx >>= 1; yy >>= 1; t++;
                }
                while (xx != 0)
                {
                    while ((xx & 1) == 0) xx >>= 1;
                    while ((yy & 1) == 0) yy >>= 1;
                    if (xx >= yy)
                        xx = (xx - yy) >> 1;
                    else
                        yy = (yy - xx) >> 1;
                }

                return yy << t;
            }


            public static BigInteger modInverse(BigInteger bi, BigInteger modulus)
            {
                if (modulus.length == 1) return modInverse(bi, modulus.data[0]);

                BigInteger[] p = { 0, 1 };
                BigInteger[] q = new BigInteger[2];    // quotients
                BigInteger[] r = { 0, 0 };             // remainders

                int step = 0;

                BigInteger a = modulus;
                BigInteger b = bi;

                ModulusRing mr = new ModulusRing(modulus);

                while (b != 0)
                {

                    if (step > 1)
                    {

                        BigInteger pval = mr.Difference(p[0], p[1] * q[0]);
                        p[0] = p[1]; p[1] = pval;
                    }

                    BigInteger[] divret = multiByteDivide(a, b);

                    q[0] = q[1]; q[1] = divret[0];
                    r[0] = r[1]; r[1] = divret[1];
                    a = b;
                    b = divret[1];

                    step++;
                }

                if (r[0] != 1)
                    throw (new ArithmeticException("No inverse!"));

                return mr.Difference(p[0], p[1] * q[0]);

            }
            #endregion
        }

        internal BigInteger Xor(BigInteger other)
        {
            BigInteger b = new BigInteger(new byte[20]);
                      
            int len = (int)Math.Min(length, other.length);

            for (int i = 0; i < len; i++)
                b.data[i] = this.data[i] ^ other.data[i];
            
            b.length=5;// 20/4
            b.Normalize();
      
            return b;
        }
        
        internal static BigInteger Pow(BigInteger value, uint p)
        {
            BigInteger b = value;
            for (int i = 0; i < p; i++)
                value = value * b;
            return value;
        }
    }
}