namespace MonoTorrent.Client
{
    public class LocalPeersAdded : PeersAddedEventArgs
    {
        public LocalPeersAdded(TorrentManager manager, int peersAdded, int total)
            : base(manager, peersAdded, total)
        {
        }
    }
}