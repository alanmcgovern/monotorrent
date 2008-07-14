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
            NodeId[] nodes = new NodeId[20];
            for (int i = 0; i < nodes.Length; i++)
            {
                byte[] id = new byte[20];
                for (int j = 0; j < id.Length; j++)
                    id[j] = (byte)(i * 20 + j);
                nodes[i] = new NodeId(id);
            }
        }

        [Test]
        public void GreaterThanTest()
        {
            Assert.Less(nodes[0], nodes[1]);
            Assert.Greater(nodes[1], nodes[2]);
            Assert.Less(nodes[15], nodes[10]);
        }
    }
}
