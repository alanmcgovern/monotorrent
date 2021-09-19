// BucketTest.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
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

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class BucketTest
    {
        [Test]
        public void CompareTo ()
        {
            var b1 = new Bucket (NodeId.Minimum, NodeId.Maximum);
            var b2 = new Bucket (NodeId.Minimum, NodeId.Maximum);
            Assert.AreEqual (0, b1.CompareTo (b1), "#1");
            Assert.AreEqual (0, b1.CompareTo (b2), "#2");
        }

        [Test]
        public void CompareTo_Null ()
        {
            var b1 = new Bucket (NodeId.Minimum, NodeId.Maximum);
            Assert.IsTrue (b1.CompareTo (null) > 0, "#1");
        }

        [Test]
        public void Equality ()
        {
            var b1 = new Bucket (NodeId.Minimum, NodeId.Maximum);
            var b2 = new Bucket (NodeId.Minimum, NodeId.Maximum);
            Assert.IsTrue (b1.Equals (b1), "#1");
            Assert.IsTrue (b1.Equals (b2), "#2");
        }

        [Test]
        public void Equality_NotEqual ()
        {
            var b1 = new Bucket (NodeId.Minimum, NodeId.Minimum);
            var b2 = new Bucket (NodeId.Minimum, NodeId.Maximum);
            Assert.IsFalse (b1.Equals (b2), "#1");
        }

        [Test]
        public void Equality_Null ()
        {
            var b1 = new Bucket (NodeId.Minimum, NodeId.Maximum);
            Assert.IsFalse (b1.Equals (null), "#1");
        }

        [Test]
        public void SortBySeen ()
        {
            var oldNode = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Any, 0));
            var newNode = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Any, 1));

            var bucket = new Bucket {
                oldNode,
                newNode
            };

            oldNode.Seen (TimeSpan.FromDays (1));
            bucket.SortBySeen ();
            Assert.AreEqual (oldNode, bucket.Nodes[0], "#1");

            newNode.Seen (TimeSpan.FromDays (2));
            bucket.SortBySeen ();
            Assert.AreEqual (newNode, bucket.Nodes[0], "#2");
        }
    }
}
