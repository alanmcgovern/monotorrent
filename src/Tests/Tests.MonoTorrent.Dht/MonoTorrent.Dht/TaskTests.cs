//
// TaskTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Net;
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
        readonly BEncodedString transactionId = "aa";

        [SetUp]
        public async Task Setup ()
        {
            counter = 0;
            listener = new TestListener ();
            engine = new DhtEngine ();
            await engine.SetListenerAsync (listener);
            node = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Any, 4));
        }

        [Test]
        [Repeat (10)]
        public async Task InitialiseFailure ()
        {
            var errorSource = new TaskCompletionSource<object> ();
            listener.MessageSent += (o, e) => errorSource.Task.GetAwaiter ().GetResult ();

            await engine.StartAsync (new byte[26], Array.Empty<string> ());
            Assert.AreEqual (DhtState.Initialising, engine.State);

            // Then set an error and make sure the engine state moves to 'NotReady'
            errorSource.SetException (new Exception ());
            await engine.WaitForState (DhtState.NotReady).WithTimeout (10000);
        }

        int counter;
        [Test]
        public async Task SendQueryTaskTimeout ()
        {
            engine.MessageLoop.Timeout = TimeSpan.Zero;

            Ping ping = new Ping (engine.LocalId);
            ping.TransactionId = transactionId;
            engine.MessageLoop.QuerySent += delegate (object o, SendQueryEventArgs e) {
                if (e.TimedOut)
                    counter++;
            };

            Assert.IsTrue ((await engine.SendQueryAsync (ping, node).WithTimeout (3000)).TimedOut, "#1");
            Assert.AreEqual (4, counter, "#2");
        }

        [Test]
        public async Task SendQueryTaskSucceed ()
        {
            var ping = new Ping (engine.LocalId) {
                TransactionId = transactionId
            };
            listener.MessageSent += (data, endpoint) => {
                engine.MessageLoop.DhtMessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (data.Span), out DhtMessage message);
                if (message is Ping && message.TransactionId.Equals (ping.TransactionId)) {
                    counter++;
                    PingResponse response = new PingResponse (node.Id, transactionId);
                    listener.RaiseMessageReceived (response, node.EndPoint);
                }
            };

            Assert.IsFalse (node.LastSeen < TimeSpan.FromSeconds (2));
            Assert.IsFalse ((await engine.SendQueryAsync (ping, node).WithTimeout (3000)).TimedOut, "#1");
            Assert.AreEqual (1, counter, "#2");
            Node n = engine.RoutingTable.FindNode (node.Id);
            Assert.IsNotNull (n, "#3");
            Assert.IsTrue (n.LastSeen < TimeSpan.FromSeconds (2));
        }

        [Test]
        public async Task NodeReplaceTest ()
        {
            int nodeCount = 0;
            Bucket b = new Bucket ();
            for (int i = 0; i < Bucket.MaxCapacity; i++) {
                Node n = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Any, i));
                n.Seen ();
                b.Add (n);
            }

            b.Nodes[3].Seen (TimeSpan.FromDays (5));
            b.Nodes[1].Seen (TimeSpan.FromDays (4));
            b.Nodes[5].Seen (TimeSpan.FromDays (3));

            listener.MessageSent += (data, endpoint) => {
                engine.MessageLoop.DhtMessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (data.Span), out DhtMessage message);

                b.Nodes.Sort ((l, r) => l.LastSeen.CompareTo (r.LastSeen));
                if ((endpoint.Port == 3 && nodeCount == 0) ||
                     (endpoint.Port == 1 && nodeCount == 1) ||
                     (endpoint.Port == 5 && nodeCount == 2)) {
                    Node n = b.Nodes.Find (no => no.EndPoint.Port == endpoint.Port);
                    n.Seen ();
                    PingResponse response = new PingResponse (n.Id, message.TransactionId);
                    listener.RaiseMessageReceived (response, node.EndPoint);
                    nodeCount++;
                }

            };

            ReplaceNodeTask task = new ReplaceNodeTask (engine, b, null);
            await task.Execute ().WithTimeout (4000);
        }

        [Test]
        public async Task BucketRefreshTest ()
        {
            List<Node> nodes = new List<Node> ();
            for (int i = 0; i < 5; i++)
                nodes.Add (new Node (NodeId.Create (), new IPEndPoint (IPAddress.Any, i)));

            listener.MessageSent += (data, endpoint) => {
                engine.MessageLoop.DhtMessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (data.Span), out DhtMessage message);

                Node current = nodes.Find (n => n.EndPoint.Port.Equals (endpoint.Port));
                if (current == null)
                    return;

                if (message is Ping) {
                    PingResponse r = new PingResponse (current.Id, message.TransactionId);
                    listener.RaiseMessageReceived (r, current.EndPoint);
                } else if (message is FindNode) {
                    FindNodeResponse response = new FindNodeResponse (current.Id, message.TransactionId);
                    response.Nodes = "";
                    listener.RaiseMessageReceived (response, current.EndPoint);
                }
            };

            foreach (var n in nodes)
                engine.RoutingTable.Add (n);

            foreach (Bucket b in engine.RoutingTable.Buckets) {
                b.Changed (TimeSpan.FromDays (1));
                foreach (var n in b.Nodes)
                    n.Seen (TimeSpan.FromDays (1));
            }

            await engine.RefreshBuckets ();

            foreach (Bucket b in engine.RoutingTable.Buckets) {
                Assert.IsTrue (b.LastChanged < TimeSpan.FromHours (1));
                Assert.IsTrue (b.Nodes.Exists (n => n.LastSeen < TimeSpan.FromHours (1)));
            }
        }

        [Test]
        public async Task ReplaceNodeTest ()
        {
            engine.MessageLoop.Timeout = TimeSpan.FromMilliseconds (0);
            Node replacement = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Loopback, 1337));
            for (int i = 0; i < 4; i++) {
                var n = new Node (NodeId.Create (), new IPEndPoint (IPAddress.Any, i));
                n.Seen (TimeSpan.FromDays (i));
                engine.RoutingTable.Add (n);
            }
            Node nodeToReplace = engine.RoutingTable.Buckets[0].Nodes[3];

            ReplaceNodeTask task = new ReplaceNodeTask (engine, engine.RoutingTable.Buckets[0], replacement);
            await task.Execute ().WithTimeout ();
            Assert.IsFalse (engine.RoutingTable.Buckets[0].Nodes.Contains (nodeToReplace), "#1");
            Assert.IsTrue (engine.RoutingTable.Buckets[0].Nodes.Contains (replacement), "#2");
        }
    }
}
