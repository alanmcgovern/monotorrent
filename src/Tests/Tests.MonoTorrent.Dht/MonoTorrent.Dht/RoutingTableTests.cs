using System.Collections.Generic;
using System.Linq;
using System.Net;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class RoutingTableTests
    {
        byte[] id;
        RoutingTable table;

        [SetUp]
        public void Setup ()
        {
            id = new byte[20];
            id[1] = 128;
            table = new RoutingTable (new NodeId (id));
        }

        [Test]
        public void AddSame ()
        {
            table.Clear ();
            for (int i = 0; i < Bucket.MaxCapacity; i++) {
                byte[] id = (byte[]) this.id.Clone ();
                table.Add (new Node (new NodeId (id), new IPEndPoint (IPAddress.Any, 0)));
            }

            Assert.AreEqual (1, table.CountNodes (), "#a");
            Assert.AreEqual (1, table.Buckets.Count, "#1");
            Assert.AreEqual (1, table.Buckets[0].Nodes.Count, "#2");

            CheckBuckets ();
        }

        [Test]
        public void AddSimilar ()
        {
            for (int i = 0; i < Bucket.MaxCapacity * 3; i++) {
                byte[] id = (byte[]) this.id.Clone ();
                id[0] += (byte) i;
                table.Add (new Node (new NodeId (id), new IPEndPoint (IPAddress.Any, 0)));
            }

            Assert.AreEqual (Bucket.MaxCapacity * 3, table.CountNodes (), "#1");
            Assert.AreEqual (6, table.Buckets.Count, "#2");
            Assert.AreEqual (8, table.Buckets[0].Nodes.Count, "#3");
            Assert.AreEqual (8, table.Buckets[1].Nodes.Count, "#4");
            Assert.AreEqual (8, table.Buckets[2].Nodes.Count, "#5");
            Assert.AreEqual (0, table.Buckets[3].Nodes.Count, "#6");
            Assert.AreEqual (0, table.Buckets[4].Nodes.Count, "#7");
            Assert.AreEqual (0, table.Buckets[5].Nodes.Count, "#8");
            CheckBuckets ();
        }

        [Test]
        public void GetClosestTest ()
        {
            TestHelper.ManyNodes (out table, out List<NodeId> nodes);


            var closest = table.GetClosest (table.LocalNodeId).ToList ();
            Assert.AreEqual (8, closest.Count, "#1");
            for (int i = 0; i < 8; i++)
                Assert.IsTrue (closest.Exists (node => nodes[i].Equals (closest[i].Id)));
        }

        private void CheckBuckets ()
        {
            foreach (Bucket b in table.Buckets)
                foreach (Node n in b.Nodes)
                    Assert.IsTrue (n.Id >= b.Min && n.Id < b.Max);
        }
    }
}
