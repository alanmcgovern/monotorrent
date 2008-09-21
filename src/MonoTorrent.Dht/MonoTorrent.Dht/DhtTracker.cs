using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client;

namespace MonoTorrent.Dht
{
    public class DhtTracker : MonoTorrent.Client.Tracker.Tracker
    {
        private DhtEngine engine;
        public DhtTracker(DhtEngine engine)
        {
            this.engine = engine;
            engine.PeersFound += delegate(object o, PeersFoundEventArgs e)
            {
                TrackerConnectionID id = new TrackerConnectionID(this, false, MonoTorrent.Common.TorrentEvent.None, null);
                AnnounceResponseEventArgs response = new AnnounceResponseEventArgs(id);

                response.Successful = true;
                response.Peers.AddRange(e.Peers);

                base.RaiseAnnounceComplete(response);
            };
            CanScrape = false;
        }

        public override WaitHandle Announce(AnnounceParameters parameters)
        {
            engine.GetPeers(parameters.Infohash);
            return new ManualResetEvent(true);
        }

        public override WaitHandle Scrape(ScrapeParameters parameters)
        {
            throw new NotSupportedException();
        }
    }
}
