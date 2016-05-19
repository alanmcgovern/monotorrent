using MonoTorrent.Client.Messages.UdpTracker;

namespace MonoTorrent.Tests.Client
{
    internal class IgnoringListener : MonoTorrent.Tracker.Listeners.UdpListener
    {
        public bool IgnoreAnnounces;
        public bool IgnoreConnects;
        public bool IgnoreErrors = false;
        public bool IgnoreScrapes;

        public IgnoringListener(int port)
            : base(port)
        {
        }

        protected override void ReceiveConnect(ConnectMessage connectMessage)
        {
            if (!IgnoreConnects)
                base.ReceiveConnect(connectMessage);
        }

        protected override void ReceiveAnnounce(AnnounceMessage announceMessage)
        {
            if (!IgnoreAnnounces)
                base.ReceiveAnnounce(announceMessage);
        }

        protected override void ReceiveError(ErrorMessage errorMessage)
        {
            if (!IgnoreErrors)
                base.ReceiveError(errorMessage);
        }

        protected override void ReceiveScrape(ScrapeMessage scrapeMessage)
        {
            if (!IgnoreScrapes)
                base.ReceiveScrape(scrapeMessage);
        }
    }
}