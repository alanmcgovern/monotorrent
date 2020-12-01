using System.Collections.Generic;
using System.Net;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class ClosestNodesCollectionTests
    {
        [Test]
        public void AddSameElementTwice ()
        {
            var node = new Node (NodeId.Minimum, new IPEndPoint (IPAddress.Any, 1));
            var nodes = new ClosestNodesCollection (NodeId.Minimum);

            Assert.IsTrue (nodes.Add (node), "#1");
            Assert.AreEqual (1, nodes.Count, "#2");

            Assert.IsFalse (nodes.Add (node), "#3");
            Assert.AreEqual (1, nodes.Count, "#4");
        }

        [Test]
        public void CloserNodes ()
        {
            var value = new BigEndianBigInteger (1);

            var closeNodes = new List<Node> ();
            var farNodes = new List<Node> ();

            for (int i = 0; i < Bucket.MaxCapacity; i++) {
                closeNodes.Add (new Node (new NodeId (value << i), new IPEndPoint (IPAddress.Any, i)));
                farNodes.Add (new Node (new NodeId (value << (i + Bucket.MaxCapacity)), new IPEndPoint (IPAddress.Any, i + Bucket.MaxCapacity)));
            }

            var nodes = new ClosestNodesCollection (NodeId.Minimum);
            foreach (var node in farNodes)
                Assert.IsTrue (nodes.Add (node), "#1");
            Assert.AreEqual (Bucket.MaxCapacity, nodes.Capacity, "#2");
            CollectionAssert.AreEquivalent (farNodes, nodes, "#3");

            foreach (var node in closeNodes)
                Assert.IsTrue (nodes.Add (node), "#4");
            Assert.AreEqual (Bucket.MaxCapacity, nodes.Capacity, "#5");
            CollectionAssert.AreEquivalent (closeNodes, nodes, "#6");

            foreach (var node in farNodes)
                Assert.IsFalse (nodes.Add (node), "#7");
        }

        [Test]
        public void ContainsTest ()
        {
            var node = new Node (NodeId.Minimum, new IPEndPoint (IPAddress.Any, 1));
            var nodes = new ClosestNodesCollection (NodeId.Minimum);

            nodes.Add (node);
            Assert.IsTrue (nodes.Contains (node), "#2");
        }

        [Test]
        public void RemoveOnlyElement ()
        {
            var node = new Node (NodeId.Minimum, new IPEndPoint (IPAddress.Any, 1));
            var otherNode = new Node (NodeId.Maximum, new IPEndPoint (IPAddress.Any, 2));

            var nodes = new ClosestNodesCollection (NodeId.Minimum);
            Assert.IsTrue (nodes.Add (node), "#1");
            Assert.AreEqual (1, nodes.Count, "#2");

            Assert.IsFalse (nodes.Remove (otherNode), "#3");
            Assert.AreEqual (1, nodes.Count, "#4");

            Assert.IsTrue (nodes.Remove (node), "#5");
            Assert.AreEqual (0, nodes.Count, "#6");

            Assert.IsFalse (nodes.Remove (node), "#7");
            Assert.AreEqual (0, nodes.Count, "#8");
        }
    }
}
