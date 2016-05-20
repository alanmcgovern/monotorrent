using System;

namespace MonoTorrent.Client
{
    public class TrackerPeersAdded : PeersAddedEventArgs
    {
        public TrackerPeersAdded(TorrentManager manager, int peersAdded, int total,
            Tracker.Tracker tracker)
            : base(manager, peersAdded, total)
        {
            if (tracker == null)
                throw new ArgumentNullException("tracker");

            Tracker = tracker;
        }

        public Tracker.Tracker Tracker { get; }
    }
}