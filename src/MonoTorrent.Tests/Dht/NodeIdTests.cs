#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MonoTorrent.Dht
{
    
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

        [Fact]
        public void GreaterLessThanTest()
        {
            Assert.True(nodes[0] < nodes[1], "#1");
            Assert.True(nodes[1] > nodes[0], "#2");
            Assert.True(nodes[0] == nodes[0], "#3");
            Assert.Equal(nodes[0], nodes[0], "#4");
            Assert.True(nodes[2] > nodes[1], "#5");
            Assert.True(nodes[15] < nodes[10], "#6");
        }

        [Fact]
        public void XorTest()
        {
            NodeId zero = new NodeId(new byte[20]);

            byte[] b = new byte[20]; b[0] = 1;
            NodeId one = new NodeId(b);

            NodeId r = one.Xor(zero);
            Assert.Equal(one, r, "#1");
            Assert.True(one > zero, "#2");
            Assert.True(one.CompareTo(zero) > 0, "#3");

            NodeId z = one.Xor(r);
            Assert.Equal(zero, z, "#4");
        }

        [Fact]
        public void CompareTest()
        {
            byte[] i = new byte[20];
            byte[] j = new byte[20];
            i[19] = 1;
            j[19] = 2;
            NodeId one = new NodeId(i);
            NodeId two = new NodeId(j);
            Assert.True(one.CompareTo(two) < 0);
            Assert.True(two.CompareTo(one) > 0);
            Assert.True(one.CompareTo(one) == 0);
        }

        [Fact]
        public void CompareTest2()
        {
            byte[] data = new byte[] { 1, 179, 114, 132, 233, 117, 195, 250, 164, 35, 157, 48, 170, 96, 87, 111, 42, 137, 195, 199 };
            BigInteger a = new BigInteger(data);
            BigInteger b = new BigInteger(new byte[0]);

            Assert.NotEqual(a, b, "#1");
        }
    }
}
#endif