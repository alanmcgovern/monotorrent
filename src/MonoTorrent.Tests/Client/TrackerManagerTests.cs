using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    class DefaultTracker : Tracker.Tracker
    {
        public DefaultTracker()
            :base(new Uri("http://tracker:5353/announce"))
        {

        }

        protected override Task<List<Peer>> DoAnnounceAsync(AnnounceParameters parameters)
        {
            return Task.FromResult (new List<Peer>());
        }

        protected override Task DoScrapeAsync(ScrapeParameters parameters)
        {
            return Task.CompletedTask;
        }
    }

    [TestFixture]
    public class TrackerManagerTests
    {
        TestRig rig;
        List<List<CustomTracker>> trackers;
        TrackerManager trackerManager;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            string[][] trackers = new string[][] {
                new string [] {
                    "custom://tracker1.com/announce",
                    "custom://tracker2.com/announce",
                    "custom://tracker3.com/announce",
                    "custom://tracker4.com/announce"
                },
                new string[] {
                    "custom://tracker5.com/announce",
                    "custom://tracker6.com/announce",
                    "custom://tracker7.com/announce",
                    "custom://tracker8.com/announce"
                }
            };

            rig = TestRig.CreateTrackers(trackers);
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public async Task Setup()
        {
            await rig.RecreateManager();
            trackerManager = rig.Manager.TrackerManager;
            this.trackers = new List<List<CustomTracker>>();
            foreach (TrackerTier t in trackerManager.Tiers)
            {
                List<CustomTracker> list = new List<CustomTracker>();
                foreach (Tracker.Tracker tracker in t)
                    list.Add((CustomTracker)tracker);
                this.trackers.Add(list);
            }
        }

        [Test]
        public void Defaults()
        {
            DefaultTracker tracker = new DefaultTracker();
            Assert.AreEqual(TimeSpan.FromMinutes(3), tracker.MinUpdateInterval, "#1");
            Assert.AreEqual(TimeSpan.FromMinutes(30), tracker.UpdateInterval, "#2");
            Assert.IsNotNull(tracker.WarningMessage, "#3");
            Assert.IsNotNull(tracker.FailureMessage, "#5");
        }

        [Test]
        public async Task ScrapePrimaryTest()
        {
            ScrapeResponseEventArgs args = null;
            trackerManager.ScrapeComplete += (o, e) => args = e;

            await trackerManager.Scrape();
            Assert.IsTrue (args.Successful);
            Assert.AreSame (trackers[0][0], args.Tracker);

            Assert.AreEqual(1, trackers[0][0].ScrapedAt.Count, "#2");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual(0, trackers[i][0].ScrapedAt.Count, "#4." + i);
        }

        [Test]
        public async Task ScrapeSecondaryTest ()
        {
            ScrapeResponseEventArgs args = null;
            trackerManager.ScrapeComplete += (o, e) => args = e;

            await trackerManager.Scrape(trackers[0][1]);
            Assert.IsTrue (args.Successful);
            Assert.AreSame (trackers[0][1], args.Tracker);

            Assert.AreEqual(1, trackers[0][1].ScrapedAt.Count, "#2");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual(0, trackers[i][0].ScrapedAt.Count, "#4." + i);
        }

        [Test]
        public async Task ScrapeFailedTest()
        {
            ScrapeResponseEventArgs args = null;
            trackers[0][0].FailScrape = true;
            trackerManager.ScrapeComplete += (o, e) => args = e;

            await trackerManager.Scrape();
            Assert.AreEqual(1, trackers[0][0].ScrapedAt.Count, "#1");
            Assert.IsFalse (args.Successful, "#2");
            Assert.AreSame (trackers[0][0], args.Tracker, "#3");
        }

        [Test]
        public async Task AnnounceTest()
        {
            await trackerManager.Announce();
            Assert.AreEqual(1, trackers[0][0].AnnouncedAt.Count, "#2");
            Assert.That((DateTime.Now - trackers[0][0].AnnouncedAt[0]) < TimeSpan.FromSeconds(1), "#3");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual(0, trackers[0][i].AnnouncedAt.Count, "#4." + i);

            await trackerManager.Announce(trackers[0][1]);
            Assert.AreEqual(1, trackers[0][1].AnnouncedAt.Count, "#6");
            Assert.That((DateTime.Now - trackers[0][1].AnnouncedAt[0]) < TimeSpan.FromSeconds(1), "#7");
        }

        [Test]
        public async Task AnnounceAllFailed ()
        {
            AnnounceResponseEventArgs args = null;
            trackerManager.AnnounceComplete += (o, e) => args = e;

            foreach (var tier in trackers)
                foreach (var tracker in tier)
                    tracker.FailAnnounce = true;

            await trackerManager.Announce();

            foreach (var tier in trackers)
                foreach (var tracker in tier)
                    Assert.AreEqual (1, tracker.AnnouncedAt.Count, "#1." + tracker.Uri);

            Assert.IsNull(args.Tracker, "#2");
            Assert.IsFalse(args.Successful, "#3");
        }

        [Test]
        public async Task AnnounceFailedTest()
        {
            AnnounceResponseEventArgs args = null;
            trackerManager.AnnounceComplete += (o, e) => args = e;
            trackers[0][0].FailAnnounce = true;
            trackers[0][1].FailAnnounce = true;
            trackers[0][3].FailAnnounce = true;

            await trackerManager.Announce();
            Assert.AreEqual(trackers[0][2], trackerManager.CurrentTracker, "#1");
            Assert.AreEqual(1, trackers[0][0].AnnouncedAt.Count, "#2");
            Assert.AreEqual(1, trackers[0][1].AnnouncedAt.Count, "#3");
            Assert.AreEqual(1, trackers[0][2].AnnouncedAt.Count, "#4");
            Assert.AreEqual(0, trackers[0][3].AnnouncedAt.Count, "#5");
            Assert.AreEqual (args.Tracker, trackers[0][2], "#6");
            Assert.IsTrue (args.Successful, "#7");
        }

        [Test]
        public async Task AnnounceSecondTier ()
        {
            AnnounceResponseEventArgs args = null;
            trackerManager.AnnounceComplete += (o, e) => args = e;

            for (int i = 0; i < trackers[0].Count; i++)
                trackers[0][i].FailAnnounce = true;
            
            await trackerManager.Announce();
            
            for (int i = 0; i < trackers[0].Count; i++)
                Assert.AreEqual(1, trackers[0][i].AnnouncedAt.Count, "#1." + i);

            Assert.AreEqual(trackers[1][0], trackerManager.CurrentTracker, "#2");
            Assert.AreSame (trackers[1][0], args.Tracker, "#4");
            Assert.IsTrue (args.Successful, "#4");
        }
    }
}
