namespace MonoTorrent.Client.Messages.UdpTracker
{
    public class ScrapeDetails
    {
        public ScrapeDetails(int seeds, int leeches, int complete)
        {
            Complete = complete;
            Leeches = leeches;
            Seeds = seeds;
        }

        public int Complete { get; }

        public int Leeches { get; }

        public int Seeds { get; }
    }
}