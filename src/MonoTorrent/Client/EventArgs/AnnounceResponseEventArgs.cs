using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class AnnounceResponseEventArgs : TrackerResponseEventArgs
    {
        internal MonoTorrentCollection<Peer> Peers;
        internal TrackerConnectionID TrackerId;


        public AnnounceResponseEventArgs(TrackerConnectionID id)
            : base(id.Tracker, true)
        {
            Peers = new MonoTorrentCollection<Peer>();
            TrackerId = id;
        }
    }
}
