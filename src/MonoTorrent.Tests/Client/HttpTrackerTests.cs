using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using System.Threading;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class HttpTrackerTests
    {
        //static void Main()
        //{
        //    HttpTrackerTests t = new HttpTrackerTests();
        //    t.FixtureSetup();
        //    t.KeyTest();
        //}
        MonoTorrent.Tracker.Tracker server;
        MonoTorrent.Tracker.Listeners.HttpListener listener;
        string prefix ="http://localhost:47124/announce/";
        List<string> keys;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            keys = new List<string>();
            server = new MonoTorrent.Tracker.Tracker();
            server.AllowUnregisteredTorrents = true;
            listener = new MonoTorrent.Tracker.Listeners.HttpListener(prefix);
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
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            listener.Stop();
            server.Dispose();
        }

        [Test]
        public void CanAnnouceOrScrapeTest()
        {
            Tracker.Tracker t = TrackerFactory.Create(new Uri("http://mytracker.com/myurl"));
            Assert.IsFalse(t.CanScrape, "#1");
            Assert.IsTrue(t.CanAnnounce, "#1b");

            t = TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah"));
            Assert.IsFalse(t.CanScrape, "#2");
            Assert.IsTrue(t.CanAnnounce, "#2b");

            t = TrackerFactory.Create(new Uri("http://mytracker.com/announce"));
            Assert.IsTrue(t.CanScrape, "#3");
            Assert.IsTrue(t.CanAnnounce, "#4");

            HTTPTracker tracker = (HTTPTracker)TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah/announce"));
            Assert.IsTrue(tracker.CanScrape, "#4");
            Assert.IsTrue(tracker.CanAnnounce, "#4");
            Assert.AreEqual("http://mytracker.com/announce/yeah/scrape", tracker.ScrapeUri.ToString(), "#5");
        }

        [Test]
        public void AnnounceTest()
        {
            HTTPTracker t = (HTTPTracker)TrackerFactory.Create(new Uri(prefix));
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));
            
            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e) {
                p = e;
                id.WaitHandle.Set();
            };
            MonoTorrent.Client.Tracker.AnnounceParameters pars = new AnnounceParameters();
            pars.Infohash = new byte[20];

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#1");
            Assert.IsTrue(p.Successful);
            Assert.AreEqual(keys[0], t.Key, "#2");
        }

        [Test]
        public void KeyTest()
        {
            MonoTorrent.Client.Tracker.AnnounceParameters pars = new AnnounceParameters();
            pars.Infohash = new byte[20];

            Tracker.Tracker t = TrackerFactory.Create(new Uri(prefix + "?key=value"));
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));
            t.AnnounceComplete += delegate { id.WaitHandle.Set(); };
            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.AreEqual("value", keys[0], "#1");
        }

        [Test]
        public void ScrapeTest()
        {
            Tracker.Tracker t = TrackerFactory.Create(new Uri(prefix.Substring(0, prefix.Length -1)));
            Assert.IsTrue(t.CanScrape, "#1");
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e) {
                p = e;
                id.WaitHandle.Set();
            };
            MonoTorrent.Client.Tracker.AnnounceParameters pars = new AnnounceParameters();
            pars.Infohash = new byte[20];

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#2");
            Assert.IsTrue(p.Successful, "#3");
            Assert.AreEqual(1, t.Complete, "#1");
            Assert.AreEqual(0, t.Incomplete, "#2");
            Assert.AreEqual(0, t.Downloaded, "#3");
        }


        void Wait(WaitHandle handle)
        {
            Assert.IsTrue(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }
    }
}
