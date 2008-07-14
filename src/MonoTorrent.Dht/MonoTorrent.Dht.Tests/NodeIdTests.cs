using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace MonoTorrent.Dht.Tests
{
    [TestFixture]
    public class NodeIdTests
    {
        private NodeId[] nodes;

        [SetUp]
        public void Setup()
        {
            nodes = new NodeId[20];
            for (int i = 0; i < nodes.Length; i++)
            {
                byte[] id = new byte[20];
                for (int j = 0; j < id.Length; j++)
                    id[j] = (byte)(i * 20 + j);
                nodes[i] = new NodeId(id);
            }
        }

        [Test]
        public void GreaterLessThanTest()
        {
            Assert.Less(nodes[0], nodes[1], "#1");
            Assert.Greater(nodes[1], nodes[0], "#2");
            Assert.IsTrue(nodes[0] == nodes[0], "#3");
            Assert.AreEqual(nodes[0], nodes[0], "#4");
            Assert.Greater(nodes[2], nodes[1], "#5");
            Assert.Less(nodes[15], nodes[10], "#6");
        }

        [Test]
        public void XorTest()
        {
            NodeId zero = new NodeId(new byte[20]);

            byte[] b = new byte[20]; b[0] = 1;
            NodeId one = new NodeId(b);

            NodeId r = one.Xor(zero);
            Assert.AreEqual(one, r, "#1");
            Assert.IsTrue(one > zero, "#2");
            Assert.Greater(one, zero, "#3");

            NodeId z = one.Xor(r);
            Assert.AreEqual(zero, z, "#4");
        }

        [Test]
        public void CompareTest()
        {
            NodeId one = new NodeId(1);
            NodeId two = new NodeId(2);
            Assert.IsTrue(one.CompareTo(two) < 0);
            Assert.IsTrue(two.CompareTo(one) > 0);
            Assert.IsTrue(one.CompareTo(one) == 0);
        }
    }
}
