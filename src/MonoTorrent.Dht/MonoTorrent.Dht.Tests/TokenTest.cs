// TokenTest.cs.cs
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
//

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Net;

using MonoTorrent.Dht;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.MessageTests
{
    [TestFixture]
    public class TokenTest
    {
        [Test]
        public void CheckTokenGenerator()
        {
            TokenManager m = new TokenManager();
            Node n = new Node(NodeId.Create(),new IPEndPoint(new IPAddress(new Byte[] {0x7F,0x00,0x00,0x01}), 25));
            Node n2 = new Node(NodeId.Create(),new IPEndPoint(new IPAddress(new Byte[] {0x7F,0x00,0x00,0x02}), 25));
            BEncodedString s = m.GenerateToken(n);
            Assert.IsTrue(m.VerifyToken(n, s),"#1");//ToolBoxByteMatch seems to fail...
            Assert.IsFalse(m.VerifyToken(n2, s),"#2");            
        }
    }
}
