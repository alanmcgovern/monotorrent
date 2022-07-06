using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Trackers
{
    public readonly struct ScrapeInfo
    {
        public int Complete { get; }
        public int Downloaded { get; }
        public int Incomplete { get; }

        public ScrapeInfo (int complete, int downloaded, int incomplete)
            => (Complete, Downloaded, Incomplete) = (complete, downloaded, incomplete);
    }
}
