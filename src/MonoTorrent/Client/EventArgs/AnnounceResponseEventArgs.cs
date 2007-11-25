using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class AnnounceResponseEventArgs : TrackerResponseEventArgs
    {
        public bool Succeeded;
        internal MonoTorrentCollection<Peer> Peers;
        internal TrackerConnectionID TrackerId;


        public AnnounceResponseEventArgs(TrackerConnectionID id)
            : base(id.Tracker)
        {
            Succeeded = true;
            Peers = new MonoTorrentCollection<Peer>();
            TrackerId = id;
        }
    }
}
