using MonoTorrent.Client.Tracker;
using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace MonoTorrent.Client
{
    public class DefaultTracker : Tracker.Tracker
    {
        public DefaultTracker()
            : base(new Uri("http://tracker:5353/announce"))
        {
        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
        }
    }


    public class TrackerManagerTests : IDisposable
    {
        //static void Main()
        //{
        //    TrackerManagerTests t = new TrackerManagerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.ScrapeTest();
        //}
        private TestRig rig;
        private List<List<CustomTracker>> trackers;
        private TrackerManager trackerManager;

        public TrackerManagerTests()
        {
            var trackers = new string[][]
            {
                new string[]
                {
                    "custom://tracker1.com/announce",
                    "custom://tracker2.com/announce",
                    "custom://tracker3.com/announce",
                    "custom://tracker4.com/announce"
                },
                new string[]
                {
                    "custom://tracker5.com/announce",
                    "custom://tracker6.com/announce",
                    "custom://tracker7.com/announce",
                    "custom://tracker8.com/announce"
                }
            };

            rig = TestRig.CreateTrackers(trackers);

            rig.RecreateManager();
            trackerManager = rig.Manager.TrackerManager;
            this.trackers = new List<List<CustomTracker>>();
            foreach (var t in trackerManager)
            {
                var list = new List<CustomTracker>();
                foreach (var tracker in t)
                    list.Add((CustomTracker) tracker);
                this.trackers.Add(list);
            }
        }

        public void Dispose()
        {
            rig.Dispose();
        }

        [Fact]
        public void Defaults()
        {
            var tracker = new DefaultTracker();
            Assert.Equal(TimeSpan.FromMinutes(3), tracker.MinUpdateInterval);
            Assert.Equal(TimeSpan.FromMinutes(30), tracker.UpdateInterval);
            Assert.NotNull(tracker.WarningMessage);
            Assert.NotNull(tracker.FailureMessage);
        }

        [Fact]
        public void ScrapeTest()
        {
            var scrapeStarted = false;
            trackers[0][0].BeforeScrape += delegate { scrapeStarted = true; };
            trackers[0][0].ScrapeComplete += delegate
            {
                if (!scrapeStarted) throw new Exception("Scrape didn't start");
            };
            Wait(trackerManager.Scrape());
            Assert.True(scrapeStarted);
            Assert.Equal(1, trackers[0][0].ScrapedAt.Count);
            Assert.True(DateTime.Now - trackers[0][0].ScrapedAt[0] < TimeSpan.FromSeconds(1));
            for (var i = 1; i < trackers.Count; i++)
                Assert.Equal(0, trackers[i][0].ScrapedAt.Count);
            Wait(trackerManager.Scrape(trackers[0][1]));
            Assert.Equal(1, trackers[0][1].ScrapedAt.Count);
            Assert.True(DateTime.Now - trackers[0][1].ScrapedAt[0] < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void AnnounceTest()
        {
            Wait(trackerManager.Announce());
            Assert.Equal(1, trackers[0][0].AnnouncedAt.Count);
            Assert.True(DateTime.Now - trackers[0][0].AnnouncedAt[0] < TimeSpan.FromSeconds(1));
            for (var i = 1; i < trackers.Count; i++)
                Assert.Equal(0, trackers[0][i].AnnouncedAt.Count);
            Wait(trackerManager.Announce(trackers[0][1]));
            Assert.Equal(1, trackers[0][1].AnnouncedAt.Count);
            Assert.True(DateTime.Now - trackers[0][1].AnnouncedAt[0] < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void AnnounceFailedTest()
        {
            trackers[0][0].FailAnnounce = true;
            trackers[0][1].FailAnnounce = true;
            Wait(trackerManager.Announce());
            Assert.Equal(trackers[0][2], trackerManager.CurrentTracker);
            Assert.Equal(1, trackers[0][0].AnnouncedAt.Count);
            Assert.Equal(1, trackers[0][1].AnnouncedAt.Count);
            Assert.Equal(1, trackers[0][2].AnnouncedAt.Count);
        }

        [Fact]
        public void AnnounceFailedTest2()
        {
            for (var i = 0; i < trackers[0].Count; i++)
                trackers[0][i].FailAnnounce = true;

            Wait(trackerManager.Announce());

            for (var i = 0; i < trackers[0].Count; i++)
                Assert.Equal(1, trackers[0][i].AnnouncedAt.Count);

            Assert.Equal(trackers[1][0], trackerManager.CurrentTracker);
        }

        private void Wait(WaitHandle handle)
        {
            Assert.True(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }
    }
}