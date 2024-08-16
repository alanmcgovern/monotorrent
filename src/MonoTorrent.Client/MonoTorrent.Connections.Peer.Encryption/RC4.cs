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
        static readonly Memory<byte> Discarder = new byte[1024];

        public EncryptionType EncryptionType => EncryptionType.RC4Full;

        readonly Memory<byte> Memory = new byte[256];
        int x;
        int y;

        internal RC4 (ReadOnlySpan<byte> key)
        {
            var span = Memory.Span;

            for (int i = 0; i < span.Length; i++)
                span[i] = (byte) i;

            for (int i = 0; i < span.Length; i++) {
                x = (x + span[i] + key[i % key.Length]) & 0xFF;

                byte c = span[x];
                span[x] = span[i];
                span[i] = c;
            }

            x = 0;

            // We need to discard the first 1024 bytes. The contents of the 'discarder' buffer
            // are irrelevant, we just to ensure we consume the first 1024 bytes of the encoder.
            Encrypt (Discarder.Span);
        }

        public void Decrypt (Span<byte> buffer)
            => Encrypt (buffer);

        public void Encrypt (Span<byte> buffer)
        {
            var span = Memory.Span;
            foreach (ref byte bufi in buffer) {
                x = (x + 1) & 0xFF;
                ref byte refSx = ref span[x];

                y = (y + refSx) & 0xFF;
                ref byte refSy = ref span[y];

                byte sy = refSy;
                byte sx = refSx;
                refSy = sx;
                refSx = sy;

                bufi ^= span[(sy + sx) & 0xFF];
            }
        }
    }
}
