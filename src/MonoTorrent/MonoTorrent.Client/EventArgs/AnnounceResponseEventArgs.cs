using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public class AnnounceResponseEventArgs : TrackerResponseEventArgs
    {
        public List<Peer> Peers { get; }

        public AnnounceResponseEventArgs(Tracker tracker, bool successful)
            : this(tracker, successful, new List<Peer>())
        {

        }

        public AnnounceResponseEventArgs(Tracker tracker, bool successful, List<Peer> peers)
            : base(tracker, successful)
        {
            Peers = peers;
        }
    }
}
