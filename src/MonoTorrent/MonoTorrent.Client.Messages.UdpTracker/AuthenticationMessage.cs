//
// AuthenticationMessage.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class AuthenticationMessage : Message
    {
        byte usernameLength;
        string username;
        byte[] password;

        public override int ByteLength => 4 + usernameLength + 8;

        public override void Decode (byte[] buffer, int offset, int length)
        {
            usernameLength = buffer[offset];
            offset++;
            username = Encoding.ASCII.GetString (buffer, offset, usernameLength);
            offset += usernameLength;
            password = new byte[8];
            Buffer.BlockCopy (buffer, offset, password, 0, password.Length);
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = Write (buffer, offset, usernameLength);
            byte[] name = Encoding.ASCII.GetBytes (username);
            written += Write (buffer, offset, name, 0, name.Length);
            written += Write (buffer, offset, password, 0, password.Length);

            CheckWritten (written);
            return written;
        }
    }
}
