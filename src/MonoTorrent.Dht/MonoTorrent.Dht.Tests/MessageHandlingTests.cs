using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Dht.Messages;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Tests
{
    [TestFixture]
    public class MessageHandlingTests
    {
        public static void Main(String[] args)
        {
            MessageHandlingTests t = new MessageHandlingTests();
            t.Setup();
            t.SendPing();
            t.Setup();
            t.PingTimeout();
        }

        BEncodedString transactionId = "aa";
        DhtEngine engine;
        Node node;
        TestListener listener;

        [SetUp]
        public void Setup()
        {

            listener = new TestListener();
            node = new Node(NodeId.Create());
            engine = new DhtEngine(listener);
            engine.Add(node);
        }

        [Test]
        public void SendPing()
        {
            // Send a ping
            Ping ping = new Ping(node.Id);
            ping.TransactionId = transactionId;
            engine.MessageLoop.EnqueueSend(ping, node);

            Assert.AreEqual(NodeState.Unknown, node.State, "#1");

            DateTime lastSeen = node.LastSeen;
            PingResponse response = new PingResponse(node.Id);
            response.TransactionId = transactionId;
            listener.RaiseMessageReceived(response, node.ContactInfo.EndPoint);
            System.Threading.Thread.Sleep(10);
            Assert.Less(lastSeen, node.LastSeen, "#2");
            Assert.AreEqual(NodeState.Good, node.State, "#3");
        }

        [Test]
        public void PingTimeout()
        {
            // Send ping
            Ping ping = new Ping(node.Id);
            ping.TransactionId = transactionId;
            engine.MessageLoop.EnqueueSend(ping, node);

            // Receive response
            PingResponse response = new PingResponse(node.Id);
            response.TransactionId = transactionId;
            listener.RaiseMessageReceived(response, node.ContactInfo.EndPoint);

            engine.TimeOut = 2;
            DateTime lastSeen = node.LastSeen;
            // Time out a few pings
            for (int i = 0; i < 4; i++)
            {
                ping = new Ping(node.Id);
                ping.TransactionId = i.ToString();
                engine.MessageLoop.EnqueueSend(ping, node);
                System.Threading.Thread.Sleep(10);
            }

            Assert.AreEqual(4, node.FailedCount, "#1");
            Assert.AreEqual(NodeState.Bad, node.State, "#2");
            Assert.AreEqual(lastSeen, node.LastSeen, "#3");
        }
    }
}
