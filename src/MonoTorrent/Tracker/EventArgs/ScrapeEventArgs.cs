using System;
using System.Collections.Generic;

namespace MonoTorrent.Tracker
{
    public class ScrapeEventArgs : EventArgs
    {
        public ScrapeEventArgs(List<SimpleTorrentManager> torrents)
        {
            Torrents = torrents;
        }

        public List<SimpleTorrentManager> Torrents { get; }
    }
}