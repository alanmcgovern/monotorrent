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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Tracker;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.Messages.UdpTracker;
using MonoTorrent.TrackerServer;

using NUnit.Framework;

namespace MonoTorrent.Trackers
{
    [TestFixture]
    public class UdpTrackerTests
    {
        static readonly byte[] PeerId = Enumerable.Repeat ((byte) 255, 20).ToArray ();
        static readonly InfoHashes InfoHashes = InfoHashes.FromV1 (new InfoHash (Enumerable.Repeat ((byte) 254, 20).ToArray ()));

        AnnounceRequest announceparams = new AnnounceRequest (100, 50, int.MaxValue,
            TorrentEvent.Completed, InfoHashes, false, PeerId, t => (null, 1515), false);

        readonly ScrapeRequest scrapeParams = new ScrapeRequest (InfoHashes);
        TrackerServer.TrackerServer server;
        UdpTrackerConnection trackerConnection;
        Tracker tracker;
        IgnoringListener listener;
        List<BEncodedString> keys;
        List<IPEndPoint> peerEndpoints;

        List<InfoHash> announcedInfoHashes = new List<InfoHash> ();
        List<InfoHash> scrapedInfoHashes = new List<InfoHash> ();

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            listener = new IgnoringListener (new IPEndPoint (IPAddress.Any, 0));
            listener.AnnounceReceived += delegate (object o, MonoTorrent.TrackerServer.AnnounceRequest e) {
                keys.Add (e.Key);
                announcedInfoHashes.Add (e.InfoHash);
            };
            listener.ScrapeReceived += (o, e) => {
                scrapedInfoHashes.AddRange (e.InfoHashes);
            };
            listener.Start ();
        }

        [SetUp]
        public void Setup ()
        {
            announcedInfoHashes.Clear ();
            scrapedInfoHashes.Clear ();

            keys = new List<BEncodedString> ();
            server = new MonoTorrent.TrackerServer.TrackerServer ();
            server.AllowUnregisteredTorrents = true;
            server.RegisterListener (listener);

            peerEndpoints = new List<IPEndPoint> {
                new IPEndPoint (IPAddress.Parse ("123.123.123.123"), 12312),
                new IPEndPoint (IPAddress.Parse ("254.254.254.254"), 3522),
                new IPEndPoint (IPAddress.Parse ("1.1.1.1"), 123),
                new IPEndPoint (IPAddress.Parse ("1.2.3.4"), 65000),
            };

            trackerConnection = new UdpTrackerConnection (new Uri ($"udp://127.0.0.1:{listener.LocalEndPoint.Port}/announce/"), AddressFamily.InterNetwork);
            tracker = new Tracker (trackerConnection);
            
            Assert.AreEqual (listener.Status, ListenerStatus.Listening, "listener is listening");

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
            AnnounceMessage m = new AnnounceMessage (0, 12345, announceparams, announceparams.InfoHashes.V1OrV2, listener.LocalEndPoint.Port);
            AnnounceMessage d = (AnnounceMessage) UdpTrackerMessage.DecodeMessage (m.Encode ().Span, MessageType.Request, AddressFamily.InterNetwork);
            Check (m, MessageType.Request);

            Assert.AreEqual (1, m.Action);
            Assert.AreEqual (m.Action, d.Action);
            Assert.IsTrue (m.Encode ().Span.SequenceEqual (d.Encode ().Span));
            Assert.AreEqual (12345, d.ConnectionId);
        }

        [Test]
        public void AnnounceResponseTest ()
        {
            var peers = peerEndpoints.Select (t => new PeerInfo (new Uri ($"ipv4://{t.Address}:{t.Port}"))).ToList ();
            AnnounceResponseMessage m = new AnnounceResponseMessage (AddressFamily.InterNetwork, 12345, TimeSpan.FromSeconds (10), 43, 65, peers);
            AnnounceResponseMessage d = (AnnounceResponseMessage) UdpTrackerMessage.DecodeMessage (m.Encode ().Span, MessageType.Response, AddressFamily.InterNetwork);
            Check (m, MessageType.Response);

            Assert.AreEqual (1, m.Action);
            Assert.AreEqual (m.Action, d.Action);
            Assert.IsTrue (m.Encode ().Span.SequenceEqual (d.Encode ().Span));
            Assert.AreEqual (12345, d.TransactionId);
        }

