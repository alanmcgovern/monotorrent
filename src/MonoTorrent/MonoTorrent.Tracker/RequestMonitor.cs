using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    public class RequestMonitor
    {
        #region Member Variables

        private SpeedMonitor announces;
        private SpeedMonitor scrapes;

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
            get { return (int)announces.Total; }
        }

        public int TotalScrapes
        {
            get { return (int)scrapes.Total; }
        }


        #endregion Properties


        #region Constructors

        public RequestMonitor()
        {
            announces = new SpeedMonitor();
            scrapes = new SpeedMonitor();
        }

        #endregion Constructors


        #region Methods

        internal void AnnounceReceived()
        {
            lock (announces)
                announces.AddDelta(1);
        }

        internal void ScrapeReceived()
        {
            lock (announces)
                scrapes.AddDelta(1);
        }

        #endregion Methods

        internal void Tick()
        {
            lock (announces)
                this.announces.Tick();
            lock (scrapes)
                this.scrapes.Tick();
        }
    }
}
