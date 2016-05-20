using System;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Tests.Client
{
    public class DefaultTracker : MonoTorrent.Client.Tracker.Tracker
    {
        public DefaultTracker()
            : base(new Uri("http://tracker:5353/announce"))
        {
        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
        }
    }
}