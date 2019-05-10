using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.BEncoding;
using System.Net;
using System.Linq;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class HttpTrackerTests
    {
        AnnounceParameters announceParams;
        ScrapeParameters scrapeParams;
        MonoTorrent.Tracker.Tracker server;
        MonoTorrent.Tracker.Listeners.HttpListener listener;
        string ListeningPrefix => "http://127.0.0.1:47124/";
        Uri AnnounceUrl => new Uri (ListeningPrefix + "/announce");
        HTTPTracker tracker;
        TrackerConnectionID id;


        readonly List<string> keys = new List<string> ();

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            server = new MonoTorrent.Tracker.Tracker();
            server.AllowUnregisteredTorrents = true;
            listener = new MonoTorrent.Tracker.Listeners.HttpListener (ListeningPrefix);
            listener.AnnounceReceived += delegate (object o, MonoTorrent.Tracker.AnnounceParameters e) {
                keys.Add(e.Key);
            };
            server.RegisterListener(listener);
            
            listener.Start();
        }

        [SetUp]
        public void Setup()
        {
            keys.Clear();
            tracker = (HTTPTracker) TrackerFactory.Create(AnnounceUrl);
            id = new TrackerConnectionID(tracker, false, TorrentEvent.Started, new ManualResetEvent(false));
            
            announceParams = new AnnounceParameters();
            announceParams.PeerId = "id";
            announceParams.InfoHash = new InfoHash (new byte[20]);

            scrapeParams = new ScrapeParameters (new InfoHash (new byte[20]));
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            listener.Stop();
            server.Dispose();
        }

        [Test]
        public void CanAnnouceOrScrapeTest()
        {
            Tracker.HTTPTracker t = (HTTPTracker) TrackerFactory.Create(new Uri("http://mytracker.com/myurl"));
            Assert.IsFalse(t.CanScrape, "#1");
            Assert.IsTrue(t.CanAnnounce, "#1b");

            t = (HTTPTracker)TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah"));
            Assert.IsFalse(t.CanScrape, "#2");
            Assert.IsTrue(t.CanAnnounce, "#2b");

            t = (HTTPTracker)TrackerFactory.Create(new Uri("http://mytracker.com/announce"));
            Assert.IsTrue(t.CanScrape, "#3");
            Assert.IsTrue(t.CanAnnounce, "#3b");
            Assert.AreEqual(t.ScrapeUri, new Uri("http://mytracker.com/scrape"));

            t = (HTTPTracker)TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah/announce"));
            Assert.IsTrue(t.CanScrape, "#4");
            Assert.IsTrue(t.CanAnnounce, "#4b");
            Assert.AreEqual("http://mytracker.com/announce/yeah/scrape", t.ScrapeUri.ToString(), "#4c");

            t = (HTTPTracker)TrackerFactory.Create(new Uri("http://mytracker.com/announce/"));
            Assert.IsTrue(t.CanScrape, "#5");
            Assert.IsTrue(t.CanAnnounce, "#5b");
            Assert.AreEqual(t.ScrapeUri, new Uri("http://mytracker.com/scrape/"));
        }

        [Test]
        public async Task Announce()
        {
            await tracker.AnnounceAsync(announceParams, id);
            Assert.IsTrue(StringComparer.OrdinalIgnoreCase.Equals (keys[0], tracker.Key), "#2");
        }

        [Test]
        public void Announce_Timeout ()
        {
            tracker.RequestTimeout = TimeSpan.FromMilliseconds (1);
            Assert.ThrowsAsync<TrackerException> (() => tracker.AnnounceAsync (announceParams, id));
        }

        [Test]
        public async Task KeyTest()
        {
            tracker = (HTTPTracker) TrackerFactory.Create(new Uri(AnnounceUrl + "?key=value"));
            id = new TrackerConnectionID(tracker, false, TorrentEvent.Started, new ManualResetEvent(false));
            await tracker.AnnounceAsync(announceParams, id);
            Assert.AreEqual("value", keys[0], "#1");
        }

        [Test]
        public async Task Scrape()
        {
            var infoHash = new InfoHash (Enumerable.Repeat ((byte)1, 20).ToArray ());
            scrapeParams = new ScrapeParameters (infoHash);

            await tracker.ScrapeAsync(scrapeParams, id);
            Assert.AreEqual(0, tracker.Complete, "#1");
            Assert.AreEqual(0, tracker.Incomplete, "#2");
            Assert.AreEqual(0, tracker.Downloaded, "#3");

            await tracker.AnnounceAsync(new AnnounceParameters (0, 0, 100, TorrentEvent.Started, infoHash, false, "peer1", null, 1), id);
            await tracker.ScrapeAsync(scrapeParams, id);
            Assert.AreEqual(0, tracker.Complete, "#4");
            Assert.AreEqual(1, tracker.Incomplete, "#5");
            Assert.AreEqual(0, tracker.Downloaded, "#6");

            await tracker.AnnounceAsync(new AnnounceParameters (0, 0, 0, TorrentEvent.Started, infoHash, false, "peer2", null, 2), id);
            await tracker.ScrapeAsync(scrapeParams, id);
            Assert.AreEqual(1, tracker.Complete, "#7");
            Assert.AreEqual(1, tracker.Incomplete, "#8");
            Assert.AreEqual(0, tracker.Downloaded, "#9");

            await tracker.AnnounceAsync(new AnnounceParameters (0, 0, 0, TorrentEvent.Completed, infoHash, false, "peer3", null, 3), id);
            await tracker.ScrapeAsync(scrapeParams, id);
            Assert.AreEqual(2, tracker.Complete, "#10");
            Assert.AreEqual(1, tracker.Incomplete, "#11");
            Assert.AreEqual(1, tracker.Downloaded, "#12");
        }

        [Test]
        public void Scrape_Timeout()
        {
            tracker.RequestTimeout = TimeSpan.FromMilliseconds (1);
            Assert.ThrowsAsync<TrackerException> (() => tracker.ScrapeAsync(scrapeParams, id));
        }
    }
}
