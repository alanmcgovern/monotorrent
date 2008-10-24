using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client.Tests;
using MonoTorrent.Client;
using System.Threading;

namespace MonoTorrent.Tests
{
    [TestFixture]
    public class TrackerTests
    {
        static void Main(string[] args)
        {
            TrackerTests t = new TrackerTests();
            t.FixtureSetup();
            t.Setup();
            t.AnnounceToMany();
            t.FixtureTeardown();
        }
        Uri uri = new Uri("http://127.0.0.1:23456/");
        MonoTorrent.Tracker.Listeners.HttpListener listener;
        MonoTorrent.Tracker.Tracker server;
        MonoTorrent.Client.Tracker.HTTPTracker tracker;
        MonoTorrent.Client.Tracker.AnnounceParameters parameters;
        MonoTorrent.Client.Tracker.TrackerTier tier;
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
        }

        [SetUp]
        public void Setup()
        {
            tracker = new MonoTorrent.Client.Tracker.HTTPTracker(uri);
        }

        int announceCount=0;
        [Test]
        [Ignore("This blocks far too much, adjust the timeout on requests instead")]
        public void MultipleAnnounce()
        {
           // listener.Stop();
            Random r = new Random();
            for (int i=0; i < 20; i++)
            {
                byte[] infoHash = new byte[20];
                r.NextBytes(infoHash);
                TrackerTier tier = new TrackerTier(new string[] { uri.ToString() });
                MonoTorrent.Client.Tracker.Tracker t = tier.Trackers[0];
                t.AnnounceComplete += delegate { announceCount++; };
                AnnounceParameters parameters = new MonoTorrent.Client.Tracker.AnnounceParameters(0, 0, 0,
                    MonoTorrent.Common.TorrentEvent.Started, infoHash,
                    new MonoTorrent.Client.Tracker.TrackerConnectionID(t, false, MonoTorrent.Common.TorrentEvent.Started, null),
                    false, new string('1', 20), "", 1411);
                t.Announce(parameters);
                System.Threading.Thread.Sleep(1000);
            }

            System.Threading.Thread.Sleep(20000);
            Console.WriteLine("All completed: {0}", 20 == announceCount);
            Assert.AreEqual(20, announceCount);
        }

        [Test]
        [Ignore ("This hasn't been completed yet")]
        public void AnnounceToMany()
        {
            List<Uri> uris = new List<Uri>();
            List<MonoTorrent.Tracker.Tracker> servers = new List<MonoTorrent.Tracker.Tracker>();
            for (int i = 0; i < 50; i++)
            {
                Uri uri = new Uri(string.Format ("http://localhost:{0}/announce/", 42123 + i));
               // MonoTorrent.Tracker.Tracker t = new MonoTorrent.Tracker.Tracker();
               // MonoTorrent.Tracker.Listeners.HttpListener listener = new MonoTorrent.Tracker.Listeners.HttpListener(uri.ToString());
               // t.RegisterListener(listener);
               //  listener.Start();
                uris.Add(uri);
            }

            using (TestRig rig = new TestRig(""))
            {
                rig.Torrent.AnnounceUrls.Clear();
                for (int i = 0; i < uris.Count; i++)
                {
                    rig.Torrent.AnnounceUrls.Add(new MonoTorrent.Common.MonoTorrentCollection<string>());
                    rig.Torrent.AnnounceUrls[i].Add(uris[i].ToString());
                }

                rig.Engine.Unregister(rig.Manager);
                TorrentManager manager = new TorrentManager(rig.Torrent, "", new TorrentSettings());
                rig.Engine.Register(manager);

                List<MonoTorrent.Client.Tracker.Tracker> trackers = new List<MonoTorrent.Client.Tracker.Tracker>();
                foreach (TrackerTier tier in manager.TrackerManager.TrackerTiers)
                    trackers.AddRange(tier.Trackers);

                foreach (MonoTorrent.Client.Tracker.Tracker t in trackers)
                {
                    ManualResetEvent handle = new ManualResetEvent(false);
                    t.AnnounceComplete += delegate { handle.Set(); };
                    manager.TrackerManager.Announce(t);
                    Assert.IsTrue(handle.WaitOne(10000), "Announce didn't complete");
                }
            }
        }
    }
}
