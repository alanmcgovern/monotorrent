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
        private Random random;
        private SHA1CryptoServiceProvider sha1;
        
        public TokenManager()
        {
            sha1 = new SHA1CryptoServiceProvider();
            random = new Random();
            LastSecretGeneration = DateTime.MinValue; //in order to force the update
            secret = new byte[10];
            previousSecret = new byte[10];
            random.NextBytes(secret);
            random.NextBytes(previousSecret);
        }
        public BEncodedString GenerateToken(Node node)
        {
            //refresh secret needed
            if (LastSecretGeneration.AddMinutes(5) < DateTime.Now)
            {
                secret.CopyTo(previousSecret,0);
                random.NextBytes(secret);
            }
            return GetToken(node, secret);
        }

        public bool VerifyToken(Node node, BEncodedString token)
        {
            return (token == GetToken(node, secret) || token == GetToken(node, previousSecret));// thanks to Equals override
        }
        
        private BEncodedString GetToken(Node node, byte[] s)
        {
            byte[] addr = node.EndPoint.Address.GetAddressBytes();
            byte[] result = new byte[addr.Length + s.Length];
            Array.Copy(addr, result, addr.Length);
            Array.Copy(s, result, s.Length);
            result = sha1.ComputeHash(result);
            return new BEncodedString(result);
        }
        /*public void PrintBuff(string str, byte[] bb)
        {
            Console.Write(str + "[");
            foreach (byte b in bb)
                Console.Write(b+", ");
            Console.WriteLine("]");
        }*/
    }
}
