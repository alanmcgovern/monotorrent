using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client.Tracker
{
    class UdpTracker : Tracker
    {
        private string announceUrl;
        public UdpTracker(string announceUrl)
        {
            this.announceUrl = announceUrl;
        }

        public override WaitHandle Announce(AnnounceParameters parameters)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override WaitHandle Scrape(byte[] infohash, TrackerConnectionID id)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