        [Test]
        public void ConnectMessageTest ()
        {
            ConnectMessage m = new ConnectMessage ();
            ConnectMessage d = (ConnectMessage) UdpTrackerMessage.DecodeMessage (m.Encode ().Span, MessageType.Request, AddressFamily.InterNetwork);
            Check (m, MessageType.Request);

            Assert.AreEqual (0, m.Action, "#0");
            Assert.AreEqual (m.Action, d.Action, "#1");
            Assert.AreEqual (m.ConnectionId, d.ConnectionId, "#2");
            Assert.AreEqual (m.TransactionId, d.TransactionId, "#3");
            Assert.IsTrue (m.Encode ().Span.SequenceEqual (d.Encode ().Span), "#4");
        }

        [Test]
        public void ConnectResponseTest ()
        {
            ConnectResponseMessage m = new ConnectResponseMessage (5371, 12345);
            ConnectResponseMessage d = (ConnectResponseMessage) UdpTrackerMessage.DecodeMessage (m.Encode ().Span, MessageType.Response, AddressFamily.InterNetwork);
            Check (m, MessageType.Response);

            Assert.AreEqual (0, m.Action, "#0");
            Assert.AreEqual (m.Action, d.Action, "#1");
            Assert.AreEqual (m.ConnectionId, d.ConnectionId, "#2");
            Assert.AreEqual (m.TransactionId, d.TransactionId, "#3");
            Assert.IsTrue (m.Encode ().Span.SequenceEqual (d.Encode ().Span), "#4");
            Assert.AreEqual (12345, d.ConnectionId);
            Assert.AreEqual (5371, d.TransactionId);

        }

        [Test]
        public void ScrapeMessageTest ()
        {
            List<InfoHash> hashes = new List<InfoHash> ();
            Random r = new Random ();
            byte[] hash1 = new byte[20];
            byte[] hash2 = new byte[20];
            byte[] hash3 = new byte[20];
            r.NextBytes (hash1);
            r.NextBytes (hash2);
            r.NextBytes (hash3);
            hashes.Add (InfoHash.FromMemory (hash1));
            hashes.Add (InfoHash.FromMemory (hash2));
            hashes.Add (InfoHash.FromMemory (hash3));

            ScrapeMessage m = new ScrapeMessage (12345, 123, hashes);
            ScrapeMessage d = (ScrapeMessage) UdpTrackerMessage.DecodeMessage (m.Encode ().Span, MessageType.Request, AddressFamily.InterNetwork);
            Check (m, MessageType.Request);

            Assert.AreEqual (2, m.Action);
            Assert.AreEqual (m.Action, d.Action);
            Assert.IsTrue (m.Encode ().Span.SequenceEqual (d.Encode ().Span));
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
            ScrapeResponseMessage d = (ScrapeResponseMessage) UdpTrackerMessage.DecodeMessage (m.Encode ().Span, MessageType.Response, AddressFamily.InterNetwork);
            Check (m, MessageType.Response);

            Assert.AreEqual (2, m.Action);
            Assert.AreEqual (m.Action, d.Action);
            Assert.IsTrue (m.Encode ().Span.SequenceEqual (d.Encode ().Span));
            Assert.AreEqual (12345, d.TransactionId);
        }

        void Check (UdpTrackerMessage message, MessageType type)
        {
            ReadOnlyMemory<byte> e = message.Encode ();
            Assert.AreEqual (e.Length, message.ByteLength, "#1");
            Assert.IsTrue (e.Span.SequenceEqual (UdpTrackerMessage.DecodeMessage (e.Span, type, AddressFamily.InterNetwork).Encode ().Span), "#2");
        }

        [Test]
        public async Task AnnounceTest ()
        {
            announceparams = announceparams
                .WithBytesDownloaded (123)
                .WithBytesLeft (456)
                .WithBytesUploaded (789);

            var announceArgsTask = new TaskCompletionSource<AnnounceEventArgs> ();
            server.PeerAnnounced += (o, e) => announceArgsTask.TrySetResult (e);
            await tracker.AnnounceAsync (announceparams, CancellationToken.None);

            await announceArgsTask.Task;

            var args = announceArgsTask.Task.Result;
            Assert.AreEqual ((BEncodedString) PeerId, args.Peer.PeerId, "#1");
            Assert.AreEqual (123, args.Peer.Downloaded);
            Assert.AreEqual (456, args.Peer.Remaining);
            Assert.AreEqual (789, args.Peer.Uploaded);
        }

        [Test]
        public async Task AnnounceHybrid ()
        {
            var hybrid = new InfoHashes (new InfoHash (Enumerable.Repeat<byte> (1, 20).ToArray ()), new InfoHash (Enumerable.Repeat<byte> (2, 32).ToArray ()));
            await tracker.AnnounceAsync (announceparams.WithInfoHashes (hybrid), CancellationToken.None);
            Assert.AreEqual (2, announcedInfoHashes.Count);
            Assert.IsTrue (announcedInfoHashes.Contains (hybrid.V1));
            Assert.IsTrue (announcedInfoHashes.Contains (hybrid.V2.Truncate ()));
        }

