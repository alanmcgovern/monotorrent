//
// System.Security.Cryptography SHA1CryptoServiceProvider Class implementation
//
// Authors:
//	Matthew S. Ford (Matthew.S.Ford@Rose-Hulman.Edu)
//	Sebastien Pouliot (sebastien@ximian.com)
//  Alan McGovern (alan.mcgovern@gmail.com)
//  Scott Peterson
//
// Copyright 2001 by Matthew S. Ford.
// Copyright (C) 2004, 2005 Novell, Inc (http://www.novell.com)
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


using System.Runtime.InteropServices;

namespace System.Security.Cryptography
{
    internal class SHA1Fast : System.Security.Cryptography.SHA1
    {
        private const int BLOCK_SIZE_BYTES = 64;
        private uint[] _H;  // these are my chaining variables
        private ulong count;
        private byte[] _ProcessingBuffer;   // Used to start data when passed less than a block worth.
        private int _ProcessingBufferCount; // Counts how much data we have stored that still needs processed.
        private uint[] buff;

        public SHA1Fast()
        {
            _H = new uint[5];
            _ProcessingBuffer = new byte[BLOCK_SIZE_BYTES];
            buff = new uint[80];

            Initialize();

        }

        protected override unsafe void HashCore(byte[] rgb, int start, int size)
        {
            int i;

            if (_ProcessingBufferCount != 0) {
                i = BLOCK_SIZE_BYTES - _ProcessingBufferCount;
                if (size < i) {
                    System.Buffer.BlockCopy(rgb, start, _ProcessingBuffer, _ProcessingBufferCount, size);
                    _ProcessingBufferCount += size;
                    return;
                }

                else {
                    System.Buffer.BlockCopy(rgb, start, _ProcessingBuffer, _ProcessingBufferCount, i);
                    ProcessBlock(_ProcessingBuffer, 0);
                    _ProcessingBufferCount = 0;
                    start += i;
                    size -= i;
                }
            }

            int leftovers = size % BLOCK_SIZE_BYTES;
            int servingSize = size - leftovers;
            fixed (uint* buff = &this.buff[0]) {
                fixed (byte* input = &rgb[start]) {
                    for (i = 0; i < servingSize; i += BLOCK_SIZE_BYTES) {
                        ProcessBlock(buff, input + i);
                    }
                }
            }

            if (leftovers != 0) {
                System.Buffer.BlockCopy(rgb, start + servingSize, _ProcessingBuffer, 0, leftovers);
                _ProcessingBufferCount = leftovers;
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] hash = new byte[20];
            
            ProcessFinalBlock(_ProcessingBuffer, 0, _ProcessingBufferCount);
            
            for (int i = 0; i < 5; i++) {
                for (int j = 0; j < 4; j++) {
                    hash[i * 4 + j] = (byte)(_H[i] >> (8 * (3 - j)));
                }
            }

            return hash;
        }

        public override void Initialize()
        {
            count = 0;
            _ProcessingBufferCount = 0;

            _H[0] = 0x67452301;
            _H[1] = 0xefcdab89;
            _H[2] = 0x98badcfe;
            _H[3] = 0x10325476;
            _H[4] = 0xC3D2E1F0;
        }

        private unsafe void ProcessBlock(byte[] inputBuffer, int inputOffset)
        {
            fixed (uint* buff = &this.buff[0]) {
                fixed (byte* input = &inputBuffer[inputOffset]) {
                    ProcessBlock (buff, input);
                }
            }
        }

