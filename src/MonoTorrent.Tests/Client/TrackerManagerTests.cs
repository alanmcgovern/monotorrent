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
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.Client.Tracker;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    class DefaultTracker : Tracker.Tracker
    {
        public DefaultTracker ()
            : base (new Uri ("http://tracker:5353/announce"))
        {

        }

        protected override Task<List<Peer>> DoAnnounceAsync (AnnounceParameters parameters)
        {
            return Task.FromResult (new List<Peer> ());
        }

        protected override Task DoScrapeAsync (ScrapeParameters parameters)
        {
            return Task.CompletedTask;
        }
    }

    [TestFixture]
    public class TrackerManagerTests
    {
        class RequestFactory : ITrackerRequestFactory
        {
            public readonly InfoHash InfoHash = new InfoHash (new byte[20]);

            public AnnounceParameters CreateAnnounce (TorrentEvent clientEvent)
            {
                return new AnnounceParameters ()
                    .WithClientEvent (clientEvent)
                    .WithInfoHash (InfoHash);
            }

            public ScrapeParameters CreateScrape ()
            {
                return new ScrapeParameters (InfoHash);
            }
        }

        static readonly string[][] trackerUrls = {
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

        [SetUp]
        public void Setup ()
        {
            TrackerFactory.Register ("custom", uri => new CustomTracker (uri));
            trackerManager = new TrackerManager (new RequestFactory (), trackerUrls.Select (t => new RawTrackerTier (t)));
            trackers = trackerManager.Tiers.Select (t => t.Trackers.Cast<CustomTracker> ().ToList ()).ToList ();
        }


        [Test]
        public async Task Announce ()
        {
            await trackerManager.Announce ();
            Assert.AreEqual (1, trackers[0][0].AnnouncedAt.Count, "#2");
            Assert.That ((DateTime.Now - trackers[0][0].AnnouncedAt[0]) < TimeSpan.FromSeconds (1), "#3");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual (0, trackers[0][i].AnnouncedAt.Count, "#4." + i);

            await trackerManager.Announce (trackers[0][1]);
            Assert.AreEqual (1, trackers[0][1].AnnouncedAt.Count, "#6");
            Assert.That ((DateTime.Now - trackers[0][1].AnnouncedAt[0]) < TimeSpan.FromSeconds (1), "#7");
        }

        [Test]
        public async Task AnnounceAllFailed ()
        {
            var argsTask = new TaskCompletionSource<AnnounceResponseEventArgs> ();
            trackerManager.AnnounceComplete += (o, e) => argsTask.TrySetResult (e);

            foreach (var tier in trackers)
                foreach (var tracker in tier)
                    tracker.FailAnnounce = true;

            await trackerManager.Announce ();

            var args = await argsTask.Task.WithTimeout ("The announce never completed");
            foreach (var tier in trackers)
                foreach (var tracker in tier)
                    Assert.AreEqual (1, tracker.AnnouncedAt.Count, "#1." + tracker.Uri);

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
            trackers[0][0].FailAnnounce = true;
            trackers[0][1].FailAnnounce = true;

            await trackerManager.Announce ().WithTimeout ();
            await tcs.Task.WithTimeout ();
            Assert.AreEqual (4, announces.Count);

            Assert.AreEqual (2, trackerManager.Tiers [0].ActiveTrackerIndex, "#1");
            Assert.AreEqual (1, trackers[0][0].AnnouncedAt.Count, "#2a");
            Assert.IsFalse (announces.Single (args => args.Tracker == trackers[0][0]).Successful, "#2b");

            Assert.AreEqual (1, trackers[0][1].AnnouncedAt.Count, "#3a");
            Assert.IsFalse (announces.Single (args => args.Tracker == trackers[0][1]).Successful, "#3b");

            Assert.AreEqual (1, trackers[0][2].AnnouncedAt.Count, "#4a");
            Assert.IsTrue (announces.Single (args => args.Tracker == trackers[0][2]).Successful, "#4b");

            Assert.AreEqual (0, trackers[0][3].AnnouncedAt.Count, "#5");

            Assert.AreEqual (0, trackerManager.Tiers[1].ActiveTrackerIndex, "#6");
            Assert.AreEqual (1, trackers[1][0].AnnouncedAt.Count, "#7a");
            Assert.IsTrue (announces.Single (args => args.Tracker == trackers[1][0]).Successful, "#7b");

            Assert.AreEqual (0, trackers[1][1].AnnouncedAt.Count, "#8");
        }

        [Test]
        public async Task CurrentTracker ()
        {
            trackers[0][0].FailAnnounce = true;
            trackers[1][0].FailAnnounce = true;

            foreach (var tier in trackerManager.Tiers) {
                Assert.IsFalse (tier.LastAnnounceSucceeded);
                Assert.IsFalse (tier.SentStartedEvent);
                Assert.AreEqual (TimeSpan.MaxValue, tier.TimeSinceLastAnnounce);
                Assert.AreEqual (0, tier.ActiveTrackerIndex);
            }

            await trackerManager.Announce ();

            foreach (var tier in trackerManager.Tiers) {
                Assert.IsTrue (tier.LastAnnounceSucceeded);
                Assert.IsTrue (tier.SentStartedEvent);
                Assert.IsTrue (tier.TimeSinceLastAnnounce < TimeSpan.FromSeconds (5));
                Assert.AreEqual (1, tier.ActiveTrackerIndex);
            }
        }

        [Test]
        public void Defaults ()
        {
            DefaultTracker tracker = new DefaultTracker ();
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

            await trackerManager.Scrape (trackers[0][0]);

            var args = await argsTask.Task.WithTimeout ("The scrape never completed");
            Assert.IsTrue (args.Successful);
            Assert.AreSame (trackers[0][0], args.Tracker);

            Assert.AreEqual (1, trackers[0][0].ScrapedAt.Count, "#2");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual (0, trackers[i][0].ScrapedAt.Count, "#4." + i);
        }

        [Test]
        public async Task ScrapeFailed ()
        {
            var argsTask = new TaskCompletionSource<ScrapeResponseEventArgs> ();
            trackers[0][0].FailScrape = true;
            trackerManager.ScrapeComplete += (o, e) => argsTask.SetResult (e);

            await trackerManager.Scrape (trackers[0][0]);

            var args = await argsTask.Task.WithTimeout ("The scrape never completed");
            Assert.AreEqual (1, trackers[0][0].ScrapedAt.Count, "#1");
            Assert.IsFalse (args.Successful, "#2");
            Assert.AreSame (trackers[0][0], args.Tracker, "#3");
        }

        [Test]
        public async Task ScrapeSecondary ()
        {
            var argsTask = new TaskCompletionSource<ScrapeResponseEventArgs> ();
            trackerManager.ScrapeComplete += (o, e) => argsTask.SetResult (e);

            await trackerManager.Scrape (trackers[0][1]);

            var args = await argsTask.Task.WithTimeout ("The scrape never completed");
            Assert.IsTrue (args.Successful);
            Assert.AreSame (trackers[0][1], args.Tracker);

            Assert.AreEqual (1, trackers[0][1].ScrapedAt.Count, "#2");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual (0, trackers[i][0].ScrapedAt.Count, "#4." + i);
        }

        [Test]
        public void UnsupportedTrackers ()
        {
            RawTrackerTier[] tiers = {
                new RawTrackerTier { "unregistered://1.1.1.1:1111", "unregistered://1.1.1.2:1112" },
                new RawTrackerTier { "unregistered://2.2.2.2:2221" },
                new RawTrackerTier { "unregistered://3.3.3.3:3331", "unregistered://3.3.3.3:3332" },
            };

            var manager = new TrackerManager (new RequestFactory (), tiers);
            Assert.AreEqual (0, manager.Tiers.Count, "#1");
        }
    }
}
