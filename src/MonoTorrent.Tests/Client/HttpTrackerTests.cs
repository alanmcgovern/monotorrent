//
// HttpTrackerTests.cs
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
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Tracker;
using MonoTorrent.Tracker.Listeners;

using NUnit.Framework;

namespace MonoTorrent.Client.Tracker
{
    [TestFixture]
    public class HttpTrackerTests
    {
        AnnounceParameters announceParams;
        ScrapeParameters scrapeParams;
        TrackerServer server;
        HttpTrackerListener listener;
        string ListeningPrefix => "http://127.0.0.1:47124/";
        Uri AnnounceUrl => new Uri ($"{ListeningPrefix}announce");
        HTTPTracker tracker;

        InfoHash infoHash;
        BEncodedString peerId;
        BEncodedString trackerId;

        readonly List<BEncodedString> keys = new List<BEncodedString> ();

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            peerId = Enumerable.Repeat ((byte) 254, 20).ToArray ();
            trackerId = Enumerable.Repeat ((byte) 255, 20).ToArray ();
            listener = new HttpTrackerListener (ListeningPrefix);
            listener.AnnounceReceived += delegate (object o, AnnounceRequest e) {
                keys.Add (e.Key);
            };

            listener.Start ();
        }

        [SetUp]
        public void Setup ()
        {
            keys.Clear ();

            listener.IncompleteAnnounce = listener.IncompleteScrape = false;

            server = new TrackerServer (trackerId) {
                AllowUnregisteredTorrents = true
            };
            server.RegisterListener (listener);

            tracker = (HTTPTracker) TrackerFactory.Create (AnnounceUrl);

            var infoHashBytes = new[] { ' ', '%', '&', '?', '&', '&', '?', '5', '1', '=' }
                        .Select (t => (byte) t);

            infoHash = new InfoHash (infoHashBytes.Concat (infoHashBytes).ToArray ());
            announceParams = new AnnounceParameters ()
                .WithPort (5555)
                .WithPeerId (peerId)
                .WithInfoHash (infoHash);

            scrapeParams = new ScrapeParameters (new InfoHash (new byte[20]));
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
        public void CanAnnouceOrScrapeTest ()
        {
            HTTPTracker t = (HTTPTracker) TrackerFactory.Create (new Uri ("http://mytracker.com/myurl"));
            Assert.IsFalse (t.CanScrape, "#1");
            Assert.IsTrue (t.CanAnnounce, "#1b");

            t = (HTTPTracker) TrackerFactory.Create (new Uri ("http://mytracker.com/announce/yeah"));
            Assert.IsFalse (t.CanScrape, "#2");
            Assert.IsTrue (t.CanAnnounce, "#2b");

            t = (HTTPTracker) TrackerFactory.Create (new Uri ("http://mytracker.com/announce"));
            Assert.IsTrue (t.CanScrape, "#3");
            Assert.IsTrue (t.CanAnnounce, "#3b");
            Assert.AreEqual (t.ScrapeUri, new Uri ("http://mytracker.com/scrape"));

            t = (HTTPTracker) TrackerFactory.Create (new Uri ("http://mytracker.com/announce/yeah/announce"));
            Assert.IsTrue (t.CanScrape, "#4");
            Assert.IsTrue (t.CanAnnounce, "#4b");
            Assert.AreEqual ("http://mytracker.com/announce/yeah/scrape", t.ScrapeUri.ToString (), "#4c");

            t = (HTTPTracker) TrackerFactory.Create (new Uri ("http://mytracker.com/announce/"));
            Assert.IsTrue (t.CanScrape, "#5");
            Assert.IsTrue (t.CanAnnounce, "#5b");
            Assert.AreEqual (t.ScrapeUri, new Uri ("http://mytracker.com/scrape/"));
        }

        [Test]
        public async Task Announce ()
        {
            await tracker.AnnounceAsync (announceParams);
            Assert.IsTrue (StringComparer.OrdinalIgnoreCase.Equals (keys[0], tracker.Key), "#2");
        }

        [Test]
        public async Task Announce_ValidateParams ()
        {
            var argsTask = new TaskCompletionSource<AnnounceRequest> ();
            listener.AnnounceReceived += (o, e) => argsTask.TrySetResult (e);

            await tracker.AnnounceAsync (announceParams);
            Assert.IsTrue (argsTask.Task.Wait (5000), "#1");

            var args = argsTask.Task.Result;
            Assert.AreEqual (peerId, announceParams.PeerId, "#1");
            Assert.AreEqual (announceParams.PeerId, args.PeerId, "#2");

            Assert.AreEqual (infoHash, args.InfoHash, "#3");
            Assert.AreEqual (announceParams.InfoHash, args.InfoHash, "#3");
        }

        [Test]
        public async Task Announce_Incomplete ()
        {
            listener.IncompleteAnnounce = true;
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceParams));
            Assert.AreEqual (TrackerState.InvalidResponse, tracker.Status);

