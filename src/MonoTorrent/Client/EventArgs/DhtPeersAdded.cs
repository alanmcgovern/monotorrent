namespace MonoTorrent.Client
{
    public class DhtPeersAdded : PeersAddedEventArgs
    {
        public DhtPeersAdded(TorrentManager manager, int peersAdded, int total)
            : base(manager, peersAdded, total)
        {
        }
    }
}