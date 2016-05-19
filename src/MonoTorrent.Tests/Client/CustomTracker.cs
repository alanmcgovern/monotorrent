using System;
using System.Collections.Generic;
using System.Threading;
using MonoTorrent.Client;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Tests.Client
{
    public class CustomTracker : MonoTorrent.Client.Tracker.Tracker
    {
        public List<DateTime> AnnouncedAt = new List<DateTime>();

        public bool FailAnnounce;
        public bool FailScrape;
        public List<DateTime> ScrapedAt = new List<DateTime>();

        public CustomTracker(Uri uri)
            : base(uri)
        {
            CanAnnounce = true;
            CanScrape = true;
        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
            RaiseBeforeAnnounce();
            AnnouncedAt.Add(DateTime.Now);
            RaiseAnnounceComplete(new AnnounceResponseEventArgs(this, state, !FailAnnounce));
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
            RaiseBeforeScrape();
            ScrapedAt.Add(DateTime.Now);
            RaiseScrapeComplete(new ScrapeResponseEventArgs(this, state, !FailScrape));
        }

        public void AddPeer(Peer p)
        {
            var id = new TrackerConnectionID(this, false, TorrentEvent.None, new ManualResetEvent(false));
            var e = new AnnounceResponseEventArgs(this, id, true);
            e.Peers.Add(p);
            RaiseAnnounceComplete(e);
            Assert.True(id.WaitHandle.WaitOne(1000, true), "#1 Tracker never raised the AnnounceComplete event");
        }

        public void AddFailedPeer(Peer p)
        {
            var id = new TrackerConnectionID(this, true, TorrentEvent.None, new ManualResetEvent(false));
            var e = new AnnounceResponseEventArgs(this, id, false);
            e.Peers.Add(p);
            RaiseAnnounceComplete(e);
            Assert.True(id.WaitHandle.WaitOne(1000, true), "#2 Tracker never raised the AnnounceComplete event");
        }

        public override string ToString()
        {
            return Uri.ToString();
        }
    }
}