using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Tracker
{
    public class ScrapeParameters
    {
        private InfoHash infoHash;


        public InfoHash InfoHash
        {
            get { return infoHash; }
        }

        public ScrapeParameters(InfoHash infoHash)
        {
            this.infoHash = infoHash;
        }
    }
}
