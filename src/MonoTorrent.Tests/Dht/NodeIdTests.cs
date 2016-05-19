#if !DISABLE_DHT
using Xunit;

namespace MonoTorrent.Dht
{
    public class NodeIdTests
    {
        private NodeId[] nodes;

        public NodeIdTests()
        {
            nodes = new NodeId[20];
            for (var i = 0; i < nodes.Length; i++)
            {
                var id = new byte[20];
                for (var j = 0; j < id.Length; j++)
                    id[j] = (byte) (i*20 + j);
                nodes[i] = new NodeId(id);
            }
        }

        [Fact]
        public void GreaterLessThanTest()
        {
            Assert.True(nodes[0] < nodes[1]);
            Assert.True(nodes[1] > nodes[0]);
            Assert.True(nodes[0] == nodes[0]);
            Assert.Equal(nodes[0], nodes[0]);
            Assert.True(nodes[2] > nodes[1]);
            Assert.True(nodes[15] < nodes[10]);
        }

        [Fact]
        public void XorTest()
        {
            var zero = new NodeId(new byte[20]);

            var b = new byte[20];
            b[0] = 1;
            var one = new NodeId(b);

            var r = one.Xor(zero);
            Assert.Equal(one, r);
            Assert.True(one > zero);
            Assert.True(one.CompareTo(zero) > 0);

            var z = one.Xor(r);
            Assert.Equal(zero, z);
        }

        [Fact]
        public void CompareTest()
        {
            var i = new byte[20];
            var j = new byte[20];
            i[19] = 1;
            j[19] = 2;
            var one = new NodeId(i);
            var two = new NodeId(j);
            Assert.True(one.CompareTo(two) < 0);
            Assert.True(two.CompareTo(one) > 0);
            Assert.True(one.CompareTo(one) == 0);
        }

        [Fact]
        public void CompareTest2()
        {
            var data = new byte[]
            {1, 179, 114, 132, 233, 117, 195, 250, 164, 35, 157, 48, 170, 96, 87, 111, 42, 137, 195, 199};
            var a = new BigInteger(data);
            var b = new BigInteger(new byte[0]);

            Assert.NotEqual(a, b);
        }
    }
}

#endif