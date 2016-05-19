#if !DISABLE_DHT
using System.Collections.Generic;
using System.Net;
using MonoTorrent.Dht;
using Xunit;

namespace MonoTorrent.Tests.Dht
{
    public class RoutingTableTests
    {
        public RoutingTableTests()
        {
            id = new byte[20];
            id[1] = 128;
            n = new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0));
            table = new RoutingTable(n);
            table.NodeAdded += delegate { addedCount++; };
            table.Add(n); //the local node is no more in routing table so add it to show test is still ok
            addedCount = 0;
        }

        //static void Main(string[] args)
        //{
        //    RoutingTableTests t = new RoutingTableTests();
        //    t.Setup();
        //    t.AddSame();
        //    t.Setup();
        //    t.AddSimilar();
        //}
        private readonly byte[] id;
        private RoutingTable table;
        private readonly Node n;
        private int addedCount;

        private void CheckBuckets()
        {
            foreach (var b in table.Buckets)
                foreach (var n in b.Nodes)
                    Assert.True(n.Id >= b.Min && n.Id < b.Max);
        }

        [Fact]
        public void AddSame()
        {
            table.Clear();
            for (var i = 0; i < Bucket.MaxCapacity; i++)
            {
                var id = (byte[]) this.id.Clone();
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.Equal(1, addedCount);
            Assert.Equal(1, table.Buckets.Count);
            Assert.Equal(1, table.Buckets[0].Nodes.Count);

            CheckBuckets();
        }

        [Fact]
        public void AddSimilar()
        {
            for (var i = 0; i < Bucket.MaxCapacity*3; i++)
            {
                var id = (byte[]) this.id.Clone();
                id[0] += (byte) i;
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.Equal(Bucket.MaxCapacity*3 - 1, addedCount);
            Assert.Equal(6, table.Buckets.Count);
            Assert.Equal(8, table.Buckets[0].Nodes.Count);
            Assert.Equal(8, table.Buckets[1].Nodes.Count);
            Assert.Equal(8, table.Buckets[2].Nodes.Count);
            Assert.Equal(0, table.Buckets[3].Nodes.Count);
            Assert.Equal(0, table.Buckets[4].Nodes.Count);
            Assert.Equal(0, table.Buckets[5].Nodes.Count);
            CheckBuckets();
        }

        [Fact]
        public void GetClosestTest()
        {
            List<NodeId> nodes;
            TestHelper.ManyNodes(out table, out nodes);


            var closest = table.GetClosest(table.LocalNode.Id);
            Assert.Equal(8, closest.Count);
            for (var i = 0; i < 8; i++)
                Assert.True(closest.Exists(delegate { return nodes[i].Equals(closest[i].Id); }));
        }
    }
}

#endif