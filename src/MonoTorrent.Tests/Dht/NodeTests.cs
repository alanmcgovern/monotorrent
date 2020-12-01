//
// NodeTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2010 Alan McGovern
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


using System.Net;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class NodeTests
    {
        [Test]
        public void CompactPort ()
        {
            Node n = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Parse ("1.21.121.3"), 511));
            BEncodedString port = n.CompactPort ();
            Assert.AreEqual (1, port.TextBytes[0], "#1");
            Assert.AreEqual (21, port.TextBytes[1], "#1");
            Assert.AreEqual (121, port.TextBytes[2], "#1");
            Assert.AreEqual (3, port.TextBytes[3], "#1");
            Assert.AreEqual (1, port.TextBytes[4], "#1");
            Assert.AreEqual (255, port.TextBytes[5], "#1");
        }

        [Test]
        public void FromCompactNode ()
        {
            byte[] buffer = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 1, 21, 131, 3, 1, 255 };
            Node n = Node.FromCompactNode (buffer, 0);
            Assert.IsTrue (Toolbox.ByteMatch (buffer, 0, n.Id.Bytes, 0, 20), "#1");
            Assert.AreEqual (IPAddress.Parse ("1.21.131.3"), n.EndPoint.Address, "#2");
            Assert.AreEqual (511, n.EndPoint.Port, "#3");
        }

        [Test]
        public void CompactNode ()
        {
            Node n = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Parse ("1.21.121.3"), 511));
            BEncodedString port = n.CompactNode ();
            Assert.IsTrue (Toolbox.ByteMatch (n.Id.Bytes, 0, port.TextBytes, 0, 20), "#A");
            Assert.AreEqual (1, port.TextBytes[20], "#1");
            Assert.AreEqual (21, port.TextBytes[21], "#1");
            Assert.AreEqual (121, port.TextBytes[22], "#1");
            Assert.AreEqual (3, port.TextBytes[23], "#1");
            Assert.AreEqual (1, port.TextBytes[24], "#1");
            Assert.AreEqual (255, port.TextBytes[25], "#1");
        }
    }
}
