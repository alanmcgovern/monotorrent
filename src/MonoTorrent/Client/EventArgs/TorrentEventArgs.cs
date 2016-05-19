using System;

namespace MonoTorrent.Client
{
    public class TorrentEventArgs : EventArgs
    {
        public TorrentEventArgs(TorrentManager manager)
        {
            TorrentManager = manager;
        }


        public TorrentManager TorrentManager { get; protected set; }
    }
}