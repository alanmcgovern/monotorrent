namespace MonoTorrent.Client.Tracker
{
    public class ScrapeParameters
    {
        public ScrapeParameters(InfoHash infoHash)
        {
            InfoHash = infoHash;
        }


        public InfoHash InfoHash { get; }
    }
}