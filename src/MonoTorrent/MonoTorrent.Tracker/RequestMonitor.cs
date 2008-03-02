using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    public class RequestMonitor
    {
        private SpeedMonitor announces;
        private SpeedMonitor scrapes;


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


        public RequestMonitor()
        {
            announces = new SpeedMonitor();
            scrapes = new SpeedMonitor();
        }


        internal void AnnounceReceived()
        {
            announces.AddDelta(1);
        }


        internal void ScrapeReceived()
        {
            scrapes.AddDelta(1);
        }
    }
}
