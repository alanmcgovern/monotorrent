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
        private Uri uri = new Uri("http://127.0.0.1:23456/");
        private Listeners.HttpListener listener;
        private Tracker server;
        //MonoTorrent.Client.Tracker.HTTPTracker tracker;

        public TrackerTests()
        {
            listener = new Listeners.HttpListener(uri.OriginalString);
            listener.Start();
            server = new Tracker();
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
            var announceCount = 0;
            var r = new Random();
            var handle = new ManualResetEvent(false);

            for (var i = 0; i < 20; i++)
            {
                var infoHash = new InfoHash(new byte[20]);
                r.NextBytes(infoHash.Hash);
                var tier = new TrackerTier(new string[] {uri.ToString()});
                tier.Trackers[0].AnnounceComplete += delegate
                {
                    if (++announceCount == 20)
                        handle.Set();
                };
                var id = new TrackerConnectionID(tier.Trackers[0], false, TorrentEvent.Started,
                    new ManualResetEvent(false));
                Client.Tracker.AnnounceParameters parameters;
                parameters = new Client.Tracker.AnnounceParameters(0, 0, 0, TorrentEvent.Started,
                    infoHash, false, new string('1', 20), "", 1411);
                tier.Trackers[0].Announce(parameters, id);
            }

            Assert.True(handle.WaitOne(5000, true), "Some of the responses weren't received");
        }
    }
}