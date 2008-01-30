using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client
{
    public class AnnounceResponseEventArgs : TrackerResponseEventArgs
    {
        public MonoTorrentCollection<Peer> Peers;
        internal TrackerConnectionID TrackerId;


        public AnnounceResponseEventArgs(TrackerConnectionID id)
            : base(id.Tracker, true)
        {
            Peers = new MonoTorrentCollection<Peer>();
            TrackerId = id;
        }
    }
}
