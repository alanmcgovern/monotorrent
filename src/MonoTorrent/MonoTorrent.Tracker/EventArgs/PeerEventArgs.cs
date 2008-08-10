using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Tracker;

namespace MonoTorrent.Tracker
{
    public abstract class PeerEventArgs : EventArgs
    {
        private Peer peer;
        private SimpleTorrentManager torrent;

        public Peer Peer
        {
            get { return peer; }
        }

        public SimpleTorrentManager Torrent
        {
            get { return torrent; }
        }

        protected PeerEventArgs(Peer peer, SimpleTorrentManager torrent)
        {
            this.peer = peer;
            this.torrent = torrent;
        }
    }
}
