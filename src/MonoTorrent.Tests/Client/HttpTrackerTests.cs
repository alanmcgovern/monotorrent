using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using System.Threading;

namespace MonoTorrent.Client
{
    
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

        [OneTimeSetUp]
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

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            listener.Stop();
            server.Dispose();
        }

        [Fact]
        public void CanAnnouceOrScrapeTest()
        {
            Tracker.Tracker t = TrackerFactory.Create(new Uri("http://mytracker.com/myurl"));
            Assert.False(t.CanScrape, "#1");
            Assert.True(t.CanAnnounce, "#1b");

            t = TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah"));
            Assert.False(t.CanScrape, "#2");
            Assert.True(t.CanAnnounce, "#2b");

            t = TrackerFactory.Create(new Uri("http://mytracker.com/announce"));
            Assert.True(t.CanScrape, "#3");
            Assert.True(t.CanAnnounce, "#4");

            HTTPTracker tracker = (HTTPTracker)TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah/announce"));
            Assert.True(tracker.CanScrape, "#4");
            Assert.True(tracker.CanAnnounce, "#4");
            Assert.Equal("http://mytracker.com/announce/yeah/scrape", tracker.ScrapeUri.ToString(), "#5");
        }

        [Fact]
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
            pars.PeerId = "id";
            pars.InfoHash = new InfoHash (new byte[20]);

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.NotNull(p, "#1");
            Assert.True(p.Successful);
            Assert.Equal(keys[0], t.Key, "#2");
        }

        [Fact]
        public void KeyTest()
        {
            MonoTorrent.Client.Tracker.AnnounceParameters pars = new AnnounceParameters();
            pars.PeerId = "id";
            pars.InfoHash = new InfoHash (new byte[20]);

            Tracker.Tracker t = TrackerFactory.Create(new Uri(prefix + "?key=value"));
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));
            t.AnnounceComplete += delegate { id.WaitHandle.Set(); };
            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.Equal("value", keys[0], "#1");
        }

        [Fact]
        public void ScrapeTest()
        {
            Tracker.Tracker t = TrackerFactory.Create(new Uri(prefix.Substring(0, prefix.Length -1)));
            Assert.True(t.CanScrape, "#1");
            TrackerConnectionID id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e) {
                p = e;
                id.WaitHandle.Set();
            };
            MonoTorrent.Client.Tracker.AnnounceParameters pars = new AnnounceParameters();
            pars.PeerId = "id";
            pars.InfoHash = new InfoHash(new byte[20]);

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.NotNull(p, "#2");
            Assert.True(p.Successful, "#3");
            Assert.Equal(1, t.Complete, "#1");
            Assert.Equal(0, t.Incomplete, "#2");
            Assert.Equal(0, t.Downloaded, "#3");
        }


        void Wait(WaitHandle handle)
        {
            Assert.True(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }
    }
}
