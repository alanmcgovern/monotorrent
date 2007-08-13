using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    internal class NewConnectionEventArgs : TorrentEventArgs
    {
        private Peer peer;
        
        public Peer Peer
        {
            get { return peer; }
        }
        
        public NewConnectionEventArgs(Peer peer)
            : base(null)
        {
            this.peer = peer;
        }
    }
}
