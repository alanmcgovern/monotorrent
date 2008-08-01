using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Net;
using MonoTorrent.Dht.Tasks;
using MonoTorrent.Dht.Messages;
using MonoTorrent.BEncoding;
using System.Threading;

namespace MonoTorrent.Dht.Tests
{
    [TestFixture]
    public class TaskTests
    {
        //static void Main(string[] args)
        //{
        //    TaskTests t = new TaskTests();
        //    t.Setup();
        //    t.SendQueryTaskSucceed();
        //}
        DhtEngine engine;
        TestListener listener;
        Node node;
        BEncodedString transactionId = "aa";
        ManualResetEvent handle;

        [SetUp]
        public void Setup()
        {
            counter = 0;
            listener = new TestListener();
            engine = new DhtEngine(listener);
            node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, 4));
            handle = new ManualResetEvent(false);
        }

        int counter;
        [Test]
        public void SendQueryTaskTimeout()
        {
            engine.TimeOut = TimeSpan.FromMilliseconds(75);

            Ping ping = new Ping(engine.LocalId);
            ping.TransactionId = transactionId;
            engine.MessageLoop.QuerySent += delegate (object o, SendQueryEventArgs e) {
                if(e.TimedOut)
                    counter++;
            };

            SendQueryTask task = new SendQueryTask(engine, ping, node);
            task.Completed += delegate { handle.Set(); };
            task.Execute();
            Assert.IsTrue(handle.WaitOne(3000, false), "#1");
            Assert.AreEqual(task.Retries, counter);
        }

        [Test]
        public void SendQueryTaskSucceed()
        {
            engine.TimeOut = TimeSpan.FromMilliseconds(75);

            Ping ping = new Ping(engine.LocalId);
            ping.TransactionId = transactionId;
            engine.MessageLoop.QuerySent += delegate(object o, SendQueryEventArgs e)
            {
                if (e.TimedOut)
                {
                    counter++;
                    PingResponse response = new PingResponse(node.Id);
                    response.TransactionId = transactionId;
                    listener.RaiseMessageReceived(response.Encode(), node.EndPoint);
                }
            };

            SendQueryTask task = new SendQueryTask(engine, ping, node);
            task.Completed += delegate { handle.Set(); };
            task.Execute();

            Assert.IsTrue(handle.WaitOne(3000, false), "#1");
            System.Threading.Thread.Sleep(200);
            Assert.AreEqual(1, counter, "#2");
            Node n = engine.RoutingTable.FindNode(this.node.Id);
            Assert.IsNotNull(n, "#3");
            Assert.Greater(n.LastSeen, DateTime.UtcNow.AddSeconds(-2));
        }

        int nodeCount = 0;
        [Test]
        public void NodeReplaceTest()
        {
            engine.TimeOut = TimeSpan.FromMilliseconds(75);
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
                    PingResponse response = new PingResponse(n.Id);
                    response.TransactionId = e.Query.TransactionId;
                    DhtEngine.MainLoop.Queue(delegate
                    {
                        //System.Threading.Thread.Sleep(100);
                        Console.WriteLine("Faking the receive");
                        listener.RaiseMessageReceived(response.Encode(), node.EndPoint);
                    });
                    nodeCount++;
                }

            };

            ReplaceNodeTask task = new ReplaceNodeTask(engine, b, null);
            // FIXME: Need to assert that node 0.0.0.0:0 is the one which failed - i.e. it should be replaced
            task.Completed += delegate(object o, TaskCompleteEventArgs e) { handle.Set(); };
            task.Execute();

            Assert.IsTrue(handle.WaitOne(4000, false), "#10");
        }
    }
}
