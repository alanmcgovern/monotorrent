using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht;
using NUnit.Framework;
using System.Net;

namespace monotorrent_dht_tests
{
    [TestFixture]
    public class RoutingTableTests
    {
        byte[] id;
        RoutingTable table;

        [SetUp]
        public void Setup()
        {
            id = new byte[20];
            id[1] = 128;
            table = new RoutingTable(new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0)));
        }

        [Test]
        public void AddSame()
        {
            for (int i = 0; i < Bucket.MaxCapacity; i++)
            {
                byte[] id = (byte[])this.id.Clone();
                //id[id.Length - 1] += (byte)i;
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.AreEqual(160, table.Buckets.Count, "#1");
            Assert.AreEqual(0, table.Buckets[0].Nodes.Count, "#2");
            for (int i = 0; i < table.Buckets.Count; i++)
            {
                if (i == 1)
                {
                    Assert.AreEqual(6, table.Buckets[1].Nodes.Count, "#3.a"+i);
                    Assert.IsNotNull(table.Buckets[i].Replacement, "#3.b" + i);
                }
                else
                {
                    Assert.AreEqual(0, table.Buckets[i].Nodes.Count, "#3.a" + i);
                    Assert.AreEqual(null, table.Buckets[i].Replacement, "#3.b" + i);
                }
            }

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

            Assert.AreEqual(8, table.Buckets.Count, "#1");
            Assert.AreEqual(5, table.Buckets[0].Nodes.Count);
            Assert.AreEqual(4, table.Buckets[1].Nodes.Count);
            Assert.AreEqual(6, table.Buckets[2].Nodes.Count);
            Assert.AreEqual(6, table.Buckets[3].Nodes.Count);
            Assert.AreEqual(0, table.Buckets[4].Nodes.Count);
            Assert.AreEqual(0, table.Buckets[5].Nodes.Count);
            Assert.AreEqual(0, table.Buckets[6].Nodes.Count);
            Assert.AreEqual(0, table.Buckets[7].Nodes.Count);
            CheckBuckets();
        }

        private void CheckBuckets()
        {
            foreach (Bucket b in table.Buckets)
                foreach (Node n in b.Nodes)
                    Assert.IsTrue(n.Id >= b.Min && n.Id < b.Max);
        }
    }
}
