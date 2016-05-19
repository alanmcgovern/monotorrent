#if !DISABLE_DHT
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
using System.Security.Cryptography;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent.Dht
{
    internal class TokenManager
    {
        private readonly byte[] previousSecret;
        private readonly RandomNumberGenerator random;
        private readonly byte[] secret;
        private readonly SHA1 sha1;
        private DateTime LastSecretGeneration;

        public TokenManager()
        {
            sha1 = HashAlgoFactory.Create<SHA1>();
            random = new RNGCryptoServiceProvider();
            LastSecretGeneration = DateTime.MinValue; //in order to force the update
            secret = new byte[10];
            previousSecret = new byte[10];
            random.GetNonZeroBytes(secret);
            random.GetNonZeroBytes(previousSecret);
        }

        internal TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

        public BEncodedString GenerateToken(Node node)
        {
            return GetToken(node, secret);
        }

        public bool VerifyToken(Node node, BEncodedString token)
        {
            return token.Equals(GetToken(node, secret)) || token.Equals(GetToken(node, previousSecret));
        }

        private BEncodedString GetToken(Node node, byte[] s)
        {
            //refresh secret needed
            if (LastSecretGeneration.Add(Timeout) < DateTime.UtcNow)
            {
                LastSecretGeneration = DateTime.UtcNow;
                secret.CopyTo(previousSecret, 0);
                random.GetNonZeroBytes(secret);
            }

            var n = node.CompactPort().TextBytes;
            sha1.Initialize();
            sha1.TransformBlock(n, 0, n.Length, n, 0);
            sha1.TransformFinalBlock(s, 0, s.Length);

            return sha1.Hash;
        }
    }
}

#endif