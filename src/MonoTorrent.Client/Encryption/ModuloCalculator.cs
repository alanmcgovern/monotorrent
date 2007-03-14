//
// ModuloCalculator.cs
//
// Authors:
//   Yiduo Wang planetbeing@gmail.com
//
// Copyright (C) 2007 Yiduo Wang
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

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    /// Class to facilitate the calculation of a^b mod p, where a and b are large integers
    /// and p is the predefined prime from message stream encryption.
    /// </summary>
    public class ModuloCalculator
    {
        // predefined prime P for message stream encryption
        private static UInt64[] thePrime = new UInt64[] {
            0x0000000000000000,
            0xFFFFFFFFFFFFFFFF, 0xC90FDAA22168C234, 0xC4C6628B80DC1CD1,
            0x29024E088A67CC74, 0x020BBEA63B139B22, 0x514A08798E3404DD,
            0xEF9519B3CD3A431B, 0x302B0A6DF25F1437, 0x4FE1356D6D51C245,
            0xE485B576625E7EC6, 0xF44C42E9A63A3621, 0x0000000000090563
        };

        private static UInt64[] precalcMult = new UInt64[] {
            0x0000000000000000,
            0xA6130C9D54DA3D53, 0x8D001CB98C6F28C7, 0xA95D0ECBC5679266,
            0xAFBC083554E85EE3, 0x3361BFF33A35E04B, 0x112CE56282E51749,
            0xD515B23CD4ACE6DD, 0x3423ECEE2DDC6158, 0xD4C85168A99A751F,
            0x6D9F175993AF6D83, 0xFF0CA2A3D838C985, 0x95F0194046281A8E
        };

        private static UInt64[] precalcInit = new UInt64[] {
            0x0000000000000000,
            0x0000000000000000, 0x36F0255DDE973DCB, 0x3B399D747F23E32E,
            0xD6FDB1F77598338B, 0xFDF44159C4EC64DD, 0xAEB5F78671CBFB22,
            0x106AE64C32C5BCE4, 0xCFD4F5920DA0EBC8, 0xB01ECA9292AE3DBA,
            0x1B7A4A899DA18139, 0x0BB3BD1659C5C9DE, 0xFFFFFFFFFFF6FA9D
        };

        public static UInt64[] ONE = new UInt64[] {
            0x0000000000000000,
            0x0000000000000000, 0x0000000000000000, 0x0000000000000000,
            0x0000000000000000, 0x0000000000000000, 0x0000000000000000,
            0x0000000000000000, 0x0000000000000000, 0x0000000000000000,
            0x0000000000000000, 0x0000000000000000, 0x0000000000000001
        };

        public static UInt64[] TWO = new UInt64[] {
            0x0000000000000000,
            0x0000000000000000, 0x0000000000000000, 0x0000000000000000,
            0x0000000000000000, 0x0000000000000000, 0x0000000000000000,
            0x0000000000000000, 0x0000000000000000, 0x0000000000000000,
            0x0000000000000000, 0x0000000000000000, 0x0000000000000002
        };

        private static void Increment(UInt64[] a, UInt64[] b)
        {
            UInt64 carry = 0;
            UInt64 left = 0;
            UInt64 toInc = 0;

            for (int i = 12; i >= 0; i--)
            {
                left = UInt64.MaxValue - a[i];
                toInc = b[i] + carry;

                if (left < toInc)
                {
                    a[i] = toInc - left - 1;
                    carry = 1;
                }
                else
                {
                    a[i] += toInc;

                    if (b[i] == UInt64.MaxValue && carry == 1)
                        carry = 1;
                    else
                        carry = 0;
                }
            }
        }

        private static void Decrement(UInt64[] a, UInt64[] b)
        {
            UInt64 carry = 0;

            for (int i = 12; i >= 0; i--)
            {
                if (a[i] < b[i])
                {
                    a[i] = a[i] - b[i] - carry;
                    carry = 1;
                }
                else
                {
                    a[i] = a[i] - b[i] - carry;
                    carry = 0;
                }
            }
        }

        private static void Half(UInt64[] a)
        {
            UInt64 carry = 0;

            for (int i = 0; i < 13; i++)
            {
                if ((a[i] & 1) != 0)
                {
                    a[i] = (a[i] >> 1) + carry;
                    carry = 0x8000000000000000;
                }
                else
                {
                    a[i] = (a[i] >> 1) + carry;
                    carry = 0;
                }
            }
        }

        private static UInt64[] Multiply(UInt64[] a, UInt64[] b)
        {
            UInt64[] c = new UInt64[13];

            for (int i = 0; i < 13; i++)
            {
                c[i] = 0;
            }

            for (int i = 12; i >= 1; i--)
            {
                for (int j = 0; j < 64; j++)
                {
                    if ((a[i] & ((UInt64)1 << j)) != 0)
                    {
                        Increment(c, b);
                    }

                    if ((c[12] & 1) != 0)
                    {
                        Increment(c, thePrime);
                    }

                    Half(c);

                    if (c[0] != 0)
                    {
                        Decrement(c, thePrime);
                    }
                }
            }

            return c;
        }

        public static byte[] Calculate(byte[] a, byte[] b)
        {
            return UInt64ToBytes(Calculate(BytesToUInt64(a), BytesToUInt64(b)));
        }

        public static byte[] Calculate(UInt64[] a, byte[] b)
        {
            return UInt64ToBytes(Calculate(a, BytesToUInt64(b)));
        }

        public static byte[] Calculate(byte[] a, UInt64[] b)
        {
            return UInt64ToBytes(Calculate(BytesToUInt64(a), b));
        }

        public static UInt64[] Calculate(UInt64[] a, UInt64[] b)
        {
            UInt64[] r = new UInt64[13];
            UInt64[] c;

            precalcInit.CopyTo(r, 0);
            c = Multiply(a, precalcMult);

            int mostSignificant = 1;
            while (b[mostSignificant] == 0)
            {
                mostSignificant++;
            }

            for (int i = 12; i >= mostSignificant; i--)
            {
                for (int j = 0; j < 64; j++)
                {
                    if ((b[i] & ((UInt64)1 << j)) != 0)
                    {
                        r = Multiply(r, c);
                    }

                    c = Multiply(c, c);
                }
            }

            r = Multiply(r, ONE);

            return r;
        }

        public static string GetString(UInt64[] a)
        {
            string result = "";
            foreach (UInt64 integer in a)
            {
                result += integer.ToString("X16");
            }
            return result;
        }

        public static string GetString(byte[] a)
        {
            string result = "";
            foreach (byte integer in a)
            {
                result += integer.ToString("X2");
            }
            return result;
        }

        public static byte[] GetByte(string hex)
        {
            char[] hexArr = hex.ToCharArray();
            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hexArr[i * 2].ToString() + hexArr[i * 2 + 1].ToString(), System.Globalization.NumberStyles.HexNumber);
            }

            return bytes;
        }

        private static UInt64[] BytesToUInt64(byte[] bytes)
        {
            UInt64[] result = new UInt64[13];

            result[0] = 0;

            for (int i = 1; i < 13; i++)
            {
                result[i] = ((UInt64)bytes[(i - 1) * 8] << (int)56) +
                    ((UInt64)bytes[(i - 1) * 8 + 1] << (int)48) +
                    ((UInt64)bytes[(i - 1) * 8 + 2] << (int)40) +
                    ((UInt64)bytes[(i - 1) * 8 + 3] << (int)32) +
                    ((UInt64)bytes[(i - 1) * 8 + 4] << (int)24) +
                    ((UInt64)bytes[(i - 1) * 8 + 5] << (int)16) +
                    ((UInt64)bytes[(i - 1) * 8 + 6] << (int)8) +
                    ((UInt64)bytes[(i - 1) * 8 + 7]);
            }

            return result;
        }

        private static byte[] UInt64ToBytes(UInt64[] integers)
        {
            byte[] result = new byte[96];

            for (int i = 1; i < 13; i++)
            {
                result[(i - 1) * 8] = (byte)((integers[i] >> 56) & 0xff);
                result[(i - 1) * 8 + 1] = (byte)((integers[i] >> 48) & 0xff);
                result[(i - 1) * 8 + 2] = (byte)((integers[i] >> 40) & 0xff);
                result[(i - 1) * 8 + 3] = (byte)((integers[i] >> 32) & 0xff);
                result[(i - 1) * 8 + 4] = (byte)((integers[i] >> 24) & 0xff);
                result[(i - 1) * 8 + 5] = (byte)((integers[i] >> 16) & 0xff);
                result[(i - 1) * 8 + 6] = (byte)((integers[i] >> 8) & 0xff);
                result[(i - 1) * 8 + 7] = (byte)((integers[i]) & 0xff);
            }

            return result;
        }
    }
}
