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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages.UdpTracker;
using MonoTorrent.Tracker.Listeners;

using NUnit.Framework;

namespace MonoTorrent.Client.Tracker
{
    [TestFixture]
    public class UdpTrackerTests
    {
        static readonly BEncodedString PeerId = new BEncodedString (Enumerable.Repeat ((byte) 255, 20).ToArray ());
        static readonly InfoHash InfoHash = new InfoHash (Enumerable.Repeat ((byte) 254, 20).ToArray ());

        AnnounceParameters announceparams = new AnnounceParameters (100, 50, int.MaxValue,
            TorrentEvent.Completed, InfoHash, false, PeerId, null, 1515, false);

        readonly ScrapeParameters scrapeParams = new ScrapeParameters (new InfoHash (new byte[20]));
        MonoTorrent.Tracker.TrackerServer server;
        UdpTracker tracker;
        IgnoringListener listener;
        List<BEncodedString> keys;
        List<IPEndPoint> peerEndpoints;

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            listener = new IgnoringListener (0);
            listener.AnnounceReceived += delegate (object o, MonoTorrent.Tracker.AnnounceRequest e) {
                keys.Add (e.Key);
            };
            listener.Start ();
        }

        [SetUp]
        public void Setup ()
        {
            keys = new List<BEncodedString> ();
            server = new MonoTorrent.Tracker.TrackerServer ();
            server.AllowUnregisteredTorrents = true;
            server.RegisterListener (listener);

            peerEndpoints = new List<IPEndPoint> {
                new IPEndPoint (IPAddress.Parse ("123.123.123.123"), 12312),
                new IPEndPoint (IPAddress.Parse ("254.254.254.254"), 3522),
                new IPEndPoint (IPAddress.Parse ("1.1.1.1"), 123),
                new IPEndPoint (IPAddress.Parse ("1.2.3.4"), 65000),
            };

            tracker = (UdpTracker) TrackerFactory.Create (new Uri ($"udp://127.0.0.1:{listener.EndPoint.Port}/announce/"));
            announceparams = announceparams.WithPort (listener.EndPoint.Port);

            listener.IgnoreAnnounces = false;
            listener.IgnoreConnects = false;
            listener.IgnoreErrors = false;
            listener.IgnoreScrapes = false;
            listener.IncompleteAnnounce = listener.IncompleteConnect = listener.IncompleteScrape = false;
        }

        [TearDown]
        public void Teardown ()
        {
            server.UnregisterListener (listener);
        }

        [OneTimeTearDown]
        public void FixtureTeardown ()
        {
            listener.Stop ();
            server.Dispose ();
        }

        [Test]
        public void AnnounceMessageTest ()
        {
            AnnounceMessage m = new AnnounceMessage (0, 12345, announceparams);
            AnnounceMessage d = (AnnounceMessage) UdpTrackerMessage.DecodeMessage (m.Encode (), 0, m.ByteLength, MessageType.Request);
            Check (m, MessageType.Request);

            Assert.AreEqual (1, m.Action);
            Assert.AreEqual (m.Action, d.Action);
            Assert.IsTrue (Toolbox.ByteMatch (m.Encode (), d.Encode ()));
            Assert.AreEqual (12345, d.ConnectionId);
        }

        [Test]
        public void AnnounceResponseTest ()
        {
            var peers = peerEndpoints.Select (t => new Peer ("", new Uri ($"ipv4://{t.Address}:{t.Port}"))).ToList ();
            AnnounceResponseMessage m = new AnnounceResponseMessage (12345, TimeSpan.FromSeconds (10), 43, 65, peers);
            AnnounceResponseMessage d = (AnnounceResponseMessage) UdpTrackerMessage.DecodeMessage (m.Encode (), 0, m.ByteLength, MessageType.Response);
            Check (m, MessageType.Response);

            Assert.AreEqual (1, m.Action);
            Assert.AreEqual (m.Action, d.Action);
            Assert.IsTrue (Toolbox.ByteMatch (m.Encode (), d.Encode ()));
            Assert.AreEqual (12345, d.TransactionId);
        }

        [Test]
        public void ConnectMessageTest ()
        {
            ConnectMessage m = new ConnectMessage ();
            ConnectMessage d = (ConnectMessage) UdpTrackerMessage.DecodeMessage (m.Encode (), 0, m.ByteLength, MessageType.Request);
            Check (m, MessageType.Request);

            Assert.AreEqual (0, m.Action, "#0");
            Assert.AreEqual (m.Action, d.Action, "#1");
            Assert.AreEqual (m.ConnectionId, d.ConnectionId, "#2");
            Assert.AreEqual (m.TransactionId, d.TransactionId, "#3");
            Assert.IsTrue (Toolbox.ByteMatch (m.Encode (), d.Encode ()), "#4");
        }

