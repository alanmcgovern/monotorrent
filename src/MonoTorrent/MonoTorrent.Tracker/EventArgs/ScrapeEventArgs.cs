using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Tracker
{
    public class ScrapeEventArgs : EventArgs
    {
        private List<SimpleTorrentManager> torrents;

        public List<SimpleTorrentManager> Torrents
        {
            get { return torrents; }
        }

        public ScrapeEventArgs(List<SimpleTorrentManager> torrents)
        {
            this.torrents = torrents;
        }
    }
}