        [Test]
        public async Task AnnounceV1 ()
        {
            var v1 = new InfoHashes(new InfoHash (Enumerable.Repeat<byte>(1, 20).ToArray ()), null);
            await tracker.AnnounceAsync (announceparams.WithInfoHashes (v1), CancellationToken.None);
            Assert.AreEqual (1, announcedInfoHashes.Count);
            Assert.AreEqual (v1.V1, announcedInfoHashes[0]);
        }

        [Test]
        public async Task AnnounceV2 ()
        {
            var v2 = new InfoHashes (null, new InfoHash (Enumerable.Repeat<byte> (2, 32).ToArray ()));
            await tracker.AnnounceAsync (announceparams.WithInfoHashes (v2), CancellationToken.None);
            Assert.AreEqual (1, announcedInfoHashes.Count);
            Assert.AreEqual (v2.V2.Truncate (), announcedInfoHashes[0]);
        }

        [Test]
        public async Task AnnounceTest_GetPeers ()
        {
            var trackable = new InfoHashTrackable ("Test", InfoHashes.V1OrV2);
            server.Add (trackable);
            var manager = (SimpleTorrentManager) server.GetTrackerItem (trackable);
            foreach (var p in peerEndpoints)
                manager.Add (new Peer (p, p));

            var response = await tracker.AnnounceAsync (announceparams, CancellationToken.None);
            var endpoints = response.Peers.Values.SelectMany (t => t).Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port)).ToArray ();
            foreach (var p in peerEndpoints) {
                Assert.IsTrue (endpoints.Contains (p), "#1." + p);
            }
        }

        [Test]
        public async Task AnnounceTest_IncompleteAnnounce ()
        {
            listener.IncompleteAnnounce = true;
            var response = await tracker.AnnounceAsync (announceparams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.InvalidResponse, tracker.Status);
            Assert.AreEqual (TrackerState.InvalidResponse, response.State);

            listener.IncompleteAnnounce = false;
            response = await tracker.AnnounceAsync (announceparams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
            Assert.AreEqual (TrackerState.Ok, response.State);
        }

        [Test]
        public async Task AnnounceTest_IncompleteConnect ()
        {
            listener.IncompleteConnect = true;
            var response = await tracker.AnnounceAsync (announceparams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.InvalidResponse, tracker.Status);
            Assert.AreEqual (TrackerState.InvalidResponse, response.State);

            listener.IncompleteConnect = false;
            response = await tracker.AnnounceAsync (announceparams, CancellationToken.None);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
            Assert.AreEqual (TrackerState.Ok, response.State);
        }

        [Test]
        public async Task AnnounceTest_NoConnect ()
        {
            trackerConnection.RetryDelay = TimeSpan.Zero;
            listener.IgnoreConnects = true;
            var response = await tracker.AnnounceAsync (announceparams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
            Assert.AreEqual (TrackerState.Offline, response.State);
        }

        [Test]
        public async Task AnnounceTest_NoConnect_ThenConnect ()
        {
            Assert.AreEqual (listener.Status, ListenerStatus.Listening, "listener is listening");

            trackerConnection.RetryDelay = TimeSpan.FromSeconds(0);
            listener.IgnoreConnects = true;
            var response = await tracker.AnnounceAsync (announceparams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.Offline, tracker.Status, "tracker status should be offline");
            Assert.AreEqual (TrackerState.Offline, response.State, "response status shoudl be offline");

            trackerConnection.RetryDelay = TimeSpan.FromSeconds (5);
            listener.IgnoreConnects = false;
            response = await tracker.AnnounceAsync (announceparams, CancellationToken.None);
            Assert.AreEqual (TrackerState.Ok, tracker.Status, "tracker status should be ok");
            Assert.AreEqual (TrackerState.Ok, response.State, "response status should be ok");
        }

        [Test]
        public async Task AnnounceTest_NoAnnounce ()
        {
            trackerConnection.RetryDelay = TimeSpan.Zero;
            listener.IgnoreAnnounces = true;
            var response = await tracker.AnnounceAsync (announceparams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
            Assert.AreEqual (TrackerState.Offline, response.State);
        }

        [Test]
        public async Task AnnounceTest_NoAnnounce_ThenAnnounce ()
        {
            trackerConnection.RetryDelay = TimeSpan.Zero;
            listener.IgnoreAnnounces = true;
            var response = await tracker.AnnounceAsync (announceparams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
            Assert.AreEqual (TrackerState.Offline, response.State);

            trackerConnection.RetryDelay = TimeSpan.FromSeconds (5);
            listener.IgnoreAnnounces = false;
            response = await tracker.AnnounceAsync (announceparams, CancellationToken.None);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
            Assert.AreEqual (TrackerState.Ok, response.State);
        }

        [Test]
        public async Task ScrapeTest ()
        {
            await tracker.ScrapeAsync (scrapeParams, CancellationToken.None);
            Assert.AreEqual (0, tracker.ScrapeInfo.Count, "#1");
        }

        [Test]
        public async Task ScrapeHybrid ()
        {
            var hybrid = new InfoHashes (new InfoHash (Enumerable.Repeat<byte> (1, 20).ToArray ()), new InfoHash (Enumerable.Repeat<byte> (2, 32).ToArray ()));
            await tracker.ScrapeAsync (new ScrapeRequest (hybrid), CancellationToken.None);
            Assert.AreEqual (2, scrapedInfoHashes.Count);
            Assert.IsTrue (scrapedInfoHashes.Contains (hybrid.V1));
            Assert.IsTrue (scrapedInfoHashes.Contains (hybrid.V2.Truncate ()));
        }

        [Test]
        public async Task ScrapeV1 ()
        {
            var v1 = new InfoHashes (new InfoHash (Enumerable.Repeat<byte> (1, 20).ToArray ()), null);
            await tracker.ScrapeAsync (new ScrapeRequest (v1), CancellationToken.None);
            Assert.AreEqual (1, scrapedInfoHashes.Count);
            Assert.AreEqual (v1.V1, scrapedInfoHashes[0]);
        }

        [Test]
        public async Task ScrapeV2 ()
        {
            var v2 = new InfoHashes (null, new InfoHash (Enumerable.Repeat<byte> (2, 32).ToArray ()));
            await tracker.ScrapeAsync (new ScrapeRequest (v2), CancellationToken.None);
            Assert.AreEqual (1, scrapedInfoHashes.Count);
            Assert.AreEqual (v2.V2.Truncate (), scrapedInfoHashes[0]);
        }

        [Test]
        public async Task ScrapeTest_NoConnect ()
        {
            trackerConnection.RetryDelay = TimeSpan.Zero;
            listener.IgnoreConnects = true;
            var response = await tracker.ScrapeAsync (scrapeParams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
            Assert.AreEqual (TrackerState.Offline, response.State);
        }

        [Test]
        public async Task ScrapeTest_NoConnect_ThenConnect ()
        {
            trackerConnection.RetryDelay = TimeSpan.Zero;
            listener.IgnoreConnects = true;
            var response = await tracker.ScrapeAsync (scrapeParams, CancellationToken.None).AsTask ();
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
            Assert.AreEqual (TrackerState.Offline, response.State);

            trackerConnection.RetryDelay = TimeSpan.FromSeconds (5);
            listener.IgnoreConnects = false;
            response = await tracker.ScrapeAsync (scrapeParams, CancellationToken.None);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
            Assert.AreEqual (TrackerState.Ok, response.State);
        }

        [Test]
        public async Task ScrapeTest_Incomplete ()
        {
            listener.IncompleteScrape = true;
            var response = await tracker.ScrapeAsync (scrapeParams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.InvalidResponse, tracker.Status);
            Assert.AreEqual (TrackerState.InvalidResponse, response.State);

            listener.IncompleteScrape = false;
            response = await tracker.ScrapeAsync (scrapeParams, CancellationToken.None);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
            Assert.AreEqual (TrackerState.Ok, response.State);
        }

        [Test]
        public async Task ScrapeTest_NoScrapes ()
        {
            trackerConnection.RetryDelay = TimeSpan.Zero;
            listener.IgnoreScrapes = true;
            var response = await tracker.ScrapeAsync (scrapeParams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
            Assert.AreEqual (TrackerState.Offline, response.State);
        }

        [Test]
        public async Task ScrapeTest_NoScrapes_ThenScrape ()
        {
            trackerConnection.RetryDelay = TimeSpan.Zero;
            listener.IgnoreScrapes = true;
            var response = await tracker.ScrapeAsync (scrapeParams, CancellationToken.None).WithTimeout ();
            Assert.AreEqual (TrackerState.Offline, tracker.Status, "tracker should be offline");
            Assert.AreEqual (TrackerState.Offline, response.State, "response should be offline");

            trackerConnection.RetryDelay = TimeSpan.FromSeconds (5);
            listener.IgnoreScrapes = false;
            response = await tracker.ScrapeAsync (scrapeParams, CancellationToken.None);
            Assert.AreEqual (TrackerState.Ok, tracker.Status, "tracker should be ok");
            Assert.AreEqual (TrackerState.Ok, response.State, "response should be ok");

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

        public IgnoringListener (IPEndPoint endpoint)
            : base (endpoint)
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
