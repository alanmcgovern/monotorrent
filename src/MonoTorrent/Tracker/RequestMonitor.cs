using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    public class RequestMonitor
    {
        #region Constructors

        public RequestMonitor()
        {
            announces = new SpeedMonitor();
            scrapes = new SpeedMonitor();
        }

        #endregion Constructors

        internal void Tick()
        {
            lock (announces)
                announces.Tick();
            lock (scrapes)
                scrapes.Tick();
        }

        #region Member Variables

        private readonly SpeedMonitor announces;
        private readonly SpeedMonitor scrapes;

        #endregion Member Variables

        #region Properties

        public int AnnounceRate
        {
            get { return announces.Rate; }
        }

        public int ScrapeRate
        {
            get { return scrapes.Rate; }
        }

        public int TotalAnnounces
        {
            get { return (int) announces.Total; }
        }

        public int TotalScrapes
        {
            get { return (int) scrapes.Total; }
        }

        #endregion Properties

        #region Methods

        internal void AnnounceReceived()
        {
            lock (announces)
                announces.AddDelta(1);
        }

        internal void ScrapeReceived()
        {
            lock (scrapes)
                scrapes.AddDelta(1);
        }

        #endregion Methods
    }
}