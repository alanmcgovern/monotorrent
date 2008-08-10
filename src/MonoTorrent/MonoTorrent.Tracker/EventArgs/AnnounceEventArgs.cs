using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Tracker
{
    public class AnnounceEventArgs : PeerEventArgs
    {
        public AnnounceEventArgs(Peer peer, SimpleTorrentManager manager)
            : base(peer, manager)
        {

        }
    }
}
