using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client.Tracker
{
    public class ScrapeResponseEventArgs : TrackerResponseEventArgs
    {
        public ScrapeResponseEventArgs(Tracker tracker, object state, bool successful)
            : base(tracker, state, successful)
        {

        }
    }
}
