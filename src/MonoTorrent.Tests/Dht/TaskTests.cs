#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Net;
using MonoTorrent.Dht.Tasks;
using MonoTorrent.Dht.Messages;
using MonoTorrent.BEncoding;
using System.Threading;

namespace MonoTorrent.Dht
{
    public class TaskTests
    {
        //static void Main(string[] args)
        //{
        //    TaskTests t = new TaskTests();
        //    t.Setup();
        //    t.ReplaceNodeTest();
        //}

        DhtEngine engine;
        TestListener listener;
        Node node;
        BEncodedString transactionId = "aa";
        ManualResetEvent handle;

        public TaskTests()
        {
            counter = 0;
            listener = new TestListener();
            engine = new DhtEngine(listener);
            node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, 4));
            handle = new ManualResetEvent(false);
        }

        int counter;

        [Fact]
        public void SendQueryTaskTimeout()
        {
            engine.TimeOut = TimeSpan.FromMilliseconds(25);

            Ping ping = new Ping(engine.LocalId);
            ping.TransactionId = transactionId;
            engine.MessageLoop.QuerySent += delegate(object o, SendQueryEventArgs e)
            {
                if (e.TimedOut)
                    counter++;
            };

            SendQueryTask task = new SendQueryTask(engine, ping, node);
            task.Completed += delegate { handle.Set(); };
            task.Execute();
            Assert.True(handle.WaitOne(3000, false));
            Assert.Equal(task.Retries, counter);
        }

        [Fact]
        public void SendQueryTaskSucceed()
        {
            engine.TimeOut = TimeSpan.FromMilliseconds(25);

            Ping ping = new Ping(engine.LocalId);
            ping.TransactionId = transactionId;
            engine.MessageLoop.QuerySent += delegate(object o, SendQueryEventArgs e)
            {
                if (e.TimedOut)
                {
                    counter++;
                    PingResponse response = new PingResponse(node.Id, transactionId);
                    listener.RaiseMessageReceived(response, node.EndPoint);
                }
            };

            SendQueryTask task = new SendQueryTask(engine, ping, node);
            task.Completed += delegate { handle.Set(); };
            task.Execute();

            Assert.True(handle.WaitOne(3000, false));
            System.Threading.Thread.Sleep(200);
            Assert.Equal(1, counter);
            Node n = engine.RoutingTable.FindNode(this.node.Id);
            Assert.NotNull(n);
            Assert.True(n.LastSeen > DateTime.UtcNow.AddSeconds(-2));
        }

        int nodeCount = 0;

        [Fact]
        public void NodeReplaceTest()
        {
            engine.TimeOut = TimeSpan.FromMilliseconds(25);
            ManualResetEvent handle = new ManualResetEvent(false);
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

            engine.MessageLoop.QuerySent += delegate(object o, SendQueryEventArgs e)
            {
                if (!e.TimedOut)
                    return;

                b.Nodes.Sort();
                if ((e.EndPoint.Port == 3 && nodeCount == 0) ||
                    (e.EndPoint.Port == 1 && nodeCount == 1) ||
                    (e.EndPoint.Port == 5 && nodeCount == 2))
                {
                    Node n = b.Nodes.Find(delegate(Node no) { return no.EndPoint.Port == e.EndPoint.Port; });
                    n.Seen();
                    PingResponse response = new PingResponse(n.Id, e.Query.TransactionId);
                    DhtEngine.MainLoop.Queue(delegate
                    {
                        //System.Threading.Thread.Sleep(100);
                        Console.WriteLine("Faking the receive");
                        listener.RaiseMessageReceived(response, node.EndPoint);
                    });
                    nodeCount++;
                }
            };

            ReplaceNodeTask task = new ReplaceNodeTask(engine, b, null);
            // FIXME: Need to Assert.True node 0.0.0.0:0 is the one which failed - i.e. it should be replaced
            task.Completed += delegate(object o, TaskCompleteEventArgs e) { handle.Set(); };
            task.Execute();

            Assert.True(handle.WaitOne(4000, false));
        }

        [Fact]
        public void BucketRefreshTest()
        {
            List<Node> nodes = new List<Node>();
            for (int i = 0; i < 5; i++)
                nodes.Add(new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, i)));

            engine.TimeOut = TimeSpan.FromMilliseconds(25);
            engine.BucketRefreshTimeout = TimeSpan.FromMilliseconds(75);
            engine.MessageLoop.QuerySent += delegate(object o, SendQueryEventArgs e)
            {
                DhtEngine.MainLoop.Queue(delegate
                {
                    if (!e.TimedOut)
                        return;

                    Node current = nodes.Find(delegate(Node n) { return n.EndPoint.Port.Equals(e.EndPoint.Port); });
                    if (current == null)
                        return;

                    if (e.Query is Ping)
                    {
                        PingResponse r = new PingResponse(current.Id, e.Query.TransactionId);
                        listener.RaiseMessageReceived(r, current.EndPoint);
                    }
                    else if (e.Query is FindNode)
                    {
                        FindNodeResponse response = new FindNodeResponse(current.Id, e.Query.TransactionId);
                        response.Nodes = "";
                        listener.RaiseMessageReceived(response, current.EndPoint);
                    }
                });
            };

            engine.Add(nodes);
            engine.Start();

            System.Threading.Thread.Sleep(500);
            foreach (Bucket b in engine.RoutingTable.Buckets)
            {
                Assert.True(b.LastChanged > DateTime.UtcNow.AddSeconds(-2));
                Assert.True(
                    b.Nodes.Exists(delegate(Node n) { return n.LastSeen > DateTime.UtcNow.AddMilliseconds(-900); }));
            }
        }

        [Fact]
        public void ReplaceNodeTest()
        {
            engine.TimeOut = TimeSpan.FromMilliseconds(25);
            Node replacement = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Loopback, 1337));
            for (int i = 0; i < 4; i++)
            {
                Node node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, i));
                node.LastSeen = DateTime.UtcNow.AddMinutes(-i);
                engine.RoutingTable.Add(node);
            }
            Node nodeToReplace = engine.RoutingTable.Buckets[0].Nodes[3];

            ReplaceNodeTask task = new ReplaceNodeTask(engine, engine.RoutingTable.Buckets[0], replacement);
            task.Completed += delegate { handle.Set(); };
            task.Execute();
            Assert.True(handle.WaitOne(1000, true), "#a");
            Assert.False(engine.RoutingTable.Buckets[0].Nodes.Contains(nodeToReplace));
            Assert.True(engine.RoutingTable.Buckets[0].Nodes.Contains(replacement));
        }
    }
}

#endif