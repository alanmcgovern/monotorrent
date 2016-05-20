using System;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public abstract class Tracker : ITracker
    {
        private string failureMessage;
        private string warningMessage;

        protected Tracker(Uri uri)
        {
            Check.Uri(uri);
            MinUpdateInterval = TimeSpan.FromMinutes(3);
            UpdateInterval = TimeSpan.FromMinutes(30);
            Uri = uri;
        }

        public event EventHandler BeforeAnnounce;
        public event EventHandler<AnnounceResponseEventArgs> AnnounceComplete;
        public event EventHandler BeforeScrape;
        public event EventHandler<ScrapeResponseEventArgs> ScrapeComplete;

        public bool CanAnnounce { get; protected set; }

        public bool CanScrape { get; set; }

        public int Complete { get; protected set; }

        public int Downloaded { get; protected set; }

        public string FailureMessage
        {
            get { return failureMessage ?? ""; }
            protected set { failureMessage = value; }
        }

        public int Incomplete { get; protected set; }

        public TimeSpan MinUpdateInterval { get; protected set; }

        public TrackerState Status { get; protected set; }

        public TimeSpan UpdateInterval { get; protected set; }

        public Uri Uri { get; }

        public string WarningMessage
        {
            get { return warningMessage ?? ""; }
            protected set { warningMessage = value; }
        }

        public abstract void Announce(AnnounceParameters parameters, object state);
        public abstract void Scrape(ScrapeParameters parameters, object state);

        protected virtual void RaiseBeforeAnnounce()
        {
            var h = BeforeAnnounce;
            if (h != null)
                h(this, EventArgs.Empty);
        }

        protected virtual void RaiseAnnounceComplete(AnnounceResponseEventArgs e)
        {
            var h = AnnounceComplete;
            if (h != null)
                h(this, e);
        }

        protected virtual void RaiseBeforeScrape()
        {
            var h = BeforeScrape;
            if (h != null)
                h(this, EventArgs.Empty);
        }

        protected virtual void RaiseScrapeComplete(ScrapeResponseEventArgs e)
        {
            var h = ScrapeComplete;
            if (h != null)
                h(this, e);
        }
    }
}