        [Test]
        public void ConnectResponseTest ()
        {
            ConnectResponseMessage m = new ConnectResponseMessage (5371, 12345);
            ConnectResponseMessage d = (ConnectResponseMessage) UdpTrackerMessage.DecodeMessage (m.Encode (), 0, m.ByteLength, MessageType.Response);
            Check (m, MessageType.Response);

            Assert.AreEqual (0, m.Action, "#0");
            Assert.AreEqual (m.Action, d.Action, "#1");
            Assert.AreEqual (m.ConnectionId, d.ConnectionId, "#2");
            Assert.AreEqual (m.TransactionId, d.TransactionId, "#3");
            Assert.IsTrue (Toolbox.ByteMatch (m.Encode (), d.Encode ()), "#4");
            Assert.AreEqual (12345, d.ConnectionId);
            Assert.AreEqual (5371, d.TransactionId);

        }

        [Test]
        public void ScrapeMessageTest ()
        {
            List<byte[]> hashes = new List<byte[]> ();
            Random r = new Random ();
            byte[] hash1 = new byte[20];
            byte[] hash2 = new byte[20];
            byte[] hash3 = new byte[20];
            r.NextBytes (hash1);
            r.NextBytes (hash2);
            r.NextBytes (hash3);
            hashes.Add (hash1);
            hashes.Add (hash2);
            hashes.Add (hash3);

            ScrapeMessage m = new ScrapeMessage (12345, 123, hashes);
            ScrapeMessage d = (ScrapeMessage) UdpTrackerMessage.DecodeMessage (m.Encode (), 0, m.ByteLength, MessageType.Request);
            Check (m, MessageType.Request);

            Assert.AreEqual (2, m.Action);
            Assert.AreEqual (m.Action, d.Action);
            Assert.IsTrue (Toolbox.ByteMatch (m.Encode (), d.Encode ()));
        }

        [Test]
        public void ScrapeResponseTest ()
        {
            List<ScrapeDetails> details = new List<ScrapeDetails> {
                new ScrapeDetails (1, 2, 3),
                new ScrapeDetails (4, 5, 6),
                new ScrapeDetails (7, 8, 9)
            };

            ScrapeResponseMessage m = new ScrapeResponseMessage (12345, details);
            ScrapeResponseMessage d = (ScrapeResponseMessage) UdpTrackerMessage.DecodeMessage (m.Encode (), 0, m.ByteLength, MessageType.Response);
            Check (m, MessageType.Response);

            Assert.AreEqual (2, m.Action);
            Assert.AreEqual (m.Action, d.Action);
            Assert.IsTrue (Toolbox.ByteMatch (m.Encode (), d.Encode ()));
            Assert.AreEqual (12345, d.TransactionId);
        }

        void Check (UdpTrackerMessage message, MessageType type)
        {
            byte[] e = message.Encode ();
            Assert.AreEqual (e.Length, message.ByteLength, "#1");
            Assert.IsTrue (Toolbox.ByteMatch (e, UdpTrackerMessage.DecodeMessage (e, 0, e.Length, type).Encode ()), "#2");
        }

        [Test]
        public async Task AnnounceTest ()
        {
            announceparams = announceparams
                .WithBytesDownloaded (123)
                .WithBytesLeft (456)
                .WithBytesUploaded (789);

            var announceArgsTask = new TaskCompletionSource<MonoTorrent.Tracker.AnnounceEventArgs> ();
            server.PeerAnnounced += (o, e) => announceArgsTask.TrySetResult (e);
            await tracker.AnnounceAsync (announceparams);

            await announceArgsTask.Task;

            var args = announceArgsTask.Task.Result;
            Assert.AreEqual (PeerId, args.Peer.PeerId, "#1");
            Assert.AreEqual (123, args.Peer.Downloaded);
            Assert.AreEqual (456, args.Peer.Remaining);
            Assert.AreEqual (789, args.Peer.Uploaded);
        }

