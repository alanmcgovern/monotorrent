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
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages.UdpTracker;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Tracker.Listeners;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class UdpTrackerTests
    {
        AnnounceParameters announceparams = new AnnounceParameters(100, 50, int.MaxValue,
            TorrentEvent.Completed,
            new InfoHash (new byte[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 }),
            false, new string('a', 20), null, 1515, false);
        ScrapeParameters scrapeParams = new ScrapeParameters(new InfoHash(new byte[20]));
        MonoTorrent.Tracker.Tracker server;
        UdpTracker tracker;
        IgnoringListener listener;
        List<BEncodedString> keys;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            keys = new List<BEncodedString>();
            server = new MonoTorrent.Tracker.Tracker();
            server.AllowUnregisteredTorrents = true;
            listener = new IgnoringListener(0);
            listener.AnnounceReceived += delegate(object o, MonoTorrent.Tracker.AnnounceParameters e)
            {
                keys.Add(e.Key);
            };
            server.RegisterListener(listener);

            listener.Start();
        }

        [SetUp]
        public void Setup()
        {
            keys.Clear();
            tracker = (UdpTracker)TrackerFactory.Create(new Uri($"udp://127.0.0.1:{listener.EndPoint.Port}/announce/"));
            announceparams = announceparams.WithPort (listener.EndPoint.Port);
            tracker.RetryDelay = TimeSpan.FromMilliseconds (50);

            listener.IgnoreAnnounces = false;
            listener.IgnoreConnects = false;
            listener.IgnoreErrors = false;
            listener.IgnoreScrapes = false;
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            listener.Stop();
            server.Dispose();
        }

        [Test]
        public void AnnounceMessageTest()
        {
            AnnounceMessage m = new AnnounceMessage(0, 12345, announceparams);
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
            peers.Add(new Peer(new string('1', 20), new Uri("ipv4://127.0.0.1:1")));
            peers.Add(new Peer(new string('2', 20), new Uri("ipv4://127.0.0.1:2")));
            peers.Add(new Peer(new string('3', 20), new Uri("ipv4://127.0.0.1:3")));

            AnnounceResponseMessage m = new AnnounceResponseMessage(12345, TimeSpan.FromSeconds(10), 43, 65, peers);
            AnnounceResponseMessage d = (AnnounceResponseMessage)UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Response);
            Check(m, MessageType.Response);

            Assert.AreEqual(1, m.Action);
            Assert.AreEqual(m.Action, d.Action);
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.AreEqual(12345, d.TransactionId);
        }

        [Test]
        public void ConnectMessageTest()
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
            Assert.AreEqual(12345, d.ConnectionId);
            Assert.AreEqual(5371, d.TransactionId);

        }

        [Test]
        public void ScrapeMessageTest()
        {
            List<byte[]> hashes = new List<byte[]>();
            Random r = new Random();
            byte[] hash1 = new byte[20];
            byte[] hash2 = new byte[20];
            byte[] hash3 = new byte[20];
            r.NextBytes(hash1);
            r.NextBytes(hash2);
            r.NextBytes(hash3);
            hashes.Add(hash1);
            hashes.Add(hash2);
            hashes.Add(hash3);

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

        [Test]
        public async Task AnnounceTest()
        {
            await tracker.AnnounceAsync(announceparams);
        }

        [Test]
        public void AnnounceTest_NoConnect()
        {
            listener.IgnoreConnects = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));
        }

        [Test]
        public async Task AnnounceTest_NoConnect_ThenConnect()
        {
            listener.IgnoreConnects = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));

            listener.IgnoreConnects = false;
            await tracker.AnnounceAsync (announceparams);
        }

        [Test]
        public void AnnounceTest_NoAnnounce()
        {
            listener.IgnoreAnnounces = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));
        }

        [Test]
        public async Task AnnounceTest_NoAnnounce_ThenAnnounce()
        {
            listener.IgnoreAnnounces = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));

            listener.IgnoreAnnounces = false;
            await tracker.AnnounceAsync (announceparams);
        }

        [Test]
        public async Task ScrapeTest()
        {
            await tracker.ScrapeAsync (scrapeParams);
            Assert.AreEqual(0, tracker.Complete, "#1");
            Assert.AreEqual(0, tracker.Incomplete, "#2");
            Assert.AreEqual(0, tracker.Downloaded, "#3");
        }

        [Test]
        public void ScrapeTest_NoConnect()
        {
            listener.IgnoreConnects = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));
        }

        [Test]
        public async Task ScrapeTest_NoConnect_ThenConnect()
        {
            listener.IgnoreConnects = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));

            listener.IgnoreConnects = false;
            await tracker.ScrapeAsync (scrapeParams);
        }

        [Test]
        public void ScrapeTest_NoScrapes()
        {
            listener.IgnoreScrapes = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));
        }

        [Test]
        public async Task ScrapeTest_NoScrapes_ThenScrape()
        {
            listener.IgnoreScrapes = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));

            listener.IgnoreScrapes = false;
            await tracker.ScrapeAsync (scrapeParams);
        }
    }

    class IgnoringListener : UdpTrackerListener
    {
        public bool IgnoreConnects;
        public bool IgnoreAnnounces;
        public bool IgnoreErrors;
        public bool IgnoreScrapes;

        public IgnoringListener(int port)
            : base(port)
        {

        }

        protected override async Task ReceiveConnect(UdpClient client, ConnectMessage connectMessage, IPEndPoint remotePeer)
        {
            if (!IgnoreConnects)
                await base.ReceiveConnect(client, connectMessage, remotePeer);
        }

        protected override async Task ReceiveAnnounce(UdpClient client, AnnounceMessage announceMessage, IPEndPoint remotePeer)
        {
            if (!IgnoreAnnounces)
                await base.ReceiveAnnounce(client, announceMessage, remotePeer);
        }

        protected override async Task ReceiveError(UdpClient client, ErrorMessage errorMessage, IPEndPoint remotePeer)
        {
            if (!IgnoreErrors)
                await base.ReceiveError(client, errorMessage, remotePeer);
        }

        protected override async Task ReceiveScrape(UdpClient client, ScrapeMessage scrapeMessage, IPEndPoint remotePeer)
        {
            if (!IgnoreScrapes)
                await base.ReceiveScrape(client, scrapeMessage, remotePeer);
        }
    }
}
