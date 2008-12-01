//
// UdpTrackerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Messages.UdpTracker;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using MonoTorrent.Client;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class UdpTrackerTests
    {
        //static void Main(string[] args)
        //{
        //    UdpTrackerTests t = new UdpTrackerTests();
        //    t.ConnectTest();
        //    t.ConnectResponseTest();
        //    t.AnnounceTest();
        //    t.AnnounceResponseTest();
        //    t.ScrapeTest();
        //    t.ScrapeResponseTest();
        //}

        AnnounceParameters announceparams = new AnnounceParameters(100, 50, int.MaxValue,
            MonoTorrent.Common.TorrentEvent.Completed,
            new byte[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 },
            new TrackerConnectionID(null, false, MonoTorrent.Common.TorrentEvent.Completed, null),
            false, new string('a', 20), null, 1515);

        [Test]
        public void AnnounceTest()
        {
            AnnounceMessage m = new AnnounceMessage(12345, announceparams);
            AnnounceMessage d = (AnnounceMessage)UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);

            Assert.AreEqual(1, m.Action);
            Assert.AreEqual(m.Action, d.Action);
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.AreEqual(12345, d.ConnectionId);
        }

        [Test]
        public void AnnounceResponseTest()
        {
            List<Peer> peers = new List<Peer>();
            peers.Add(new Peer(new string('1', 20), new Uri("tcp://127.0.0.1:1")));
            peers.Add(new Peer(new string('2', 20), new Uri("tcp://127.0.0.1:2")));
            peers.Add(new Peer(new string('3', 20), new Uri("tcp://127.0.0.1:3")));

            AnnounceResponseMessage m = new AnnounceResponseMessage(12345, 123, 43, 65, peers);
            AnnounceResponseMessage d = (AnnounceResponseMessage)UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Response);
            Check(m, MessageType.Response);

            Assert.AreEqual(1, m.Action);
            Assert.AreEqual(m.Action, d.Action);
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.AreEqual(12345, d.TransactionId);
        }

        [Test]
        public void ConnectTest()
        {
            ConnectMessage m = new ConnectMessage();
            ConnectMessage d = (ConnectMessage)UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);
            
            Assert.AreEqual(0, m.Action, "#0");
            Assert.AreEqual(m.Action, d.Action, "#1");
            Assert.AreEqual(m.ConnectionId, d.ConnectionId, "#2");
            Assert.AreEqual(m.TransactionId, d.TransactionId, "#3");
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()), "#4");
        }

        [Test]
        public void ConnectResponseTest()
        {
            ConnectResponseMessage m = new ConnectResponseMessage(5371, 12345);
            ConnectResponseMessage d = (ConnectResponseMessage)UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Response);
            Check(m, MessageType.Response);
            
            Assert.AreEqual(0, m.Action, "#0"); 
            Assert.AreEqual(m.Action, d.Action, "#1");
            Assert.AreEqual(m.ConnectionId, d.ConnectionId, "#2");
            Assert.AreEqual(m.TransactionId, d.TransactionId, "#3");
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()), "#4");
            Assert.AreEqual(5371, d.ConnectionId);
            Assert.AreEqual(12345, d.TransactionId);

        }

        [Test]
        public void ScrapeTest()
        {
            List<byte[]> hashes = new List<byte[]>();
            hashes.Add(MonoTorrent.Dht.NodeId.Create().Bytes);
            hashes.Add(MonoTorrent.Dht.NodeId.Create().Bytes);
            hashes.Add(MonoTorrent.Dht.NodeId.Create().Bytes);

            ScrapeMessage m = new ScrapeMessage(12345, 123, hashes);
            ScrapeMessage d = (ScrapeMessage)UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);
            
            Assert.AreEqual(2, m.Action);
            Assert.AreEqual(m.Action, d.Action);
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()));
        }

        [Test]
        public void ScrapeResponseTest()
        {
            List<ScrapeDetails> details = new List<ScrapeDetails>();
            details.Add(new ScrapeDetails(1, 2, 3));
            details.Add(new ScrapeDetails(4, 5, 6));
            details.Add(new ScrapeDetails(7, 8, 9));
            
            ScrapeResponseMessage m = new ScrapeResponseMessage(12345, details);
            ScrapeResponseMessage d = (ScrapeResponseMessage)UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Response);
            Check(m, MessageType.Response);
            
            Assert.AreEqual(2, m.Action);
            Assert.AreEqual(m.Action, d.Action);
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.AreEqual(12345, d.TransactionId);
        }

        void Check(UdpTrackerMessage message, MessageType type)
        {
            byte[] e = message.Encode();
            Assert.AreEqual(e.Length, message.ByteLength, "#1");
            Assert.IsTrue(Toolbox.ByteMatch(e, UdpTrackerMessage.DecodeMessage(e, 0, e.Length, type).Encode()), "#2");
        }
    }
}
