using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Dht.Messages;
using MonoTorrent.BEncoding;
using System.Net;
using System.Threading;
using MonoTorrent.Dht.Tasks;

namespace MonoTorrent.Dht.Tests
{
    [TestFixture]
    public class MessageHandlingTests
    {
        //static void Main(string[] args)
        //{
        //    TaskTests t = new TaskTests();
        //    t.Setup();
        //    t.BucketRefreshTest();
        //}
        BEncodedString transactionId = "cc";
        DhtEngine engine;
        Node node;
        TestListener listener;

        [SetUp]
        public void Setup()
        {
            listener = new TestListener();
            node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, 0));
            engine = new DhtEngine(listener);
            //engine.Add(node);
        }

        [TearDown]
        public void Teardown()
        {
            engine.Dispose();
        }

        [Test]
        public void SendPing()
        {
            engine.Add(node);
            engine.TimeOut = TimeSpan.FromMilliseconds(75);
            ManualResetEvent handle = new ManualResetEvent(false);
            engine.MessageLoop.QuerySent += delegate(object o, SendQueryEventArgs e) {
                if (!e.TimedOut && e.Query is Ping)
                    handle.Set();

                if (!e.TimedOut || !(e.Query is Ping))
                    return;

                PingResponse response = new PingResponse(node.Id);
                response.TransactionId = e.Query.TransactionId;
                listener.RaiseMessageReceived(response, e.EndPoint);
            };

            Assert.AreEqual(NodeState.Unknown, node.State, "#1");

            DateTime lastSeen = node.LastSeen;
            Assert.IsTrue(handle.WaitOne(1000, false), "#1a");
            Node nnnn = node;
            node = engine.RoutingTable.FindNode(nnnn.Id);
            Assert.Less(lastSeen, node.LastSeen, "#2");
            Assert.AreEqual(NodeState.Good, node.State, "#3");
        }

        [Test]
        public void PingTimeout()
        {
            engine.TimeOut = TimeSpan.FromHours(1);
            // Send ping
            Ping ping = new Ping(node.Id);
            ping.TransactionId = transactionId;

            ManualResetEvent handle = new ManualResetEvent(false);
            SendQueryTask task = new SendQueryTask(engine, ping, node);
            task.Completed += delegate {
                handle.Set();
            };
            task.Execute();

            // Receive response
            PingResponse response = new PingResponse(node.Id);
            response.TransactionId = transactionId;
            listener.RaiseMessageReceived(response, node.EndPoint);

            Assert.IsTrue(handle.WaitOne(1000, true), "#0");

            engine.TimeOut = TimeSpan.FromMilliseconds(75);
            DateTime lastSeen = node.LastSeen;

            // Time out a ping
            ping = new Ping(node.Id);
            ping.TransactionId = "ab";

            task = new SendQueryTask(engine, ping, node, 4);
            task.Completed += delegate { handle.Set(); };

            handle.Reset();
            task.Execute();
            handle.WaitOne();

            Assert.AreEqual(4, node.FailedCount, "#1");
            Assert.AreEqual(NodeState.Bad, node.State, "#2");
            Assert.AreEqual(lastSeen, node.LastSeen, "#3");
        }

        void FakePingResponse(object sender, SendQueryEventArgs e)
        {
            if (!e.TimedOut || !(e.Query is Ping))
                return;

            SendQueryTask task = (SendQueryTask)e.Task;
            PingResponse response = new PingResponse(task.Target.Id);
            listener.RaiseMessageReceived(response, task.Target.EndPoint);
        }
    }
}
