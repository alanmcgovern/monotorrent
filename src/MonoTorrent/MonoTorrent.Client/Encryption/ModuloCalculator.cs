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
using System.Text;
using Mono.Math;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    /// Class to facilitate the calculation of a^b mod p, where a and b are large integers
    /// and p is the predefined prime from message stream encryption.
    /// </summary>
    internal class ModuloCalculator
    {
        
        private static BigInteger prime = new BigInteger(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xC9, 0xF, 0xDA, 0xA2, 0x21, 0x68, 0xC2, 0x34, 0xC4, 0xC6, 0x62, 0x8B, 0x80, 0xDC, 0x1C, 0xD1, 0x29, 0x2, 0x4E, 0x8, 0x8A, 0x67, 0xCC, 0x74, 0x2, 0xB, 0xBE, 0xA6, 0x3B, 0x13, 0x9B, 0x22, 0x51, 0x4A, 0x8, 0x79, 0x8E, 0x34, 0x4, 0xDD, 0xEF, 0x95, 0x19, 0xB3, 0xCD, 0x3A, 0x43, 0x1B, 0x30, 0x2B, 0xA, 0x6D, 0xF2, 0x5F, 0x14, 0x37, 0x4F, 0xE1, 0x35, 0x6D, 0x6D, 0x51, 0xC2, 0x45, 0xE4, 0x85, 0xB5, 0x76, 0x62, 0x5E, 0x7E, 0xC6, 0xF4, 0x4C, 0x42, 0xE9, 0xA6, 0x3A, 0x36, 0x21, 0x0, 0x0, 0x0, 0x0, 0x0, 0x9, 0x5, 0x63 });

        public static BigInteger TWO = new BigInteger(2);


        public static byte[] Calculate(byte[] a, byte[] b)
        {
            return Calculate(new BigInteger(a), new BigInteger(b)); //UInt64ToBytes(Calculate(BytesToUInt64(a), BytesToUInt64(b)));
        }

        public static byte[] Calculate(BigInteger a, byte[] b)
        {
            return Calculate(a, new BigInteger(b)); //UInt64ToBytes(Calculate(a, BytesToUInt64(b)));
        }

        public static byte[] Calculate(byte[] a, BigInteger b)
        {
            return Calculate(new BigInteger(a), b); //UInt64ToBytes(Calculate(BytesToUInt64(a), b));
        }

        private static object locker = new object();
        public static byte[] Calculate(BigInteger a, BigInteger b)
        {
            byte[] bytes;
            lock (locker)
                bytes = a.ModPow(b, prime).GetBytes();

            if (bytes.Length < 96)
            {
                byte[] oldBytes = bytes;
                bytes = new byte[96];
                Array.Copy(oldBytes, 0, bytes, 96 - oldBytes.Length, oldBytes.Length);
                for (int i = 0; i < (96 - oldBytes.Length); i++)
                    bytes[i] = 0;
            }

            return bytes;
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
    }
}
