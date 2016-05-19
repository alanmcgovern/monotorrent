//
// NullEncryption.cs
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
using System.Text;

namespace MonoTorrent.Client.Encryption
{
    /// <summary>
    /// Plaintext "encryption"
    /// </summary>
    public class PlainTextEncryption : IEncryption
    {
        public void Decrypt(byte[] buffer)
        {
            // Nothing
        }

        public void Decrypt(byte[] buffer, int offset, int count)
        {
            // Nothing
        }

        public void Decrypt(byte[] src, int srcOffset, byte[] dest, int destOffset, int count)
        {
            Encrypt(src, srcOffset, dest, destOffset, count);
        }

        public void Encrypt(byte[] buffer)
        {
            // Nothing
        }

        public void Encrypt(byte[] buffer, int offset, int count)
        {
            // Nothing
        }

        public void Encrypt(byte[] src, int srcOffset, byte[] dest, int destOffset, int count)
        {
            Buffer.BlockCopy(src, srcOffset, dest, destOffset, count);
        }
    }
}
