using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Tracker
{
    public class ScrapeParameters
    {
        private byte[] infoHash;
        private TrackerConnectionID id;

        public TrackerConnectionID Id
        {
            get { return id; }
        }

        public byte[] InfoHash
        {
            get { return infoHash; }
        }

        public ScrapeParameters(TrackerConnectionID id, byte[] infoHash)
        {
            this.id = id;
            this.infoHash = infoHash;
        }
    }
}
