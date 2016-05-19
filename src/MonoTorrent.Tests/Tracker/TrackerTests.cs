using System;
using System.Threading;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using MonoTorrent.Tracker.Listeners;
using Xunit;

namespace MonoTorrent.Tracker
{
    public class TrackerTests : IDisposable
    {
        //MonoTorrent.Client.Tracker.HTTPTracker tracker;

        public TrackerTests()
        {
            listener = new HttpListener(uri.OriginalString);
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

        //static void Main(string[] args)
        //{
        //    TrackerTests t = new TrackerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.MultipleAnnounce();
        //    t.FixtureTeardown();
        //}
        private readonly Uri uri = new Uri("http://127.0.0.1:23456/");
        private readonly HttpListener listener;
        private readonly Tracker server;

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
                var tier = new TrackerTier(new[] {uri.ToString()});
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