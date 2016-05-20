using System;

namespace MonoTorrent.Tracker
{
    public abstract class PeerEventArgs : EventArgs
    {
        protected PeerEventArgs(Peer peer, SimpleTorrentManager torrent)
        {
            Peer = peer;
            Torrent = torrent;
        }

        public Peer Peer { get; }

        public SimpleTorrentManager Torrent { get; }
    }
}