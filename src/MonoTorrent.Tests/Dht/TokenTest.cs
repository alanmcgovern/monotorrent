#if !DISABLE_DHT
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
using System.Net;
using System.Threading;
using MonoTorrent.Dht;
using Xunit;

namespace MonoTorrent.Tests.Dht
{
    public class TokenTest
    {
        //static void Main(string[] args)
        //{
        //    TokenTest t = new TokenTest();
        //    t.CheckTokenGenerator();
        //}
        [Fact]
        public void CheckTokenGenerator()
        {
            var m = new TokenManager();
            m.Timeout = TimeSpan.FromMilliseconds(75); // 1 second timeout for testing purposes
            var n = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 25));
            var n2 = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Parse("127.0.0.2"), 25));
            var s = m.GenerateToken(n);
            var s2 = m.GenerateToken(n);

            Assert.Equal(s, s2);

            Assert.True(m.VerifyToken(n, s), "#2");
            Assert.False(m.VerifyToken(n2, s), "#3");

            Thread.Sleep(100);
            Assert.True(m.VerifyToken(n, s));

            Thread.Sleep(100);
            Assert.False(m.VerifyToken(n, s));
        }
    }
}

#endif