        [Test]
        public async Task AnnounceTest_GetPeers ()
        {
            var trackable = new MonoTorrent.Tracker.InfoHashTrackable ("Test", InfoHash);
            server.Add (trackable);
            var manager = (MonoTorrent.Tracker.SimpleTorrentManager) server.GetTrackerItem (trackable);
            foreach (var p in peerEndpoints)
                manager.Add (new MonoTorrent.Tracker.Peer (p, p));

            var peers = await tracker.AnnounceAsync (announceparams);
            var endpoints = peers.Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port)).ToArray ();
            foreach (var p in peerEndpoints) {
                Assert.IsTrue (endpoints.Contains (p), "#1." + p);
            }
        }

        [Test]
        public async Task AnnounceTest_IncompleteAnnounce ()
        {
            listener.IncompleteAnnounce = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));
            Assert.AreEqual (TrackerState.InvalidResponse, tracker.Status);

            listener.IncompleteAnnounce = false;
            await tracker.AnnounceAsync (announceparams);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
        }

        [Test]
        public async Task AnnounceTest_IncompleteConnect ()
        {
            listener.IncompleteConnect = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));
            Assert.AreEqual (TrackerState.InvalidResponse, tracker.Status);

            listener.IncompleteConnect = false;
            await tracker.AnnounceAsync (announceparams);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);

        }

        [Test]
        public void AnnounceTest_NoConnect ()
        {
            tracker.RetryDelay = TimeSpan.Zero;
            listener.IgnoreConnects = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
        }

        [Test]
        public async Task AnnounceTest_NoConnect_ThenConnect ()
        {
            tracker.RetryDelay = TimeSpan.Zero;
            listener.IgnoreConnects = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));

            tracker.RetryDelay = TimeSpan.FromSeconds (5);
            listener.IgnoreConnects = false;
            await tracker.AnnounceAsync (announceparams);
        }

        [Test]
        public void AnnounceTest_NoAnnounce ()
        {
            tracker.RetryDelay = TimeSpan.Zero;
            listener.IgnoreAnnounces = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
        }

        [Test]
        public async Task AnnounceTest_NoAnnounce_ThenAnnounce ()
        {
            tracker.RetryDelay = TimeSpan.Zero;
            listener.IgnoreAnnounces = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceparams));

            tracker.RetryDelay = TimeSpan.FromSeconds (5);
            listener.IgnoreAnnounces = false;
            await tracker.AnnounceAsync (announceparams);
        }

        [Test]
        public async Task ScrapeTest ()
        {
            await tracker.ScrapeAsync (scrapeParams);
            Assert.AreEqual (0, tracker.Complete, "#1");
            Assert.AreEqual (0, tracker.Incomplete, "#2");
            Assert.AreEqual (0, tracker.Downloaded, "#3");
        }

        [Test]
        public void ScrapeTest_NoConnect ()
        {
            tracker.RetryDelay = TimeSpan.Zero;
            listener.IgnoreConnects = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));
        }

        [Test]
        public async Task ScrapeTest_NoConnect_ThenConnect ()
        {
            tracker.RetryDelay = TimeSpan.Zero;
            listener.IgnoreConnects = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));

            tracker.RetryDelay = TimeSpan.FromSeconds (5);
            listener.IgnoreConnects = false;
            await tracker.ScrapeAsync (scrapeParams);
        }

        [Test]
        public async Task ScrapeTest_Incomplete ()
        {
            listener.IncompleteScrape = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));
            Assert.AreEqual (TrackerState.InvalidResponse, tracker.Status);

            listener.IncompleteScrape = false;
            await tracker.ScrapeAsync (scrapeParams);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
        }

        [Test]
        public void ScrapeTest_NoScrapes ()
        {
            tracker.RetryDelay = TimeSpan.Zero;
            listener.IgnoreScrapes = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
        }

        [Test]
        public async Task ScrapeTest_NoScrapes_ThenScrape ()
        {
            tracker.RetryDelay = TimeSpan.Zero;
            listener.IgnoreScrapes = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));

            tracker.RetryDelay = TimeSpan.FromSeconds (5);
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

        public bool IncompleteAnnounce { get; set; }
        public bool IncompleteConnect { get; set; }
        public bool IncompleteScrape { get; set; }

        public IgnoringListener (int port)
            : base (port)
        {

        }

        protected override async Task ReceiveConnect (UdpClient client, ConnectMessage connectMessage, IPEndPoint remotePeer)
        {
            if (IncompleteConnect) {
                await client.SendAsync (Enumerable.Repeat ((byte) 200, 50).ToArray (), 50, remotePeer);
                return;
            }
            if (!IgnoreConnects)
                await base.ReceiveConnect (client, connectMessage, remotePeer);
        }

        protected override async Task ReceiveAnnounce (UdpClient client, AnnounceMessage announceMessage, IPEndPoint remotePeer)
        {
            if (IncompleteAnnounce) {
                await client.SendAsync (Enumerable.Repeat ((byte) 200, 50).ToArray (), 50, remotePeer);
                return;
            }

            if (!IgnoreAnnounces)
                await base.ReceiveAnnounce (client, announceMessage, remotePeer);
        }

        protected override async Task ReceiveError (UdpClient client, ErrorMessage errorMessage, IPEndPoint remotePeer)
        {
            if (!IgnoreErrors)
                await base.ReceiveError (client, errorMessage, remotePeer);
        }

        protected override async Task ReceiveScrape (UdpClient client, ScrapeMessage scrapeMessage, IPEndPoint remotePeer)
        {
            if (IncompleteScrape) {
                await client.SendAsync (Enumerable.Repeat ((byte) 200, 50).ToArray (), 50, remotePeer);
                return;
            }
            if (!IgnoreScrapes)
                await base.ReceiveScrape (client, scrapeMessage, remotePeer);
        }
    }
}
