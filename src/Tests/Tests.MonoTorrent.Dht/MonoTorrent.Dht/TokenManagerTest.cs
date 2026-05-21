// TokenManagerTest.cs
//
// Authors:
//   Olivier Dufour <olivier.duff@gmail.com>
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2008 Olivier Dufour
// Copyright (C) 2019 Alan McGovern
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
using System.Security.Cryptography;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class TokenManagerTest
    {
        TokenManager manager;
        Node node;
        ReadOnlyMemory<byte> token;

        [SetUp]
        public void Setup ()
        {
            manager = new TokenManager ();
            node = new Node (NodeId.Create (), new CompactEndPoint (IPAddress.Parse ("127.0.0.1"), 25));
            token = manager.GenerateToken (node);
        }

        [Test]
        public void CanGenerateToken ([Values("::1", "127.0.0.1")] string ipAddress)
        {
            var node = new Node (NodeId.Create (), new CompactEndPoint (IPAddress.Parse (ipAddress), 25));
            var token = manager.GenerateToken (node);
            Assert.IsTrue (manager.VerifyToken (node, token.Span));
        }

        [Test]
        public void InvalidateOldTokens ()
        {
            Assert.IsTrue (manager.VerifyToken (node, token.Span), "#1");

            manager.RefreshTokens ();
            Assert.IsTrue (manager.VerifyToken (node, token.Span), "#2");

            manager.RefreshTokens ();
            Assert.IsFalse (manager.VerifyToken (node, token.Span), "#3");
        }

        [Test]
        public void InvalidTokenForNode ()
        {
            var otherNode = new Node (node.Id, new CompactEndPoint (IPAddress.Parse ("127.0.0.2"), 25));
            Assert.IsFalse (manager.VerifyToken (otherNode, token.Span), "#1");

            otherNode = new Node (node.Id, new CompactEndPoint (IPAddress.Parse ("127.0.0.1"), 26));
            Assert.IsFalse (manager.VerifyToken (otherNode, token.Span), "#2");
        }

        [Test]
        public void TokenChangesAfterRefresh ()
        {
            Assert.IsTrue (token.Span.SequenceEqual (manager.GenerateToken (node).Span), "#1");

            manager.RefreshTokens ();
            Assert.AreNotEqual (token, manager.GenerateToken (node), "#2");
        }
    }
}
