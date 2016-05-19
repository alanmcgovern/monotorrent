#if !DISABLE_DHT
using System;
using System.Net;
using System.Threading;
using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Dht.Tasks;
using Xunit;

namespace MonoTorrent.Dht
{
    public class MessageHandlingTests : IDisposable
    {
        public MessageHandlingTests()
        {
            listener = new TestListener();
            node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, 0));
            engine = new DhtEngine(listener);
            //engine.Add(node);
        }

        public void Dispose()
        {
            engine.Dispose();
        }

        //static void Main(string[] args)
        //{
        //    TaskTests t = new TaskTests();
        //    t.Setup();
        //    t.BucketRefreshTest();
        //}
        private readonly BEncodedString transactionId = "cc";
        private readonly DhtEngine engine;
        private Node node;
        private readonly TestListener listener;

        [Fact]
        public void PingTimeout()
        {
            engine.TimeOut = TimeSpan.FromHours(1);
            // Send ping
            var ping = new Ping(node.Id);
            ping.TransactionId = transactionId;

            var handle = new ManualResetEvent(false);
            var task = new SendQueryTask(engine, ping, node);
            task.Completed += delegate { handle.Set(); };
            task.Execute();

            // Receive response
            var response = new PingResponse(node.Id, transactionId);
            listener.RaiseMessageReceived(response, node.EndPoint);

            Assert.True(handle.WaitOne(1000, true));

            engine.TimeOut = TimeSpan.FromMilliseconds(75);
            var lastSeen = node.LastSeen;

            // Time out a ping
            ping = new Ping(node.Id);
            ping.TransactionId = (BEncodedString) "ab";

            task = new SendQueryTask(engine, ping, node, 4);
            task.Completed += delegate { handle.Set(); };

            handle.Reset();
            task.Execute();
            handle.WaitOne();

            Assert.Equal(4, node.FailedCount);
            Assert.Equal(NodeState.Bad, node.State);
            Assert.Equal(lastSeen, node.LastSeen);
        }

        [Fact]
        public void SendPing()
        {
            engine.Add(node);
            engine.TimeOut = TimeSpan.FromMilliseconds(75);
            var handle = new ManualResetEvent(false);
            engine.MessageLoop.QuerySent += delegate(object o, SendQueryEventArgs e)
            {
                if (!e.TimedOut && e.Query is Ping)
                    handle.Set();

                if (!e.TimedOut || !(e.Query is Ping))
                    return;

                var response = new PingResponse(node.Id, e.Query.TransactionId);
                listener.RaiseMessageReceived(response, e.EndPoint);
            };

            Assert.Equal(NodeState.Unknown, node.State);

            var lastSeen = node.LastSeen;
            Assert.True(handle.WaitOne(1000, false));
            var nnnn = node;
            node = engine.RoutingTable.FindNode(nnnn.Id);
            Assert.True(lastSeen < node.LastSeen);
            Assert.Equal(NodeState.Good, node.State);
        }

//        void FakePingResponse(object sender, SendQueryEventArgs e)
//        {
//            if (!e.TimedOut || !(e.Query is Ping))
//                return;
//
//            SendQueryTask task = (SendQueryTask)e.Task;
//            PingResponse response = new PingResponse(task.Target.Id);

//            listener.RaiseMessageReceived(response, task.Target.EndPoint);
//        }
    }
}

#endif