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
using System.IO;
using System.Security.Cryptography;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    public class TokenManager
    {
        private byte[] secret;
        private byte[] previousSecret;
        private DateTime LastSecretGeneration;
        private RandomNumberGenerator random;
        private SHA1 sha1;
        private TimeSpan timeout = TimeSpan.FromMinutes(5);

        internal TimeSpan Timeout
        {
            get { return timeout; }
            set { timeout = value; }
        }

        public TokenManager()
        {
            sha1 = SHA1.Create();
            random = RandomNumberGenerator.Create();
            LastSecretGeneration = DateTime.MinValue; //in order to force the update
            secret = new byte[10];
            previousSecret = new byte[10];
            random.GetNonZeroBytes(secret);
            random.GetNonZeroBytes(previousSecret);
        }
        public BEncodedString GenerateToken(Node node)
        {
            return GetToken(node, secret);
        }

        public bool VerifyToken(Node node, BEncodedString token)
        {
            return (token.Equals(GetToken(node, secret)) || token.Equals(GetToken(node, previousSecret)));
        }
        
        private BEncodedString GetToken(Node node, byte[] s)
        {
            //refresh secret needed
            if (LastSecretGeneration.Add(timeout) < DateTime.UtcNow)
            {
                LastSecretGeneration = DateTime.UtcNow;
                secret.CopyTo(previousSecret, 0);
                random.GetNonZeroBytes(secret);
            }

            byte[] n = node.CompactPort().TextBytes;
            sha1.Initialize();
            sha1.TransformBlock(n, 0, n.Length, n, 0);
            sha1.TransformFinalBlock(s, 0, s.Length);

            return (BEncodedString)sha1.Hash;
        }
    }
}
