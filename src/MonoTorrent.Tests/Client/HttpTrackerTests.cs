using System;
using System.Collections.Generic;
using System.Threading;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using MonoTorrent.Tracker.Listeners;
using Xunit;
using AnnounceParameters = MonoTorrent.Tracker.AnnounceParameters;

namespace MonoTorrent.Client
{
    public class HttpTrackerTests : IDisposable
    {
        public HttpTrackerTests()
        {
            keys = new List<string>();
            server = new MonoTorrent.Tracker.Tracker();
            server.AllowUnregisteredTorrents = true;
            listener = new HttpListener(prefix);
            listener.AnnounceReceived +=
                delegate(object o, AnnounceParameters e) { keys.Add(e.Key); };
            server.RegisterListener(listener);

            listener.Start();

            keys.Clear();
        }

        public void Dispose()
        {
            listener.Stop();
            server.Dispose();
        }

        //static void Main()
        //{
        //    HttpTrackerTests t = new HttpTrackerTests();
        //    t.FixtureSetup();
        //    t.KeyTest();
        //}
        private readonly MonoTorrent.Tracker.Tracker server;
        private readonly HttpListener listener;
        private readonly string prefix = "http://localhost:47124/announce/";
        private readonly List<string> keys;


        private void Wait(WaitHandle handle)
        {
            Assert.True(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }

        [Fact]
        public void AnnounceTest()
        {
            var t = (HTTPTracker) TrackerFactory.Create(new Uri(prefix));
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e)
            {
                p = e;
                id.WaitHandle.Set();
            };
            var pars = new Tracker.AnnounceParameters();
            pars.PeerId = "id";
            pars.InfoHash = new InfoHash(new byte[20]);

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.NotNull(p);
            Assert.True(p.Successful);
            Assert.Equal(keys[0], t.Key);
        }

        [Fact]
        public void CanAnnouceOrScrapeTest()
        {
            var t = TrackerFactory.Create(new Uri("http://mytracker.com/myurl"));
            Assert.False(t.CanScrape);
            Assert.True(t.CanAnnounce);

            t = TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah"));
            Assert.False(t.CanScrape);
            Assert.True(t.CanAnnounce);

            t = TrackerFactory.Create(new Uri("http://mytracker.com/announce"));
            Assert.True(t.CanScrape);
            Assert.True(t.CanAnnounce);

            var tracker =
                (HTTPTracker) TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah/announce"));
            Assert.True(tracker.CanScrape);
            Assert.True(tracker.CanAnnounce);
            Assert.Equal("http://mytracker.com/announce/yeah/scrape", tracker.ScrapeUri.ToString());
        }

        [Fact]
        public void KeyTest()
        {
            var pars = new Tracker.AnnounceParameters();
            pars.PeerId = "id";
            pars.InfoHash = new InfoHash(new byte[20]);

            var t = TrackerFactory.Create(new Uri(prefix + "?key=value"));
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));
            t.AnnounceComplete += delegate { id.WaitHandle.Set(); };
            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.Equal("value", keys[0]);
        }

        [Fact]
        public void ScrapeTest()
        {
            var t = TrackerFactory.Create(new Uri(prefix.Substring(0, prefix.Length - 1)));
            Assert.True(t.CanScrape);
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e)
            {
                p = e;
                id.WaitHandle.Set();
            };
            var pars = new Tracker.AnnounceParameters();
            pars.PeerId = "id";
            pars.InfoHash = new InfoHash(new byte[20]);

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.NotNull(p);
            Assert.True(p.Successful);
            Assert.Equal(1, t.Complete);
            Assert.Equal(0, t.Incomplete);
            Assert.Equal(0, t.Downloaded);
        }
    }
}