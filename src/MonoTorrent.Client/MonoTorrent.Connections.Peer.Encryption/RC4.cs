//
// RC4.cs
//
// Authors:
//   Yiduo Wang planetbeing@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
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
using System.Security.Cryptography;

namespace MonoTorrent.Connections.Peer.Encryption
{
    /// <summary>
    /// RC4 encryption
    /// </summary>
    class RC4 : IEncryption
    {
        static readonly RandomNumberGenerator random = new RNGCryptoServiceProvider ();

        readonly byte[] S;
        int x;
        int y;

        public RC4 (byte[] key)
        {
            S = new byte[256];
            for (int i = 0; i < S.Length; i++)
                S[i] = (byte) i;

            byte c;

            for (int i = 0; i <= 255; i++) {
                x = (x + S[i] + key[i % key.Length]) % 256;
                c = S[x];
                S[x] = S[i];
                S[i] = c;
            }

            x = 0;

            byte[] wasteBuffer = new byte[1024];
            random.GetBytes (wasteBuffer);
            Encrypt (wasteBuffer);
        }

        public void Decrypt (Span<byte> buffer)
            => Encrypt (buffer);

        public void Encrypt (Span<byte> buffer)
        {
            byte c;
            for (int i = 0; i < buffer.Length; i++) {
                x = (x + 1) & 0xFF;
                y = (y + S[x]) & 0xFF;

                c = S[y];
                S[y] = S[x];
                S[x] = c;

                buffer[i] = (byte) (buffer[i] ^ (S[(S[x] + S[y]) & 0xFF]));
            }
        }
    }
}
