using System;
using System.Net;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class MessageHandlingTests
    {
        readonly BEncodedString transactionId = "cc";
        DhtEngine engine;
        Node node;
        TestListener listener;
        DhtMessageFactory DhtMessageFactory => engine.MessageLoop.DhtMessageFactory;

        [SetUp]
        public async Task Setup ()
        {
            listener = new TestListener ();
            node = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Any, 0));
            engine = new DhtEngine ();
            await engine.SetListenerAsync (listener);
        }

        [TearDown]
        public void Teardown ()
        {
            engine.Dispose ();
        }

        [Test]
        public void ErrorReceived ()
        {
            int failedCount = 0;
            var pingSuccessful = new TaskCompletionSource<bool> ();

            var ping = new Ping (node.Id) {
                TransactionId = transactionId
            };

            engine.MessageLoop.QuerySent += (o, e) => {
                // This ping should not time out.
                if (e.Query.TransactionId.Equals (ping.TransactionId))
                    pingSuccessful.TrySetResult (!e.TimedOut && e.Response == null && e.Error != null);
            };

            listener.MessageSent += (data, endpoint) => {
                engine.MessageLoop.DhtMessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (data.Span), out DhtMessage message);

                // This TransactionId should be registered and it should be pending a response.
                if (!DhtMessageFactory.IsRegistered (ping.TransactionId) || engine.MessageLoop.PendingQueries != 1)
                    pingSuccessful.TrySetResult (false);

                if (message.TransactionId.Equals (ping.TransactionId)) {
                    listener.RaiseMessageReceived (new ErrorMessage (ping.TransactionId, ErrorCode.ServerError, "Ooops"), node.EndPoint);
                    failedCount++;
                }
            };

            // Send the ping
            var task = engine.SendQueryAsync (ping, node);

            // The query should complete, and the message should not have timed out.
            Assert.IsTrue (task.Wait (100000), "#1");
            Assert.IsTrue (pingSuccessful.Task.Wait (1000), "#2");
            Assert.IsTrue (pingSuccessful.Task.Result, "#3");
            Assert.IsFalse (DhtMessageFactory.IsRegistered (ping.TransactionId), "#4");
            Assert.AreEqual (0, engine.MessageLoop.PendingQueries, "#5");
            Assert.AreEqual (1, failedCount, "#6");
        }

        [Test]
        public async Task SendPing_Synchronous ()
            => await SendPing (false);

        [Test]
        public async Task SendPing_Asynchronous ()
            => await SendPing (true);

        async Task SendPing (bool asynchronous)
        {
            listener.SendAsynchronously = asynchronous;

            var tcs = new TaskCompletionSource<object> ();
            listener.MessageSent += (data, endpoint) => {
                engine.MessageLoop.DhtMessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (data.Span), out DhtMessage message);

                if (message is Ping && endpoint.Equals (node.EndPoint)) {
                    var response = new PingResponse (node.Id, message.TransactionId);
                    listener.RaiseMessageReceived (response, endpoint);
                }
            };
            engine.MessageLoop.QuerySent += (o, e) => {
                if (e.Query is Ping && e.EndPoint.Equals (node.EndPoint))
                    tcs.TrySetResult (null);
            };

            Assert.AreEqual (NodeState.Unknown, node.State, "#1");

            // Should cause an implicit Ping to be sent to the node to verify it's alive.
            await engine.Add (node);

            Assert.IsTrue (tcs.Task.Wait (10_000), "#1a");
            Assert.IsTrue (node.LastSeen < TimeSpan.FromSeconds (1), "#2");
            Assert.AreEqual (NodeState.Good, node.State, "#3");
        }

        [Test]
        public void PingTimeout ()
        {
            bool pingSuccessful = false;
            var ping = new Ping (node.Id) {
                TransactionId = transactionId
            };

            bool timedOutPingSuccessful = false;
            var timedOutPing = new Ping (node.Id) {
                TransactionId = (BEncodedNumber) 5
            };

            listener.MessageSent += (data, endpoint) => {
                engine.MessageLoop.DhtMessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (data.Span), out DhtMessage message);

                if (message.TransactionId.Equals (ping.TransactionId)) {
                    var response = new PingResponse (node.Id, transactionId);
                    listener.RaiseMessageReceived (response, endpoint);
                }
            };

            engine.MessageLoop.QuerySent += (o, e) => {
                // This ping should not time out.
                if (e.Query.TransactionId.Equals (ping.TransactionId))
                    pingSuccessful = !e.TimedOut;

                // This ping should time out.
                if (e.Query.TransactionId.Equals (timedOutPing.TransactionId))
                    timedOutPingSuccessful = e.TimedOut;
            };

            // Send the ping which will be responded to
            engine.SendQueryAsync (ping, node).WithTimeout ().Wait ();
            Assert.AreEqual (0, node.FailedCount, "#0b");

            engine.MessageLoop.Timeout = TimeSpan.Zero;
            node.Seen (TimeSpan.FromHours (1));

            // Send a ping which will time out
            engine.SendQueryAsync (timedOutPing, node).WithTimeout ().Wait ();

            Assert.AreEqual (4, node.FailedCount, "#1");
            Assert.AreEqual (NodeState.Bad, node.State, "#2");
            Assert.IsTrue (node.LastSeen >= TimeSpan.FromHours (1), "#3");
            Assert.IsTrue (pingSuccessful, "#4");
            Assert.IsTrue (pingSuccessful, "#5");
        }

        [Test]
        public void TransactionIdCollision ()
        {
            // See what happens if we receive a query with the same ID as a pending query we are sending.
            var pingSuccessful = new TaskCompletionSource<bool> ();
            var ping = new Ping (node.Id) {
                TransactionId = transactionId
            };

            engine.MessageLoop.QuerySent += (o, e) => {
                // This ping should not time out.
                if (e.Query.TransactionId.Equals (ping.TransactionId))
                    pingSuccessful.TrySetResult (!e.TimedOut);
            };

            // Send the ping
            var task = engine.SendQueryAsync (ping, node);

            // Some other node sends us a Query with the same transaction ID
            listener.RaiseMessageReceived (ping, new IPEndPoint (IPAddress.Any, 9876));

            // Now we receive a response to our original ping
            listener.RaiseMessageReceived (new PingResponse (node.Id, ping.TransactionId), node.EndPoint);

            // The query should complete, and the message should not have timed out.
            Assert.IsTrue (task.Wait (1000), "#1");
            Assert.IsTrue (pingSuccessful.Task.Wait (1000), "#2");
            Assert.IsTrue (pingSuccessful.Task.Result, "#3");
            Assert.AreEqual (0, engine.MessageLoop.PendingQueries, "#4");
        }
    }
}
