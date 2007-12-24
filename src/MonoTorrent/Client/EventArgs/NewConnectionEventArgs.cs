using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class NewConnectionEventArgs : TorrentEventArgs
    {
        private Peer peer;
        private PeerConnectionBase connection;

        public PeerConnectionBase Connection
        {
            get { return connection; }
        }

        public Peer Peer
        {
            get { return peer; }
        }
        
        public NewConnectionEventArgs(Peer peer, PeerConnectionBase connection)
            : base(null)
        {
            this.connection = connection;
            this.peer = peer;
        }
    }
}
