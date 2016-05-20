using System;
using MonoTorrent.Client;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;

namespace SampleClient
{
    public class CustomTracker : Tracker
    {
        public CustomTracker(Uri uri)
            : base(uri)
        {
            CanScrape = false;
        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
            RaiseAnnounceComplete(new AnnounceResponseEventArgs(this, state, true));
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
            RaiseScrapeComplete(new ScrapeResponseEventArgs(this, state, true));
        }

        public void AddPeer(Peer p)
        {
            var id = new TrackerConnectionID(this, false, TorrentEvent.None, null);
            var e = new AnnounceResponseEventArgs(this, null, true);
            e.Peers.Add(p);
            e.Successful = true;
            RaiseAnnounceComplete(e);
        }

        public void AddFailedPeer(Peer p)
        {
            var id = new TrackerConnectionID(this, true, TorrentEvent.None, null);
            var e = new AnnounceResponseEventArgs(this, null, true);
            e.Peers.Add(p);
            e.Successful = false;
            RaiseAnnounceComplete(e);
        }
    }
}