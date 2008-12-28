using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Tracker
{
    public class ScrapeParameters
    {
        private byte[] infoHash;


        public byte[] InfoHash
        {
            get { return infoHash; }
        }

        public ScrapeParameters(byte[] infoHash)
        {
            this.infoHash = infoHash;
        }
    }
}
