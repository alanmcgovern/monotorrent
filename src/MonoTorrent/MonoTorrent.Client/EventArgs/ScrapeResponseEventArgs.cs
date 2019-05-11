using System;

namespace MonoTorrent.Client.Tracker
{
    public class ScrapeResponseEventArgs : TrackerResponseEventArgs
    {
        public ScrapeResponseEventArgs(Tracker tracker, bool successful)
            : base(tracker, successful)
        {

        }
    }
}
