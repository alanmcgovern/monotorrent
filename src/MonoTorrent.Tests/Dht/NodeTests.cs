#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Net;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Dht
{
    public class NodeTests
    {
        [Fact]
        public void CompactNode()
        {
            var n = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Parse("1.21.121.3"), 511));
            var port = n.CompactNode();
            Assert.True(Toolbox.ByteMatch(n.Id.Bytes, 0, port.TextBytes, 0, 20), "#A");
            Assert.Equal(1, port.TextBytes[20]);
            Assert.Equal(21, port.TextBytes[21]);
            Assert.Equal(121, port.TextBytes[22]);
            Assert.Equal(3, port.TextBytes[23]);
            Assert.Equal(1, port.TextBytes[24]);
            Assert.Equal(255, port.TextBytes[25]);
        }

        //static void Main(string[] args)
        //{
        //    NodeTests t = new NodeTests();
        //    t.CompactNode();
        //}
        [Fact]
        public void CompactPort()
        {
            var n = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Parse("1.21.121.3"), 511));
            var port = n.CompactPort();
            Assert.Equal(1, port.TextBytes[0]);
            Assert.Equal(21, port.TextBytes[1]);
            Assert.Equal(121, port.TextBytes[2]);
            Assert.Equal(3, port.TextBytes[3]);
            Assert.Equal(1, port.TextBytes[4]);
            Assert.Equal(255, port.TextBytes[5]);
        }

        [Fact]
        public void FromCompactNode()
        {
            var buffer = new byte[]
            {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 1, 21, 131, 3, 1, 255};
            var n = Node.FromCompactNode(buffer, 0);
            Assert.True(Toolbox.ByteMatch(buffer, 0, n.Id.Bytes, 0, 20));
            Assert.Equal(IPAddress.Parse("1.21.131.3"), n.EndPoint.Address);
            Assert.Equal(511, n.EndPoint.Port);
        }

        [Fact]
        public void SortByLastSeen()
        {
            var nodes = new List<Node>();
            var start = DateTime.Now;
            for (var i = 0; i < 5; i++)
            {
                var n = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, 0));
                n.LastSeen = start.AddDays(-i);
                nodes.Add(n);
            }

            nodes.Sort();
            Assert.Equal(start.AddDays(-4), nodes[0].LastSeen);
            Assert.Equal(start, nodes[4].LastSeen);
        }
    }
}

#endif