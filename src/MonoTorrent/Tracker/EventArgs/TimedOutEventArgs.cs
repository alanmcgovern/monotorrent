using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Tracker
{
    public class TimedOutEventArgs : PeerEventArgs
    {
        public TimedOutEventArgs(Peer peer, SimpleTorrentManager manager)
            : base(peer, manager)
        {

        }
    }
}
