using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client;
using System.Threading;

namespace MonoTorrent.Tracker
{
    [TestFixture]
    public class TrackerTests
    {
        static void Main(string[] args)
        {
            TrackerTests t = new TrackerTests();
            t.FixtureSetup();
            t.Setup();
            t.MultipleAnnounce();
            t.FixtureTeardown();
        }
        Uri uri = new Uri("http://127.0.0.1:23456/");
        MonoTorrent.Tracker.Listeners.HttpListener listener;
        MonoTorrent.Tracker.Tracker server;
        MonoTorrent.Client.Tracker.HTTPTracker tracker;
        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            listener = new MonoTorrent.Tracker.Listeners.HttpListener(uri.OriginalString);
            server = new MonoTorrent.Tracker.Tracker();
            server.RegisterListener(listener);
            listener.Start();
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            listener.Stop();
            server.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            tracker = new MonoTorrent.Client.Tracker.HTTPTracker(uri);
        }

        int announceCount=0;
        [Test]
        public void MultipleAnnounce()
        {
            Random r = new Random();
            ManualResetEvent handle = new ManualResetEvent(false);

            for (int i=0; i < 20; i++)
            {
                byte[] infoHash = new byte[20];
                r.NextBytes(infoHash);
                TrackerTier tier = new TrackerTier(new string[] { uri.ToString() });
                tier.Trackers[0].AnnounceComplete += delegate {
                    if (++announceCount == 20)
                        handle.Set();
                };
                MonoTorrent.Client.Tracker.AnnounceParameters parameters = new MonoTorrent.Client.Tracker.AnnounceParameters(0, 0, 0,
                    MonoTorrent.Common.TorrentEvent.Started, infoHash,
                    new MonoTorrent.Client.Tracker.TrackerConnectionID(tier.Trackers[0], false, MonoTorrent.Common.TorrentEvent.Started, null),
                    false, new string('1', 20), "", 1411);
                tier.Trackers[0].Announce(parameters);
            }

            Assert.IsTrue(handle.WaitOne(5000), "Some of the responses weren't received");
        }
    }
}
