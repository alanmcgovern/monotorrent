using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht;
using NUnit.Framework;
using System.Net;

namespace MonoTorrent.Dht.Tests
{
    [TestFixture]
    public class RoutingTableTests
    {
        static void Main(string[] args)
        {
            RoutingTableTests t = new RoutingTableTests();
            t.Setup();
            t.AddSame();
            t.Setup();
            t.AddSimilar();
        }
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

        [Test]
        public void AddSame()
        {
            table.Clear();
            for (int i = 0; i < Bucket.MaxCapacity; i++)
            {
                byte[] id = (byte[])this.id.Clone();
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.AreEqual(1, addedCount, "#a");
            Assert.AreEqual(1, table.Buckets.Count, "#1");
            Assert.AreEqual(1, table.Buckets[0].Nodes.Count, "#2");

            CheckBuckets();
        }

        [Test]
        public void AddSimilar()
        {
            for (int i = 0; i < Bucket.MaxCapacity * 3; i++)
            {
                byte[] id = (byte[])this.id.Clone();
                id[0] += (byte)i;
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.AreEqual(Bucket.MaxCapacity * 3 - 1, addedCount, "#1");
            Assert.AreEqual(7, table.Buckets.Count, "#2");
            Assert.AreEqual(8, table.Buckets[0].Nodes.Count, "#3");
            Assert.AreEqual(8, table.Buckets[1].Nodes.Count, "#4");
            Assert.AreEqual(8, table.Buckets[2].Nodes.Count, "#5");
            Assert.AreEqual(0, table.Buckets[3].Nodes.Count, "#6");
            Assert.AreEqual(0, table.Buckets[4].Nodes.Count, "#7");
            Assert.AreEqual(0, table.Buckets[5].Nodes.Count, "#8");
            Assert.AreEqual(0, table.Buckets[6].Nodes.Count, "#9");
            CheckBuckets();
        }

        [Test]
        public void GetClosestTest()
        {
            List<NodeId> nodes = new List<NodeId>();
            byte[] id = new byte[20];
            id[19] = 7;
            table = new RoutingTable(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));

            for (int i = 0; i <= 30; i++)
            {
                if (i == 7)
                    continue;

                id = new byte[20];
                id[19] = (byte)i;
                nodes.Add(new NodeId(id));
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            nodes.Sort(delegate(NodeId left, NodeId right)
            {
                NodeId dLeft = left.Xor(table.LocalNode.Id);
                NodeId dRight = right.Xor(table.LocalNode.Id);
                return dLeft.CompareTo(dRight);
            });

            List<Node> closest = table.GetClosest(table.LocalNode.Id);
            Assert.AreEqual(8, closest.Count, "#1");
            for (int i = 0; i < 8; i++)
                Assert.IsTrue(closest.Exists(delegate(Node node) { return nodes[i].Equals(closest[i].Id); }));
        }
        
        private void CheckBuckets()
        {
            foreach (Bucket b in table.Buckets)
                foreach (Node n in b.Nodes)
                    Assert.IsTrue(n.Id >= b.Min && n.Id < b.Max);
        }
    }
}
