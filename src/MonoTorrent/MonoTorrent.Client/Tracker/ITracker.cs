using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client.Tracker
{
    interface ITracker
    {
        event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        bool CanAnnounce { get; }
        bool CanScrape { get; }
        int Complete { get; }
        int Downloaded { get;}
        string FailureMessage { get; }
        int Incomplete { get; }
        TimeSpan MinUpdateInterval { get; }
        TimeSpan UpdateInterval { get; }
        Uri Uri { get; }
        string WarningMessage { get; }

        void Announce(AnnounceParameters parameters, object state);
        void Scrape(ScrapeParameters parameters, object state);
    }
}
