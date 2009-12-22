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
using System.Threading;
using System.Net;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class UdpTrackerTests
    {
        static void Main(string[] args)
        {
            UdpTrackerTests t = new UdpTrackerTests();
            t.ConnectMessageTest();
            t.ConnectResponseTest();
            t.AnnounceMessageTest();
            t.AnnounceResponseTest();
            t.ScrapeMessageTest();
            t.ScrapeResponseTest();
            t.FixtureSetup();

            t.AnnounceTest();
            t.Setup();
            t.ScrapeTest();

            t.FixtureTeardown();
        }

        AnnounceParameters announceparams = new AnnounceParameters(100, 50, int.MaxValue,
            MonoTorrent.Common.TorrentEvent.Completed,
            new InfoHash (new byte[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 }),
            false, new string('a', 20), null, 1515);
        MonoTorrent.Tracker.Tracker server;
        MonoTorrent.Tracker.Listeners.UdpListener listener;
        List<string> keys;
        string prefix = "udp://localhost:6767/announce/";

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            keys = new List<string>();
            server = new MonoTorrent.Tracker.Tracker();
            server.AllowUnregisteredTorrents = true;
            listener = new MonoTorrent.Tracker.Listeners.UdpListener(6767);
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
        }

        [TestFixtureTearDown]
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
            peers.Add(new Peer(new string('1', 20), new Uri("tcp://127.0.0.1:1")));
            peers.Add(new Peer(new string('2', 20), new Uri("tcp://127.0.0.1:2")));
            peers.Add(new Peer(new string('3', 20), new Uri("tcp://127.0.0.1:3")));

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
        public void AnnounceTest()
        {
            UdpTracker t = (UdpTracker)TrackerFactory.Create(new Uri(prefix));
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e)
            {
                p = e;
                id.WaitHandle.Set();
            };
            MonoTorrent.Client.Tracker.AnnounceParameters pars = new AnnounceParameters();
            pars.InfoHash = new InfoHash(new byte[20]);
            pars.PeerId = "";

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#1");
            Assert.IsTrue(p.Successful);
            //Assert.AreEqual(keys[0], t.Key, "#2");
        }

        [Test]
        public void AnnounceTest_NoConnect()
        {
            IgnoringListener listener = new IgnoringListener(57532);
            try
            {
                listener.IgnoreConnects = true;
                listener.Start();
                OfflineAnnounceTest();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void AnnounceTest_NoAnnounce()
        {
            IgnoringListener listener = new IgnoringListener(57532);
            try
            {
                listener.IgnoreAnnounces = true;
                listener.Start();
                OfflineAnnounceTest();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void ScrapeTest_NoConnect()
        {
            IgnoringListener listener = new IgnoringListener(57532);
            try
            {
                listener.IgnoreConnects = true;
                listener.Start();
                OfflineScrapeTest();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void ScrapeTest_NoScrapes()
        {
            IgnoringListener listener = new IgnoringListener(57532);
            try
            {
                listener.IgnoreScrapes = true;
                listener.Start();
                OfflineScrapeTest();
            }
            finally
            {
                listener.Stop();
            }
        }

        void OfflineAnnounceTest()
        {
            UdpTracker t = (UdpTracker)TrackerFactory.Create(new Uri("udp://127.0.0.1:57532/announce"));
            t.RetryDelay = TimeSpan.FromMilliseconds(500);
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e) {
                p = e;
                id.WaitHandle.Set();
            };
            MonoTorrent.Client.Tracker.AnnounceParameters pars = new AnnounceParameters();
            pars.InfoHash = new InfoHash(new byte[20]);
            pars.PeerId = "";

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#1");
            Assert.IsFalse(p.Successful);
        }

        void OfflineScrapeTest()
        {
            UdpTracker t = (UdpTracker)TrackerFactory.Create(new Uri("udp://127.0.0.1:57532/announce"));
            t.RetryDelay = TimeSpan.FromMilliseconds(500);
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            ScrapeResponseEventArgs p = null;
            t.ScrapeComplete += delegate(object o, ScrapeResponseEventArgs e)
            {
                if (e.Successful)
                    Console.ReadLine();
                p = e;
                id.WaitHandle.Set();
            };
            MonoTorrent.Client.Tracker.ScrapeParameters pars = new ScrapeParameters(new InfoHash(new byte[20]));

            t.Scrape(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#1");
            Assert.IsFalse(p.Successful);
        }

        [Test]
        public void ScrapeTest()
        {
            UdpTracker t = (UdpTracker)TrackerFactory.Create(new Uri(prefix));
            Assert.IsTrue(t.CanScrape, "#1");
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            ScrapeResponseEventArgs p = null;
            t.ScrapeComplete += delegate(object o, ScrapeResponseEventArgs e)
            {
                p = e;
                id.WaitHandle.Set();
            };
            MonoTorrent.Client.Tracker.ScrapeParameters pars = new ScrapeParameters(new InfoHash(new byte[20]));

            t.Scrape(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#2");
            Assert.IsTrue(p.Successful, "#3");
            Assert.AreEqual(0, t.Complete, "#1");
            Assert.AreEqual(0, t.Incomplete, "#2");
            Assert.AreEqual(0, t.Downloaded, "#3");
        }

        void Wait(WaitHandle handle)
        {
            Assert.IsTrue(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }
    }

    class IgnoringListener : MonoTorrent.Tracker.Listeners.UdpListener
    {
        public bool IgnoreConnects;
        public bool IgnoreAnnounces;
        public bool IgnoreErrors;
        public bool IgnoreScrapes;

        public IgnoringListener(int port)
            : base(port)
        {

        }

        protected override void ReceiveConnect(ConnectMessage connectMessage)
        {
            if (!IgnoreConnects)
                base.ReceiveConnect(connectMessage);
        }

        protected override void ReceiveAnnounce(AnnounceMessage announceMessage)
        {
            if (!IgnoreAnnounces)
                base.ReceiveAnnounce(announceMessage);
        }

        protected override void ReceiveError(ErrorMessage errorMessage)
        {
            if (!IgnoreErrors)
                base.ReceiveError(errorMessage);
        }

        protected override void ReceiveScrape(ScrapeMessage scrapeMessage)
        {
            if (!IgnoreScrapes)
                base.ReceiveScrape(scrapeMessage);
        }
    }
}
