using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client;
using System.Threading;
using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    public class TrackerTests : IDisposable
    {
        //static void Main(string[] args)
        //{
        //    TrackerTests t = new TrackerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.MultipleAnnounce();
        //    t.FixtureTeardown();
        //}
        Uri uri = new Uri("http://127.0.0.1:23456/");
        MonoTorrent.Tracker.Listeners.HttpListener listener;
        MonoTorrent.Tracker.Tracker server;
        //MonoTorrent.Client.Tracker.HTTPTracker tracker;

        public TrackerTests()
        {
            listener = new MonoTorrent.Tracker.Listeners.HttpListener(uri.OriginalString);
            listener.Start();
            server = new MonoTorrent.Tracker.Tracker();
            server.RegisterListener(listener);
            listener.Start();

            //tracker = new MonoTorrent.Client.Tracker.HTTPTracker(uri);
        }

        public void Dispose()
        {
            listener.Stop();
            server.Dispose();
        }

        [Fact]
        public void MultipleAnnounce()
        {
            int announceCount = 0;
            Random r = new Random();
            ManualResetEvent handle = new ManualResetEvent(false);

            for (int i = 0; i < 20; i++)
            {
                InfoHash infoHash = new InfoHash(new byte[20]);
                r.NextBytes(infoHash.Hash);
                TrackerTier tier = new TrackerTier(new string[] {uri.ToString()});
                tier.Trackers[0].AnnounceComplete += delegate
                {
                    if (++announceCount == 20)
                        handle.Set();
                };
                TrackerConnectionID id = new TrackerConnectionID(tier.Trackers[0], false, TorrentEvent.Started,
                    new ManualResetEvent(false));
                MonoTorrent.Client.Tracker.AnnounceParameters parameters;
                parameters = new MonoTorrent.Client.Tracker.AnnounceParameters(0, 0, 0, TorrentEvent.Started,
                    infoHash, false, new string('1', 20), "", 1411);
                tier.Trackers[0].Announce(parameters, id);
            }

            Assert.True(handle.WaitOne(5000, true), "Some of the responses weren't received");
        }
    }
}