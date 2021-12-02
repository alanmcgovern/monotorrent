using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class NodeIdTests
    {
        private NodeId[] nodes;

        [SetUp]
        public void Setup ()
        {
            nodes = new NodeId[20];
            for (int i = 0; i < nodes.Length; i++) {
                byte[] id = new byte[20];
                for (int j = 0; j < id.Length; j++)
                    id[j] = (byte) (i * 20 + j);
                nodes[i] = new NodeId (id);
            }
        }

        [Test]
        public void GreaterLessThanTest ()
        {
            Assert.IsTrue (nodes[0] < nodes[1], "#1");
            Assert.IsTrue (nodes[1] > nodes[0], "#2");
            Assert.IsTrue (nodes[0] == nodes[0], "#3");
            Assert.AreEqual (nodes[0], nodes[0], "#4");
            Assert.IsTrue (nodes[2] > nodes[1], "#5");
            Assert.IsTrue (nodes[15] < nodes[10], "#6");
        }

        [Test]
        public void XorTest ()
        {
            NodeId zero = new NodeId (new byte[20]);

            byte[] b = new byte[20];
            b[0] = 1;
            NodeId one = new NodeId (b);

            NodeId r = one ^ zero;
            Assert.AreEqual (one, r, "#1");
            Assert.IsTrue (one > zero, "#2");
            Assert.IsTrue (one.CompareTo (zero) > 0, "#3");

            NodeId z = one ^ r;
            Assert.AreEqual (zero, z, "#4");
        }

        [Test]
        public void CompareTest ()
        {
            byte[] i = new byte[20];
            byte[] j = new byte[20];
            i[19] = 1;
            j[19] = 2;
            NodeId one = new NodeId (i);
            NodeId two = new NodeId (j);
            Assert.IsTrue (one.CompareTo (two) < 0);
            Assert.IsTrue (two.CompareTo (one) > 0);
            Assert.IsTrue (one.CompareTo (one) == 0);
        }

        [Test]
        public void CompareTest2 ()
        {
            byte[] data = { 1, 179, 114, 132, 233, 117, 195, 250, 164, 35, 157, 48, 170, 96, 87, 111, 42, 137, 195, 199 };
            var a = new BigEndianBigInteger (data);
            var b = new BigEndianBigInteger (new byte[0]);

            Assert.AreNotEqual (a, b, "#1");
        }

        [Test]
        public void GetBytesTest ()
        {
            var str = new NodeId (new BEncodedString (new byte[20])).AsMemory ();
            Assert.AreEqual (20, str.Span.Length);

            str = new NodeId (new byte[20]).AsMemory ();
            Assert.AreEqual (20, str.Span.Length);

            str = new NodeId (new InfoHash (new byte[20])).AsMemory ();
            Assert.AreEqual (20, str.Span.Length);
        }
    }
}
