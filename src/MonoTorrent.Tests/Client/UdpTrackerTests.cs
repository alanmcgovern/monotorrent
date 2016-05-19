using System;
using System.Collections.Generic;
using System.Threading;
using MonoTorrent.Client.Messages.UdpTracker;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Client
{
    public class UdpTrackerTests : IDisposable
    {
        public UdpTrackerTests()
        {
            keys = new List<string>();
            server = new MonoTorrent.Tracker.Tracker();
            server.AllowUnregisteredTorrents = true;
            listener = new MonoTorrent.Tracker.Listeners.UdpListener(6767);
            listener.AnnounceReceived +=
                delegate(object o, MonoTorrent.Tracker.AnnounceParameters e) { keys.Add(e.Key); };
            server.RegisterListener(listener);

            listener.Start();

            keys.Clear();
        }

        public void Dispose()
        {
            listener.Stop();
            server.Dispose();
        }

        private static void Main(string[] args)
        {
            var t = new UdpTrackerTests();
            t.ConnectMessageTest();
            t.ConnectResponseTest();
            t.AnnounceMessageTest();
            t.AnnounceResponseTest();
            t.ScrapeMessageTest();
            t.ScrapeResponseTest();

            t.AnnounceTest();
            t.ScrapeTest();

            t.Dispose();
        }

        private readonly AnnounceParameters announceparams = new AnnounceParameters(100, 50, int.MaxValue,
            TorrentEvent.Completed,
            new InfoHash(new byte[] {1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5}),
            false, new string('a', 20), null, 1515);

        private readonly MonoTorrent.Tracker.Tracker server;
        private readonly MonoTorrent.Tracker.Listeners.UdpListener listener;
        private readonly List<string> keys;
        private readonly string prefix = "udp://localhost:6767/announce/";

        private void Check(UdpTrackerMessage message, MessageType type)
        {
            var e = message.Encode();
            Assert.Equal(e.Length, message.ByteLength);
            Assert.True(Toolbox.ByteMatch(e, UdpTrackerMessage.DecodeMessage(e, 0, e.Length, type).Encode()));
        }

        private void OfflineAnnounceTest()
        {
            var t = (UdpTracker) TrackerFactory.Create(new Uri("udp://127.0.0.1:57532/announce"));
            t.RetryDelay = TimeSpan.FromMilliseconds(500);
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e)
            {
                p = e;
                id.WaitHandle.Set();
            };
            var pars = new AnnounceParameters();
            pars.InfoHash = new InfoHash(new byte[20]);
            pars.PeerId = "";

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.NotNull(p);
            Assert.False(p.Successful);
        }

        private void OfflineScrapeTest()
        {
            var t = (UdpTracker) TrackerFactory.Create(new Uri("udp://127.0.0.1:57532/announce"));
            t.RetryDelay = TimeSpan.FromMilliseconds(500);
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            ScrapeResponseEventArgs p = null;
            t.ScrapeComplete += delegate(object o, ScrapeResponseEventArgs e)
            {
                if (e.Successful)
                    Console.ReadLine();
                p = e;
                id.WaitHandle.Set();
            };
            var pars = new ScrapeParameters(new InfoHash(new byte[20]));

            t.Scrape(pars, id);
            Wait(id.WaitHandle);
            Assert.NotNull(p);
            Assert.False(p.Successful);
        }

        private void Wait(WaitHandle handle)
        {
            Assert.True(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }

        [Fact]
        public void AnnounceMessageTest()
        {
            var m = new AnnounceMessage(0, 12345, announceparams);
            var d =
                (AnnounceMessage) UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);

            Assert.Equal(1, m.Action);
            Assert.Equal(m.Action, d.Action);
            Assert.True(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.Equal(12345, d.ConnectionId);
        }

        [Fact]
        public void AnnounceResponseTest()
        {
            var peers = new List<Peer>();
            peers.Add(new Peer(new string('1', 20), new Uri("tcp://127.0.0.1:1")));
            peers.Add(new Peer(new string('2', 20), new Uri("tcp://127.0.0.1:2")));
            peers.Add(new Peer(new string('3', 20), new Uri("tcp://127.0.0.1:3")));

            var m = new AnnounceResponseMessage(12345, TimeSpan.FromSeconds(10), 43, 65, peers);
            var d =
                (AnnounceResponseMessage)
                    UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Response);
            Check(m, MessageType.Response);

            Assert.Equal(1, m.Action);
            Assert.Equal(m.Action, d.Action);
            Assert.True(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.Equal(12345, d.TransactionId);
        }

        [Fact]
        public void AnnounceTest()
        {
            var t = (UdpTracker) TrackerFactory.Create(new Uri(prefix));
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e)
            {
                p = e;
                id.WaitHandle.Set();
            };
            var pars = new AnnounceParameters();
            pars.InfoHash = new InfoHash(new byte[20]);
            pars.PeerId = "";

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.NotNull(p);
            Assert.True(p.Successful);
            //Assert.Equal(keys[0], t.Key);
        }

        [Fact]
        public void AnnounceTest_NoAnnounce()
        {
            var listener = new IgnoringListener(57532);
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

        [Fact]
        public void AnnounceTest_NoConnect()
        {
            var listener = new IgnoringListener(57532);
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

        [Fact]
        public void ConnectMessageTest()
        {
            var m = new ConnectMessage();
            var d =
                (ConnectMessage) UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);

            Assert.Equal(0, m.Action);
            Assert.Equal(m.Action, d.Action);
            Assert.Equal(m.ConnectionId, d.ConnectionId);
            Assert.Equal(m.TransactionId, d.TransactionId);
            Assert.True(Toolbox.ByteMatch(m.Encode(), d.Encode()));
        }

        [Fact]
        public void ConnectResponseTest()
        {
            var m = new ConnectResponseMessage(5371, 12345);
            var d =
                (ConnectResponseMessage)
                    UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Response);
            Check(m, MessageType.Response);

            Assert.Equal(0, m.Action);
            Assert.Equal(m.Action, d.Action);
            Assert.Equal(m.ConnectionId, d.ConnectionId);
            Assert.Equal(m.TransactionId, d.TransactionId);
            Assert.True(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.Equal(12345, d.ConnectionId);
            Assert.Equal(5371, d.TransactionId);
        }

        [Fact]
        public void ScrapeMessageTest()
        {
            var hashes = new List<byte[]>();
            var r = new Random();
            var hash1 = new byte[20];
            var hash2 = new byte[20];
            var hash3 = new byte[20];
            r.NextBytes(hash1);
            r.NextBytes(hash2);
            r.NextBytes(hash3);
            hashes.Add(hash1);
            hashes.Add(hash2);
            hashes.Add(hash3);

            var m = new ScrapeMessage(12345, 123, hashes);
            var d =
                (ScrapeMessage) UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);

            Assert.Equal(2, m.Action);
            Assert.Equal(m.Action, d.Action);
            Assert.True(Toolbox.ByteMatch(m.Encode(), d.Encode()));
        }

        [Fact]
        public void ScrapeResponseTest()
        {
            var details = new List<ScrapeDetails>();
            details.Add(new ScrapeDetails(1, 2, 3));
            details.Add(new ScrapeDetails(4, 5, 6));
            details.Add(new ScrapeDetails(7, 8, 9));

            var m = new ScrapeResponseMessage(12345, details);
            var d =
                (ScrapeResponseMessage)
                    UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Response);
            Check(m, MessageType.Response);

            Assert.Equal(2, m.Action);
            Assert.Equal(m.Action, d.Action);
            Assert.True(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.Equal(12345, d.TransactionId);
        }

        [Fact]
        public void ScrapeTest()
        {
            var t = (UdpTracker) TrackerFactory.Create(new Uri(prefix));
            Assert.True(t.CanScrape);
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            ScrapeResponseEventArgs p = null;
            t.ScrapeComplete += delegate(object o, ScrapeResponseEventArgs e)
            {
                p = e;
                id.WaitHandle.Set();
            };
            var pars = new ScrapeParameters(new InfoHash(new byte[20]));

            t.Scrape(pars, id);
            Wait(id.WaitHandle);
            Assert.NotNull(p);
            Assert.True(p.Successful);
            Assert.Equal(0, t.Complete);
            Assert.Equal(0, t.Incomplete);
            Assert.Equal(0, t.Downloaded);
        }

        [Fact]
        public void ScrapeTest_NoConnect()
        {
            var listener = new IgnoringListener(57532);
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

        [Fact]
        public void ScrapeTest_NoScrapes()
        {
            var listener = new IgnoringListener(57532);
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
    }

    internal class IgnoringListener : MonoTorrent.Tracker.Listeners.UdpListener
    {
        public bool IgnoreAnnounces;
        public bool IgnoreConnects;
        public bool IgnoreErrors = false;
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