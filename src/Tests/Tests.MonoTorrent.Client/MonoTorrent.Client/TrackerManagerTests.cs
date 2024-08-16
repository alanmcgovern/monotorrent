//
// TrackerManagerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Connections.Tracker;
using MonoTorrent.Trackers;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.Client
{
    class CustomTracker : Tracker
    {
        public CustomTrackerConnection Connection { get; }

        public CustomTracker (CustomTrackerConnection connection)
            : base (connection)
        {
            Connection = connection;
        }
    }

    class RateLimitingTracker : Tracker
    {
        public RateLimitingTrackerConnection Connection { get; }

        public RateLimitingTracker (RateLimitingTrackerConnection connection)
            : base (connection)
        {
            Connection = (RateLimitingTrackerConnection) connection;
        }
    }

    class RateLimitingTrackerConnection : ITrackerConnection
    {
        public bool CanScrape => true;

        public Uri Uri => new Uri ("http://tracker:5353/announce");

        public List<TaskCompletionSource<TrackerState>> PendingAnnounces { get; } = new List<TaskCompletionSource<TrackerState>> ();
        public List<TaskCompletionSource<TrackerState>> PendingScrapes { get; } = new List<TaskCompletionSource<TrackerState>> ();

        public RateLimitingTrackerConnection ()
        {

        }

        public async ReusableTask<AnnounceResponse> AnnounceAsync (AnnounceRequest parameters, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<TrackerState> ();
            PendingAnnounces.Add (tcs);
            return new AnnounceResponse (await tcs.Task);
        }

        public async ReusableTask<ScrapeResponse> ScrapeAsync (ScrapeRequest parameters, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<TrackerState> ();
            PendingScrapes.Add (tcs);
            return new ScrapeResponse (await tcs.Task);
        }
    }

    [TestFixture]
    public class TrackerManagerTests
    {
        class RequestFactory : ITrackerRequestFactory
        {
            public readonly InfoHashes InfoHashes = InfoHashes.FromV1 (new InfoHash (new byte[20]));

            public AnnounceRequest CreateAnnounce (TorrentEvent clientEvent)
            {
                return new AnnounceRequest (InfoHashes)
                    .WithClientEvent (clientEvent);
            }

            public ScrapeRequest CreateScrape ()
            {
                return new ScrapeRequest (InfoHashes);
            }
        }

        static readonly IList<IList<string>> trackerUrls = new[] {
            new [] {
                "custom://tracker0.com/announce",
                "custom://tracker1.com/announce",
                "custom://tracker2.com/announce",
                "custom://tracker3.com/announce"
            },
            new [] {
                "custom://tracker4.com/announce",
                "custom://tracker5.com/announce",
                "custom://tracker6.com/announce",
                "custom://tracker7.com/announce"
            }
        };

        TrackerManager trackerManager;
        IList<List<CustomTracker>> trackers;

        Factories Factories { get; set; }

        [SetUp]
        public void Setup ()
        {
            Factories = Factories.Default
                .WithTrackerCreator ("custom", uri => new CustomTracker (new CustomTrackerConnection (uri)));
            trackerManager = new TrackerManager (Factories, new RequestFactory (), trackerUrls, true);
            trackers = trackerManager.Tiers.Select (t => t.Trackers.Cast<CustomTracker> ().ToList ()).ToList ();
        }

        [Test]
        public async Task Announce_EmitEvent ()
        {
            foreach (var tier in trackers)
                foreach (var tracker in tier)
                    tracker.Connection.AddPeer (new Peer (new PeerInfo (new Uri ("ipv4://127.123.123.123:12312"), "peerid")));

            var tcs = new TaskCompletionSource<AnnounceResponseEventArgs> ();
            trackerManager.AnnounceComplete += (o, e) => tcs.TrySetResult (e);

            await trackerManager.AnnounceAsync (CancellationToken.None).WithTimeout ();

            var result = await tcs.Task.WithTimeout ();
            Assert.AreEqual (1, result.Peers.Count);
        }

        [Test]
        public async Task Announce_NoEvent_SkipSecond ()
        {
            await trackerManager.AnnounceAsync (CancellationToken.None);
            Assert.AreEqual (1, trackers[0][0].Connection.AnnouncedAt.Count, "#2");
            Assert.That ((DateTime.Now - trackers[0][0].Connection.AnnouncedAt[0]) < TimeSpan.FromSeconds (1), "#3");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual (0, trackers[0][i].Connection.AnnouncedAt.Count, "#4." + i);

            await trackerManager.AnnounceAsync (trackers[0][1], CancellationToken.None);
            Assert.AreEqual (1, trackers[0][1].Connection.AnnouncedAt.Count, "#6");
            Assert.That ((DateTime.Now - trackers[0][1].Connection.AnnouncedAt[0]) < TimeSpan.FromSeconds (1), "#7");
        }

        [Test]
        public async Task Announce_SpecialEvent_DoNotSkipSecond (
            [Values (TorrentEvent.Started, TorrentEvent.Stopped, TorrentEvent.Completed)]
            TorrentEvent clientEvent)
        {
            await trackerManager.AnnounceAsync (clientEvent, CancellationToken.None);
            Assert.AreEqual (1, trackers[0][0].Connection.AnnouncedAt.Count, "#1a");
            Assert.AreEqual (1, trackers[1][0].Connection.AnnouncedAt.Count, "#1b");

            await trackerManager.AnnounceAsync (clientEvent, CancellationToken.None);
            Assert.AreEqual (2, trackers[0][0].Connection.AnnouncedAt.Count, "#2a");
            Assert.AreEqual (2, trackers[1][0].Connection.AnnouncedAt.Count, "#2b");

            for (int i = 1; i < trackers[0].Count; i++)
                Assert.AreEqual (0, trackers[0][i].Connection.AnnouncedAt.Count, "#3." + i);
            for (int i = 1; i < trackers[1].Count; i++)
                Assert.AreEqual (0, trackers[1][i].Connection.AnnouncedAt.Count, "#4." + i);
        }

        [Test]
        public async Task AnnounceAllFailed ()
        {
            var argsTask = new TaskCompletionSource<AnnounceResponseEventArgs> ();
            trackerManager.AnnounceComplete += (o, e) => argsTask.TrySetResult (e);

            foreach (var tier in trackers)
                foreach (var tracker in tier)
                    tracker.Connection.FailAnnounce = true;

            await trackerManager.AnnounceAsync (CancellationToken.None);

            var args = await argsTask.Task.WithTimeout ("The announce never completed");
            foreach (var tier in trackers)
                foreach (var tracker in tier)
                    Assert.AreEqual (1, tracker.Connection.AnnouncedAt.Count, "#1." + tracker.Uri);

            Assert.IsNotNull (args.Tracker, "#2");
            Assert.IsFalse (args.Successful, "#3");
        }

        [Test]
        public async Task AnnounceFailed ()
        {
            var tcs = new TaskCompletionSource<object> ();
            var announces = new List<AnnounceResponseEventArgs> ();
            trackerManager.AnnounceComplete += (o, e) => {
                lock (announces) {
                    announces.Add (e);
                    if (announces.Count == 4)
                        tcs.SetResult (null);
                }
            };
            trackers[0][0].Connection.FailAnnounce = true;
            trackers[0][1].Connection.FailAnnounce = true;

            await trackerManager.AnnounceAsync (CancellationToken.None).WithTimeout ();
            await tcs.Task.WithTimeout ();
            Assert.AreEqual (4, announces.Count);

            Assert.AreEqual (trackerManager.Tiers[0].Trackers[2], trackerManager.Tiers[0].ActiveTracker, "#1");
            Assert.AreEqual (1, trackers[0][0].Connection.AnnouncedAt.Count, "#2a");
            Assert.IsFalse (announces.Single (args => args.Tracker == trackers[0][0]).Successful, "#2b");

            Assert.AreEqual (1, trackers[0][1].Connection.AnnouncedAt.Count, "#3a");
            Assert.IsFalse (announces.Single (args => args.Tracker == trackers[0][1]).Successful, "#3b");

            Assert.AreEqual (1, trackers[0][2].Connection.AnnouncedAt.Count, "#4a");
            Assert.IsTrue (announces.Single (args => args.Tracker == trackers[0][2]).Successful, "#4b");

            Assert.AreEqual (0, trackers[0][3].Connection.AnnouncedAt.Count, "#5");

            Assert.AreEqual (trackerManager.Tiers[1].Trackers[0], trackerManager.Tiers[1].ActiveTracker, "#6");
            Assert.AreEqual (1, trackers[1][0].Connection.AnnouncedAt.Count, "#7a");
            Assert.IsTrue (announces.Single (args => args.Tracker == trackers[1][0]).Successful, "#7b");

            Assert.AreEqual (0, trackers[1][1].Connection.AnnouncedAt.Count, "#8");
        }

        [Test]
        public async Task Announce_RateLimitedAnnounceAttempts ()
        {
            var factories = Factories.Default
                .WithTrackerCreator ("custom", uri => new RateLimitingTracker (new RateLimitingTrackerConnection ()));

            var tier = new[] { new[] { $"custom://tracker/announce" } };
            var trackerManager = new TrackerManager (factories, new RequestFactory (), tier, true);
            var trackers = trackerManager.Tiers.Select (t => t.Trackers.Cast<RateLimitingTracker> ().ToList ()).ToList ();

            // only 1 concurrent regular announce can run at a time. 
            var announce = trackerManager.AnnounceAsync (CancellationToken.None);

            // These should all early-exit
            for (int i = 0; i < 3; i++)
                await trackerManager.AnnounceAsync (CancellationToken.None).WithTimeout (TimeSpan.FromSeconds (10));
            for (int i = 0; i < 3; i++)
                await trackerManager.AnnounceAsync (TorrentEvent.None, CancellationToken.None).WithTimeout (TimeSpan.FromSeconds (10));

            Assert.IsFalse (announce.IsCompleted);
        }

        [Test]
        public void Announce_RateLimitedTierAnnounces ()
        {
            var factories = Factories.Default
                .WithTrackerCreator ("custom", uri => new RateLimitingTracker (new RateLimitingTrackerConnection ()));

            // Create 100 tracker tiers.
            var urls = Enumerable.Range (0, 100).Select (t => new[] { $"custom://tracker{t}/announce" }).ToArray ();
            var trackerManager = new TrackerManager (factories, new RequestFactory (), urls, true);
            var trackers = trackerManager.Tiers.Select (t => t.Trackers.Cast<RateLimitingTracker> ().ToList ()).ToList ();

            var cts = new CancellationTokenSource (TimeSpan.FromSeconds (10));
            var announce = trackerManager.AnnounceAsync (CancellationToken.None);
            while (trackers.SelectMany (t => t).Where (t => t.Connection.PendingAnnounces.Count == 1).Count () != 15) {
                Thread.Sleep (1);
                cts.Token.ThrowIfCancellationRequested ();
            }
        }

        [Test]
        public async Task CurrentTracker ()
        {
            trackers[0][0].Connection.FailAnnounce = true;
            trackers[1][0].Connection.FailAnnounce = true;

            foreach (var tier in trackerManager.Tiers) {
                Assert.IsFalse (tier.LastAnnounceSucceeded);
                Assert.AreEqual (TimeSpan.MaxValue, tier.TimeSinceLastAnnounce);
                Assert.AreEqual (tier.Trackers[0], tier.ActiveTracker);
            }

            await trackerManager.AnnounceAsync (CancellationToken.None);

            foreach (var tier in trackerManager.Tiers) {
                Assert.IsTrue (tier.LastAnnounceSucceeded);
                Assert.IsTrue (tier.TimeSinceLastAnnounce < (Debugger.IsAttached ? TimeSpan.MaxValue : TimeSpan.FromSeconds (5)));
                Assert.AreEqual (tier.Trackers[1], tier.ActiveTracker);
            }
            Assert.AreEqual (TorrentEvent.Started, (object) trackers[0][1].Connection.AnnounceParameters.Single ().ClientEvent);
            Assert.AreEqual (TorrentEvent.Started, (object) trackers[1][1].Connection.AnnounceParameters.Single ().ClientEvent);
        }

        [Test]
        public async Task AnnounceTwice_SendStartedOnce ()
        {
            await trackerManager.AnnounceAsync (CancellationToken.None);

            Assert.AreEqual (TorrentEvent.Started, (object) trackers[0][0].Connection.AnnounceParameters.Single ().ClientEvent);
            Assert.AreEqual (TorrentEvent.Started, (object) trackers[1][0].Connection.AnnounceParameters.Single ().ClientEvent);

            trackerManager.Tiers[0].TimeSinceLastAnnounce = TimeSpan.FromHours (1);
            await trackerManager.AnnounceAsync (CancellationToken.None);

            Assert.AreEqual (TorrentEvent.None, (object) trackers[0][0].Connection.AnnounceParameters.Last ().ClientEvent);
            Assert.AreEqual (2, trackers[0][0].Connection.AnnounceParameters.Count);
            Assert.AreEqual (1, trackers[1][0].Connection.AnnounceParameters.Count);
        }

        [Test]
        public void Defaults ()
        {
            var tracker = new CustomTracker (new CustomTrackerConnection (new Uri("http://tester/announce")));
            Assert.AreEqual (TimeSpan.FromMinutes (3), tracker.MinUpdateInterval, "#1");
            Assert.AreEqual (TimeSpan.FromMinutes (30), tracker.UpdateInterval, "#2");
            Assert.IsNotNull (tracker.WarningMessage, "#3");
            Assert.IsNotNull (tracker.FailureMessage, "#5");
        }

        [Test]
        public async Task ScrapePrimary ()
        {
            var argsTask = new TaskCompletionSource<ScrapeResponseEventArgs> ();
            trackerManager.ScrapeComplete += (o, e) => argsTask.SetResult (e);

            await trackerManager.ScrapeAsync (trackers[0][0], CancellationToken.None);

            var args = await argsTask.Task.WithTimeout ("The scrape never completed");
            Assert.IsTrue (args.Successful);
            Assert.AreSame (trackers[0][0], args.Tracker);

            Assert.AreEqual (1, trackers[0][0].Connection.ScrapedAt.Count, "#2");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual (0, trackers[i][0].Connection.ScrapedAt.Count, "#4." + i);
        }

        [Test]
        public async Task ScrapeFailed ()
        {
            var argsTask = new TaskCompletionSource<ScrapeResponseEventArgs> ();
            trackers[0][0].Connection.FailScrape = true;
            trackerManager.ScrapeComplete += (o, e) => argsTask.SetResult (e);

            await trackerManager.ScrapeAsync (trackers[0][0], CancellationToken.None);

            var args = await argsTask.Task.WithTimeout ("The scrape never completed");
            Assert.AreEqual (1, trackers[0][0].Connection.ScrapedAt.Count, "#1");
            Assert.IsFalse (args.Successful, "#2");
            Assert.AreSame (trackers[0][0], args.Tracker, "#3");
        }

        [Test]
        public async Task ScrapeSecondary ()
        {
            var argsTask = new TaskCompletionSource<ScrapeResponseEventArgs> ();
            trackerManager.ScrapeComplete += (o, e) => argsTask.SetResult (e);

            await trackerManager.ScrapeAsync (trackers[0][1], CancellationToken.None);

            var args = await argsTask.Task.WithTimeout ("The scrape never completed");
            Assert.IsTrue (args.Successful);
            Assert.AreSame (trackers[0][1], args.Tracker);

            Assert.AreEqual (1, trackers[0][1].Connection.ScrapedAt.Count, "#2");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual (0, trackers[i][0].Connection.ScrapedAt.Count, "#4." + i);
        }

        [Test]
        public void UnsupportedTrackers ()
        {
            var tiers = new[]{
                new List<string> { "unregistered://1.1.1.1:1111", "unregistered://1.1.1.2:1112" },
                new List<string> { "unregistered://2.2.2.2:2221" },
                new List<string> { "unregistered://3.3.3.3:3331", "unregistered://3.3.3.3:3332" },
            };

            var manager = new TrackerManager (Factories.Default, new RequestFactory (), tiers, false);
            Assert.AreEqual (0, manager.Tiers.Count, "#1");
        }
    }
}
