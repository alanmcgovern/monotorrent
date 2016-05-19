using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class TorrentEventArgs : EventArgs
    {
        private TorrentManager torrentManager;


        public TorrentManager TorrentManager
        {
            get { return torrentManager; }
            protected set { torrentManager = value; }
        }


        public TorrentEventArgs(TorrentManager manager)
        {
            torrentManager = manager;
        }
    }
}
