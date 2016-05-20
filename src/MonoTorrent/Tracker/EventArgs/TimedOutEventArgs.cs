namespace MonoTorrent.Tracker
{
    public class TimedOutEventArgs : PeerEventArgs
    {
        public TimedOutEventArgs(Peer peer, SimpleTorrentManager manager)
            : base(peer, manager)
        {
        }
    }
}