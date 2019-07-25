using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Dht.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class TaskTests
    {
        DhtEngine engine;
        TestListener listener;
        Node node;
        BEncodedString transactionId = "aa";

        [SetUp]
        public void Setup()
        {
            counter = 0;
            listener = new TestListener();
            engine = new DhtEngine(listener);
            node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, 4));
        }

        int counter;
        [Test]
        public void SendQueryTaskTimeout()
        {
            engine.Timeout = TimeSpan.Zero;

            Ping ping = new Ping(engine.LocalId);
            ping.TransactionId = transactionId;
            engine.MessageLoop.QuerySent += delegate (object o, SendQueryEventArgs e) {
                if(e.TimedOut)
                    counter++;
            };

            Assert.IsTrue(engine.SendQueryAsync (ping, node).Wait (3000), "#1");
            Assert.AreEqual (4, counter, "#2");
        }

        [Test]
        public void SendQueryTaskSucceed()
        {
            var ping = new Ping(engine.LocalId) {
                TransactionId = transactionId
            };
            listener.MessageSent += (message, endpoint) => {
                if (message is Ping && message.TransactionId.Equals (ping.TransactionId)) {
                    counter++;
                    PingResponse response = new PingResponse(node.Id, transactionId);
                    listener.RaiseMessageReceived(response, node.EndPoint);
                }
            };

            Assert.IsTrue(engine.SendQueryAsync (ping, node).Wait (3000), "#1");
            Assert.AreEqual(1, counter, "#2");
            Node n = engine.RoutingTable.FindNode(this.node.Id);
            Assert.IsNotNull(n, "#3");
            Assert.IsTrue(n.LastSeen > DateTime.UtcNow.AddSeconds(-2));
        }

        [Test]
        public void NodeReplaceTest()
        {
            int nodeCount = 0;
            Bucket b = new Bucket();
            for (int i = 0; i < Bucket.MaxCapacity; i++)
            {
                Node n = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, i));
                n.LastSeen = DateTime.UtcNow;
                b.Add(n);
            }

            b.Nodes[3].LastSeen = DateTime.UtcNow.AddDays(-5);
            b.Nodes[1].LastSeen = DateTime.UtcNow.AddDays(-4);
            b.Nodes[5].LastSeen = DateTime.UtcNow.AddDays(-3);

            listener.MessageSent += (message, endpoint) => {

                b.Nodes.Sort();
                if ((endpoint.Port == 3 && nodeCount == 0) ||
                     (endpoint.Port == 1 && nodeCount == 1) ||
                     (endpoint.Port == 5 && nodeCount == 2))
                {
                    Node n = b.Nodes.Find(delegate(Node no) { return no.EndPoint.Port == endpoint.Port; });
                    n.Seen();
                    PingResponse response = new PingResponse(n.Id, message.TransactionId);
                    listener.RaiseMessageReceived(response, node.EndPoint);
                    nodeCount++;
                }

            };

            ReplaceNodeTask task = new ReplaceNodeTask(engine, b, null);
            Assert.IsTrue(task.Execute ().Wait (4000), "#10");
        }

        [Test]
        public async Task BucketRefreshTest()
        {
            List<Node> nodes = new List<Node>();
            for (int i = 0; i < 5; i++)
                nodes.Add(new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, i)));

            engine.Timeout = TimeSpan.FromMilliseconds(25);
            engine.BucketRefreshTimeout = TimeSpan.FromMilliseconds(75);
            listener.MessageSent += (message, endpoint) => {
                Node current = nodes.Find(delegate(Node n) { return n.EndPoint.Port.Equals(endpoint.Port); });
                if (current == null)
                    return;

                if (message is Ping)
                {
                    PingResponse r = new PingResponse(current.Id, message.TransactionId);
                    listener.RaiseMessageReceived(r, current.EndPoint);
                }
                else if (message is FindNode)
                {
                    FindNodeResponse response = new FindNodeResponse(current.Id, message.TransactionId);
                    response.Nodes = "";
                    listener.RaiseMessageReceived(response, current.EndPoint);
                }
            };

            engine.Add(nodes);

            foreach (Bucket b in engine.RoutingTable.Buckets)
                b.LastChanged = DateTime.MinValue;

            await engine.StartAsync();
            await engine.WaitForState (DhtState.Ready);

            foreach (Bucket b in engine.RoutingTable.Buckets)
            {
                Assert.IsTrue(b.LastChanged > DateTime.UtcNow.AddSeconds(-2));
                Assert.IsTrue(b.Nodes.Exists(delegate(Node n) { return n.LastSeen > DateTime.UtcNow.AddMilliseconds(-900); }));
            }
        }

        [Test]
        public void ReplaceNodeTest()
        {
            engine.Timeout = TimeSpan.FromMilliseconds(25);
            Node replacement = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Loopback, 1337));
            for(int i=0; i < 4; i++)
            {
                Node node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, i));
                node.LastSeen = DateTime.UtcNow.AddMinutes(-i);
                engine.RoutingTable.Add(node);
            }
            Node nodeToReplace = engine.RoutingTable.Buckets[0].Nodes[3];

            ReplaceNodeTask task = new ReplaceNodeTask(engine, engine.RoutingTable.Buckets[0], replacement);
            Assert.IsTrue(task.Execute ().Wait (1000), "#a");
            Assert.IsFalse(engine.RoutingTable.Buckets[0].Nodes.Contains(nodeToReplace), "#1");
            Assert.IsTrue(engine.RoutingTable.Buckets[0].Nodes.Contains(replacement), "#2");
        }
    }
}
