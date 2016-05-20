using System.Threading;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    internal class TrackerConnectionID
    {
        public TrackerConnectionID(Tracker tracker, bool trySubsequent, TorrentEvent torrentEvent,
            ManualResetEvent waitHandle)
        {
            Tracker = tracker;
            TrySubsequent = trySubsequent;
            TorrentEvent = torrentEvent;
            WaitHandle = waitHandle;
        }

        public TorrentEvent TorrentEvent { get; }

        public Tracker Tracker { get; }

        internal bool TrySubsequent { get; }

        public ManualResetEvent WaitHandle { get; }
    }
}