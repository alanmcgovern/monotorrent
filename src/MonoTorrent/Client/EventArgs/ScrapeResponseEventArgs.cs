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