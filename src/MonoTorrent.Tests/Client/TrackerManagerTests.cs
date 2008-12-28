using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client;
using System.Threading;

namespace MonoTorrent.Client
{
    public class DefaultTracker : Tracker.Tracker
    {
        public DefaultTracker()
            :base(new Uri("http://tracker:5353/announce"))
        {

        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
        }
    }

    [TestFixture]
    public class TrackerManagerTests
    {
        static void Main()
        {
            TrackerManagerTests t = new TrackerManagerTests();
            t.FixtureSetup();
            t.Setup();
            t.ScrapeTest();
        }
        TestRig rig;
        List<List<CustomTracker>> trackers;
        TrackerManager trackerManager;

        [TestFixtureSetUp]
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

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            rig.RecreateManager();
            trackerManager = rig.Manager.TrackerManager;
            this.trackers = new List<List<CustomTracker>>();
            foreach (TrackerTier t in trackerManager)
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
        public void ScrapeTest()
        {
            Wait(trackerManager.Scrape());
            Assert.AreEqual(1, trackers[0][0].ScrapedAt.Count, "#2");
            Assert.That((DateTime.Now - trackers[0][0].ScrapedAt[0]) < TimeSpan.FromSeconds(1), "#3");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual(0, trackers[i][0].ScrapedAt.Count, "#4." + i);
            Wait(trackerManager.Scrape(trackers[0][1]));
            Assert.AreEqual(1, trackers[0][1].ScrapedAt.Count, "#6");
            Assert.That((DateTime.Now - trackers[0][1].ScrapedAt[0]) < TimeSpan.FromSeconds(1), "#7");
        }

        [Test]
        public void AnnounceTest()
        {
            Wait(trackerManager.Announce());
            Assert.AreEqual(1, trackers[0][0].AnnouncedAt.Count, "#2");
            Assert.That((DateTime.Now - trackers[0][0].AnnouncedAt[0]) < TimeSpan.FromSeconds(1), "#3");
            for (int i = 1; i < trackers.Count; i++)
                Assert.AreEqual(0, trackers[0][i].AnnouncedAt.Count, "#4." + i);
            Wait(trackerManager.Announce(trackers[0][1]));
            Assert.AreEqual(1, trackers[0][1].AnnouncedAt.Count, "#6");
            Assert.That((DateTime.Now - trackers[0][1].AnnouncedAt[0]) < TimeSpan.FromSeconds(1), "#7");
        }

        [Test]
        public void AnnounceFailedTest()
        {
            trackers[0][0].FailAnnounce = true;
            trackers[0][1].FailAnnounce = true;
            Wait(trackerManager.Announce());
            Assert.AreEqual(trackers[0][2], trackerManager.CurrentTracker, "#1");
            Assert.AreEqual(1, trackers[0][0].AnnouncedAt.Count, "#2");
            Assert.AreEqual(1, trackers[0][1].AnnouncedAt.Count, "#3");
            Assert.AreEqual(1, trackers[0][2].AnnouncedAt.Count, "#4");
        }

        [Test]
        public void AnnounceFailedTest2()
        {
            for (int i = 0; i < trackers[0].Count; i++)
                trackers[0][i].FailAnnounce = true;
            
            Wait(trackerManager.Announce());
            
            for (int i = 0; i < trackers[0].Count; i++)
                Assert.AreEqual(1, trackers[0][i].AnnouncedAt.Count, "#1." + i);

            Assert.AreEqual(trackers[1][0], trackerManager.CurrentTracker, "#2");
        }

        void Wait(WaitHandle handle)
        {
            Assert.IsTrue(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }
    }
}
