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
            //128 done in setup => distance 24
            id = new byte[20];
            id[1] = 12;//=> distance 148
            Node n1 = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table.Add(n1);
            
            id = new byte[20];
            id[1] = 46;//=> distance 174
            Node n2 = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table.Add(n2);
            
            id = new byte[20];
            id[1] = 78;//=> distance 214
            Node n3 = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table.Add(n3);
            
            id = new byte[20];
            id[1] = 127;//=> distance 231
            Node n4 = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table.Add(n4);
            
            id = new byte[20];
            id[1] = 232;//=> distance 112
            Node n5 = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table.Add(n5);
            
            id = new byte[20];
            id[1] = 196;//=> distance 92
            Node n6 = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table.Add(n6);
            
            id = new byte[20];
            id[1] = 253;//=> distance 101
            Node n7 = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table.Add(n7);
            
            id = new byte[20];
            id[1] = 0;//=> distance 152
            Node n8 = new Node(new NodeId(id), new System.Net.IPEndPoint(IPAddress.Any, 0));
            table.Add(n8);
            
            id = new byte[20];
            id[1] = 152; 
            NodeId target = new NodeId(id);
            IList<Node> nodes = table.GetClosest(target);
            
            Assert.AreEqual(8, nodes.Count,"#1");
            Assert.IsTrue(nodes.Contains(n1),"#2");
            Assert.IsTrue(nodes.Contains(n2),"#3");
            Assert.IsTrue(nodes.Contains(n3),"#4");
            Assert.IsFalse(nodes.Contains(n4),"#5");
            Assert.IsTrue(nodes.Contains(n5),"#6");
            Assert.IsTrue(nodes.Contains(n6),"#7");
            Assert.IsTrue(nodes.Contains(n7),"#8");
            Assert.IsTrue(nodes.Contains(n8),"#9");
            Assert.IsTrue(nodes.Contains(n),"#10");

        }
        
        private void CheckBuckets()
        {
            foreach (Bucket b in table.Buckets)
                foreach (Node n in b.Nodes)
                    Assert.IsTrue(n.Id >= b.Min && n.Id < b.Max);
        }
    }
}
