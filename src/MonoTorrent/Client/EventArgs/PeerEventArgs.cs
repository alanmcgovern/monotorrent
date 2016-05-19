namespace MonoTorrent.Client
{
    internal class PeerEventArgs : TorrentEventArgs
    {
        public PeerEventArgs(PeerId peer)
            : base(peer.TorrentManager)
        {
            Peer = peer;
        }

        public PeerId Peer { get; }
    }
}