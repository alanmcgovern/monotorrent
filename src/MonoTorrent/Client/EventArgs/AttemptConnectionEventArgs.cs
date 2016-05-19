using System;

namespace MonoTorrent.Client
{
    public class AttemptConnectionEventArgs : EventArgs
    {
        public AttemptConnectionEventArgs(Peer peer)
        {
            Peer = peer;
        }

        public bool BanPeer { get; set; }

        public Peer Peer { get; }
    }
}