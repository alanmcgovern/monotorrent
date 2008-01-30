using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public abstract class TorrentEventArgs : EventArgs
    {
        private TorrentManager torrentManager;


        public TorrentManager TorrentManager
        {
            get { return torrentManager; }
            protected set { torrentManager = value; }
        }


        protected TorrentEventArgs(TorrentManager manager)
        {
            torrentManager = manager;
        }
    }
}
