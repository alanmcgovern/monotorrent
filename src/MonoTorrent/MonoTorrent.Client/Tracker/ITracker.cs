using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public interface ITracker
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
        TrackerState Status { get; }
        TimeSpan UpdateInterval { get; }
        Uri Uri { get; }
        string WarningMessage { get; }

        Task<List<Peer>> AnnounceAsync(AnnounceParameters parameters);
        Task ScrapeAsync(ScrapeParameters parameters);
    }
}
