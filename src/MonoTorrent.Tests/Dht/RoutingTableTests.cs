#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht;
using Xunit;
using System.Net;

namespace MonoTorrent.Dht
{
    
    public class RoutingTableTests
    {
        //static void Main(string[] args)
        //{
        //    RoutingTableTests t = new RoutingTableTests();
        //    t.Setup();
        //    t.AddSame();
        //    t.Setup();
        //    t.AddSimilar();
        //}
        byte[] id;
        RoutingTable table;
        Node n;
        int addedCount;
        
        [SetUp]
        public void Setup()
        {
            id = new byte[20];
            id[1] = 128;
            n = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table = new RoutingTable(n);
            table.NodeAdded += delegate { addedCount++; };
            table.Add(n);//the local node is no more in routing table so add it to show test is still ok
            addedCount = 0;
        }

        [Fact]
        public void AddSame()
        {
            table.Clear();
            for (int i = 0; i < Bucket.MaxCapacity; i++)
            {
                byte[] id = (byte[])this.id.Clone();
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.Equal(1, addedCount, "#a");
            Assert.Equal(1, table.Buckets.Count, "#1");
            Assert.Equal(1, table.Buckets[0].Nodes.Count, "#2");

            CheckBuckets();
        }

        [Fact]
        public void AddSimilar()
        {
            for (int i = 0; i < Bucket.MaxCapacity * 3; i++)
            {
                byte[] id = (byte[])this.id.Clone();
                id[0] += (byte)i;
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.Equal(Bucket.MaxCapacity * 3 - 1, addedCount, "#1");
            Assert.Equal(6, table.Buckets.Count, "#2");
            Assert.Equal(8, table.Buckets[0].Nodes.Count, "#3");
            Assert.Equal(8, table.Buckets[1].Nodes.Count, "#4");
            Assert.Equal(8, table.Buckets[2].Nodes.Count, "#5");
            Assert.Equal(0, table.Buckets[3].Nodes.Count, "#6");
            Assert.Equal(0, table.Buckets[4].Nodes.Count, "#7");
            Assert.Equal(0, table.Buckets[5].Nodes.Count, "#8");
            CheckBuckets();
        }

        [Fact]
        public void GetClosestTest()
        {
            List<NodeId> nodes;
            TestHelper.ManyNodes(out table, out nodes);
            

            List<Node> closest = table.GetClosest(table.LocalNode.Id);
            Assert.Equal(8, closest.Count, "#1");
            for (int i = 0; i < 8; i++)
                Assert.True(closest.Exists(delegate(Node node) { return nodes[i].Equals(closest[i].Id); }));
        }
        
        private void CheckBuckets()
        {
            foreach (Bucket b in table.Buckets)
                foreach (Node n in b.Nodes)
                    Assert.True(n.Id >= b.Min && n.Id < b.Max);
        }
    }
}
#endif