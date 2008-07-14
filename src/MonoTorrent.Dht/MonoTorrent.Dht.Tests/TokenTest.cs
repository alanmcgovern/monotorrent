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
        static void Main(string[] args)
        {
            TokenTest t = new TokenTest();
            t.CheckTokenGenerator();
        }
        [Test]
        public void CheckTokenGenerator()
        {
            TokenManager m = new TokenManager();
            m.Timeout = 1; // 1 second timeout for testing purposes
            Node n = new Node(NodeId.Create(),new IPEndPoint(IPAddress.Parse("127.0.0.1"), 25));
            Node n2 = new Node(NodeId.Create(),new IPEndPoint(IPAddress.Parse("127.0.0.2"), 25));
            BEncodedString s = m.GenerateToken(n);
            BEncodedString s2 = m.GenerateToken(n);

            Assert.AreEqual(s, s2, "#1");

            Assert.IsTrue(m.VerifyToken(n, s),"#2");
            Assert.IsFalse(m.VerifyToken(n2, s),"#3");

            System.Threading.Thread.Sleep(1200);
            Assert.IsTrue(m.VerifyToken(n, s), "#4");

            System.Threading.Thread.Sleep(1200);
            Assert.IsFalse(m.VerifyToken(n, s), "#5");
        }
    }
}