        private unsafe void ProcessBlock(uint* buff, byte* input)
        {
            uint[] _H = this._H;
            uint a, b, c, d, e;
            int i;
            count += BLOCK_SIZE_BYTES;
            
            InitializeBuff(buff, input);
            FillBuff(buff + 16);

            a = _H[0];
            b = _H[1];
            c = _H[2];
            d = _H[3];
            e = _H[4];
            
#region SHA1 Rotations

            // This function was unrolled because it seems to be doubling our performance with current compiler/VM.
            // Possibly roll up if this changes.

            i = 0;
            // ---- Round 1 --------

            while (i < 20)
            {
                e += ((a << 5) | (a >> 27)) + (((c ^ d) & b) ^ d) + 0x5A827999 + buff[i];
                b = (b << 30) | (b >> 2);

                d += ((e << 5) | (e >> 27)) + (((b ^ c) & a) ^ c) + 0x5A827999 + buff[i + 1];
                a = (a << 30) | (a >> 2);

                c += ((d << 5) | (d >> 27)) + (((a ^ b) & e) ^ b) + 0x5A827999 + buff[i + 2];
                e = (e << 30) | (e >> 2);

                b += ((c << 5) | (c >> 27)) + (((e ^ a) & d) ^ a) + 0x5A827999 + buff[i + 3];
                d = (d << 30) | (d >> 2);

                a += ((b << 5) | (b >> 27)) + (((d ^ e) & c) ^ e) + 0x5A827999 + buff[i + 4];
                c = (c << 30) | (c >> 2);
                i += 5;
            }
            

            // ---- Round 2 --------
            while (i < 40)
            {
                e += ((a << 5) | (a >> 27)) + (b ^ c ^ d) + 0x6ED9EBA1 + buff[i];
                b = (b << 30) | (b >> 2);

                d += ((e << 5) | (e >> 27)) + (a ^ b ^ c) + 0x6ED9EBA1 + buff[i + 1];
                a = (a << 30) | (a >> 2);

                c += ((d << 5) | (d >> 27)) + (e ^ a ^ b) + 0x6ED9EBA1 + buff[i + 2];
                e = (e << 30) | (e >> 2);

                b += ((c << 5) | (c >> 27)) + (d ^ e ^ a) + 0x6ED9EBA1 + buff[i + 3];
                d = (d << 30) | (d >> 2);

                a += ((b << 5) | (b >> 27)) + (c ^ d ^ e) + 0x6ED9EBA1 + buff[i + 4];
                c = (c << 30) | (c >> 2);
                i += 5;
            }

            // ---- Round 3 --------
            while (i < 60)
            {

                e += ((a << 5) | (a >> 27)) + ((b & c) | (b & d) | (c & d)) + 0x8F1BBCDC + buff[i];
                b = (b << 30) | (b >> 2);

                d += ((e << 5) | (e >> 27)) + ((a & b) | (a & c) | (b & c)) + 0x8F1BBCDC + buff[i + 1];
                a = (a << 30) | (a >> 2);

                c += ((d << 5) | (d >> 27)) + ((e & a) | (e & b) | (a & b)) + 0x8F1BBCDC + buff[i + 2];
                e = (e << 30) | (e >> 2);

                b += ((c << 5) | (c >> 27)) + ((d & e) | (d & a) | (e & a)) + 0x8F1BBCDC + buff[i + 3];
                d = (d << 30) | (d >> 2);

                a += ((b << 5) | (b >> 27)) + ((c & d) | (c & e) | (d & e)) + 0x8F1BBCDC + buff[i + 4];
                c = (c << 30) | (c >> 2);
                i += 5;
            }

            // ---- Round 4 --------
            while (i < 80)
            {
                e += ((a << 5) | (a >> 27)) + (b ^ c ^ d) + 0xCA62C1D6 + buff[i];
                b = (b << 30) | (b >> 2);

                d += ((e << 5) | (e >> 27)) + (a ^ b ^ c) + 0xCA62C1D6 + buff[i + 1];
                a = (a << 30) | (a >> 2);

                c += ((d << 5) | (d >> 27)) + (e ^ a ^ b) + 0xCA62C1D6 + buff[i + 2];
                e = (e << 30) | (e >> 2);

                b += ((c << 5) | (c >> 27)) + (d ^ e ^ a) + 0xCA62C1D6 + buff[i + 3];
                d = (d << 30) | (d >> 2);

                a += ((b << 5) | (b >> 27)) + (c ^ d ^ e) + 0xCA62C1D6 + buff[i + 4];
                c = (c << 30) | (c >> 2);
                i += 5;
            }
#endregion

            _H[0] += a;
            _H[1] += b;
            _H[2] += c;
            _H[3] += d;
            _H[4] += e;
        }
        