            listener.IncompleteAnnounce = false;
            await tracker.AnnounceAsync (announceParams);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
        }

        [Test]
        public void Announce_Timeout ()
        {
            TaskCompletionSource<bool> s = new TaskCompletionSource<bool> ();
            listener.AnnounceReceived += (o, e) => s.Task.Wait ();
            tracker.RequestTimeout = TimeSpan.FromMilliseconds (0);
            try {
                Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceParams).WithTimeout ());
            } finally {
                s.SetResult (true);
            }
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
        }

        [Test]
        public async Task KeyTest ()
        {
            // Set a key which uses characters which need escaping.
            tracker = (HTTPTracker) TrackerFactory.Create (AnnounceUrl);
            tracker.Key = peerId;

            await tracker.AnnounceAsync (announceParams);
            Assert.AreEqual (peerId, keys[0], "#1");
        }

        [Test]
        public async Task NullKeyTest ()
        {
            // Set a key which uses characters which need escaping.
            tracker = (HTTPTracker) TrackerFactory.Create (AnnounceUrl);
            tracker.Key = null;

            await tracker.AnnounceAsync (announceParams);
            Assert.AreEqual (null, keys[0], "#1");
        }


        [Test]
        public async Task Scrape ()
        {
            // make sure it's a unique infohash as the listener isn't re-created for every test.
            infoHash = new InfoHash (Enumerable.Repeat ((byte) 1, 20).ToArray ());
            var trackable = new InfoHashTrackable ("Test", infoHash);
            server.Add (trackable);
            scrapeParams = new ScrapeParameters (infoHash);

            await tracker.ScrapeAsync (scrapeParams);
            Assert.AreEqual (0, tracker.Complete, "#1");
            Assert.AreEqual (0, tracker.Incomplete, "#2");
            Assert.AreEqual (0, tracker.Downloaded, "#3");

            await tracker.AnnounceAsync (new AnnounceParameters (0, 0, 100, TorrentEvent.Started, infoHash, false, "peer1", null, 1, false));
            await tracker.ScrapeAsync (scrapeParams);
            Assert.AreEqual (0, tracker.Complete, "#4");
            Assert.AreEqual (1, tracker.Incomplete, "#5");
            Assert.AreEqual (0, tracker.Downloaded, "#6");

            await tracker.AnnounceAsync (new AnnounceParameters (0, 0, 0, TorrentEvent.Started, infoHash, false, "peer2", null, 2, false));
            await tracker.ScrapeAsync (scrapeParams);
            Assert.AreEqual (1, tracker.Complete, "#7");
            Assert.AreEqual (1, tracker.Incomplete, "#8");
            Assert.AreEqual (0, tracker.Downloaded, "#9");

            await tracker.AnnounceAsync (new AnnounceParameters (0, 0, 0, TorrentEvent.Completed, infoHash, false, "peer3", null, 3, false));
            await tracker.ScrapeAsync (scrapeParams);
            Assert.AreEqual (2, tracker.Complete, "#10");
            Assert.AreEqual (1, tracker.Incomplete, "#11");
            Assert.AreEqual (1, tracker.Downloaded, "#12");
        }

        [Test]
        public async Task Scrape_Incomplete ()
        {
            listener.IncompleteScrape = true;
            tracker.RequestTimeout = TimeSpan.FromHours (1);
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams));
            Assert.AreEqual (TrackerState.InvalidResponse, tracker.Status);

            listener.IncompleteScrape = false;
            await tracker.ScrapeAsync (scrapeParams);
            Assert.AreEqual (TrackerState.Ok, tracker.Status);
        }

        [Test]
        public void Scrape_Timeout ()
        {
            var tcs = new TaskCompletionSource<bool> ();
            listener.ScrapeReceived += (o, e) => tcs.Task.Wait ();
            tracker.RequestTimeout = TimeSpan.FromMilliseconds (0);
            try {
                Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync (scrapeParams).WithTimeout ());
            } finally {
                tcs.SetResult (true);
            }
            Assert.AreEqual (TrackerState.Offline, tracker.Status);
        }

        [Test]
        public async Task TrackerId ()
        {
            // Null until the server side tracker sends us the value
            Assert.IsNull (tracker.TrackerId, "#1");
            await tracker.AnnounceAsync (announceParams);

            // Now we have the value, the next announce should contain it
            Assert.AreEqual (trackerId, tracker.TrackerId, "#2");

            var argsTask = new TaskCompletionSource<AnnounceRequest> ();
            listener.AnnounceReceived += (o, e) => argsTask.TrySetResult (e);

            await tracker.AnnounceAsync (announceParams);
            var result = await argsTask.Task.WithTimeout ("#3");
            Assert.AreEqual (trackerId, result.TrackerId, "#4");
        }
    }
}
