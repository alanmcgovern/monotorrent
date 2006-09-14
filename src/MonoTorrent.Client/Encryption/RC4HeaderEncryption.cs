//
// RC4HeaderEncryption.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Net;
using System.Security.Cryptography;

namespace MonoTorrent.Client.Encryption
{
    internal class RC4HeaderEncryption : IEncryptor
    {
        private ICryptoTransform encryptor;
        private ICryptoTransform decryptor;
        private Mono.Security.Cryptography.RC4 rc4;
     
        public RC4HeaderEncryption()
        {
            this.rc4 = Mono.Security.Cryptography.RC4.Create();
            this.encryptor = rc4.CreateEncryptor();
            this.decryptor = rc4.CreateDecryptor();
        }


        public void Encrypt(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }


        public void Decrypt(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}