using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoTorrent.Client;

namespace MonoTorrent.Client
{
    public class ManagerNotFoundEventArgs:EventArgs
    {
        public ManagerNotFoundEventArgs(InfoHash infoHash)
        {
            InfoHash = infoHash;
        }

        public InfoHash InfoHash { get; private set; }
        public TorrentManager Manager { get; set; }
    }
}