        private unsafe void InitializeBuff(uint* buff2, byte* input)
        {
            byte* buff = (byte*)buff2;

            buff += 0; buff[3] = input[0];  buff[2] = input[1];  buff[1] = input[2];  buff[0] = input[3];
            buff += 4; buff[3] = input[4];  buff[2] = input[5];  buff[1] = input[6];  buff[2] = input[7];
            buff += 4; buff[3] = input[8];  buff[2] = input[9];  buff[1] = input[10]; buff[2] = input[11];
            buff += 4; buff[3] = input[12]; buff[2] = input[13]; buff[1] = input[14]; buff[2] = input[15];
            buff += 4; buff[3] = input[16]; buff[2] = input[17]; buff[1] = input[18]; buff[2] = input[19];
            buff += 4; buff[3] = input[20]; buff[2] = input[21]; buff[1] = input[22]; buff[2] = input[23];
            buff += 4; buff[3] = input[24]; buff[2] = input[25]; buff[1] = input[26]; buff[2] = input[27];
            buff += 4; buff[3] = input[28]; buff[2] = input[29]; buff[1] = input[30]; buff[2] = input[31];
            buff += 4; buff[3] = input[32]; buff[2] = input[33]; buff[1] = input[34]; buff[2] = input[35];
            buff += 4; buff[3] = input[36]; buff[2] = input[37]; buff[1] = input[38]; buff[2] = input[39];
            buff += 4; buff[3] = input[40]; buff[2] = input[41]; buff[1] = input[42]; buff[2] = input[43];
            buff += 4; buff[3] = input[44]; buff[2] = input[45]; buff[1] = input[46]; buff[2] = input[47];
            buff += 4; buff[3] = input[48]; buff[2] = input[49]; buff[1] = input[50]; buff[2] = input[51];
            buff += 4; buff[3] = input[52]; buff[2] = input[53]; buff[1] = input[54]; buff[2] = input[55];
            buff += 4; buff[3] = input[56]; buff[2] = input[57]; buff[1] = input[58]; buff[2] = input[59];
            buff += 4; buff[3] = input[60]; buff[2] = input[61]; buff[1] = input[62]; buff[2] = input[63];
        }

        private unsafe void FillBuff(uint* buff)
        {
            uint val;
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
            val = *(buff - 3) ^ *(buff - 8) ^ *(buff - 14) ^ *(buff - 16);
            *(buff++) = (val << 1) | (val >> 31);
        }

        private void ProcessFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            ulong total = count + (ulong)inputCount;
            int paddingSize = (56 - (int)(total % BLOCK_SIZE_BYTES));

            if (paddingSize < 1)
                paddingSize += BLOCK_SIZE_BYTES;

            int length = inputCount + paddingSize + 8;
            byte[] fooBuffer = (length == 64) ? _ProcessingBuffer : new byte[length];

            for (int i = 0; i < inputCount; i++) {
                fooBuffer[i] = inputBuffer[i + inputOffset];
            }

            fooBuffer[inputCount] = 0x80;
            for (int i = inputCount + 1; i < inputCount + paddingSize; i++) {
                fooBuffer[i] = 0x00;
            }

            // I deal in bytes. The algorithm deals in bits.
            ulong size = total << 3;
            AddLength(size, fooBuffer, inputCount + paddingSize);
            ProcessBlock(fooBuffer, 0);

            if (length == 128)
                ProcessBlock(fooBuffer, 64);
        }

        internal void AddLength(ulong length, byte[] buffer, int position)
        {
            buffer[position++] = (byte)(length >> 56);
            buffer[position++] = (byte)(length >> 48);
            buffer[position++] = (byte)(length >> 40);
            buffer[position++] = (byte)(length >> 32);
            buffer[position++] = (byte)(length >> 24);
            buffer[position++] = (byte)(length >> 16);
            buffer[position++] = (byte)(length >> 8);
            buffer[position] = (byte)(length);
        }
    }
}
