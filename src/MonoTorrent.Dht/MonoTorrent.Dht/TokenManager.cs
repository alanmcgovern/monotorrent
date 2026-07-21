//
// TokenManager.cs
//
// Authors:
//   Olivier Dufour <olivier.duff@gmail.com>
//
// Copyright (C) 2008 Olivier Dufour
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
using System.Buffers;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace MonoTorrent.Dht
{
    class TokenManager
    {
        public int TokenLength => 8;
        Memory<byte> currentSecret;
        Memory<byte> previousSecret;

        public TokenManager ()
        {
            currentSecret = new byte[10];
            previousSecret = new byte[10];
            RandomNumberGenerator.Fill (currentSecret.Span);
            RandomNumberGenerator.Fill (previousSecret.Span);
        }

        public ReadOnlyMemory<byte> GenerateToken (Node node)
        {
            var token = new byte[TokenLength];
            GenerateToken (node, currentSecret.Span, token);
            return token;
        }

        public bool TryGenerateToken (Node node, Span<byte> dest, out int written)
        {
            if (dest.Length < TokenLength) {
                written = 0;
                return false;
            } else {
                GenerateToken (node, currentSecret.Span, dest.Slice (0, TokenLength));
                written = TokenLength;
                return true;
            }
        }

        void GenerateToken (Node node, ReadOnlySpan<byte> secret, Span<byte> token)
        {
            // IPv6 compact details are 18 bytes, the secret is 10 bytes.
            Span<byte> hashBuffer = stackalloc byte[28];

            int written = node.CompactEndPoint (hashBuffer);
            secret.CopyTo (hashBuffer.Slice (written));

            if (!SHA1.TryHashData (
                    hashBuffer.Slice (0, written + secret.Length),
                    hashBuffer,
                    out written) || written != 20)
                throw new InvalidOperationException ("Could not create a 20 byte token");

            hashBuffer.Slice (0, token.Length).CopyTo (token);
        }

        public void RefreshTokens ()
        {
            (currentSecret, previousSecret) = (previousSecret, currentSecret);
            RandomNumberGenerator.Fill (currentSecret.Span);
        }

        public bool VerifyToken (Node node, ReadOnlySpan<byte> token)
        {
            if (token.Length != 8)
                return false;

            Span<byte> expected = stackalloc byte[8];

            GenerateToken (node, currentSecret.Span, expected);
            if (token.SequenceEqual (expected))
                return true;

            GenerateToken (node, previousSecret.Span, expected);
            return token.SequenceEqual (expected);
        }
    }
}
