using System.Collections.Generic;

namespace MonoTorrent.Client.Tracker
{
    public class AnnounceResponseEventArgs : TrackerResponseEventArgs
    {
        public AnnounceResponseEventArgs(Tracker tracker, object state, bool successful)
            : this(tracker, state, successful, new List<Peer>())
        {
        }

        public AnnounceResponseEventArgs(Tracker tracker, object state, bool successful, List<Peer> peers)
            : base(tracker, state, successful)
        {
            Peers = peers;
        }

        public List<Peer> Peers { get; }
    }
}