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


using System.Security.Cryptography;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    class TokenManager
    {
        readonly byte[] currentSecret;
        readonly byte[] previousSecret;
        readonly RandomNumberGenerator random;
        readonly SHA1 sha1;

        public TokenManager ()
        {
            sha1 = SHA1.Create ();
            random = RandomNumberGenerator.Create ();
            currentSecret = new byte[10];
            previousSecret = new byte[10];
            random.GetNonZeroBytes (currentSecret);
            random.GetNonZeroBytes (previousSecret);
        }

        public BEncodedString GenerateToken (Node node)
        {
            return GenerateToken (node, currentSecret);
        }

        BEncodedString GenerateToken (Node node, byte[] secret)
        {
            byte[] n = node.CompactPort ().Span.ToArray ();
            sha1.Initialize ();
            sha1.TransformBlock (n, 0, n.Length, n, 0);
            sha1.TransformFinalBlock (secret, 0, secret.Length);

            return new BEncodedString (sha1.Hash!);
        }

        public void RefreshTokens ()
        {
            currentSecret.CopyTo (previousSecret, 0);
            random.GetNonZeroBytes (currentSecret);
        }

        public bool VerifyToken (Node node, BEncodedString token)
        {
            return token.Equals (GenerateToken (node, currentSecret)) || token.Equals (GenerateToken (node, previousSecret));
        }
    }
}
