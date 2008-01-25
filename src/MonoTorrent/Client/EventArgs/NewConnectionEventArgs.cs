using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class NewConnectionEventArgs : TorrentEventArgs
    {
        private IConnection connection;
        private TorrentManager manager;
        private Peer peer;

        public IConnection Connection
        {
            get { return connection; }
        }

        public Peer Peer
        {
            get { return peer; }
        }

        public TorrentManager Manager
        {
            get { return manager; }
        }

        public NewConnectionEventArgs(Peer peer, IConnection connection)
            : this(peer, connection, null)
        {
        }

        public NewConnectionEventArgs(Peer peer, IConnection connection, TorrentManager manager)
            : base(null)
        {
            if (!connection.IsIncoming && manager == null)
                throw new InvalidOperationException("An outgoing connection must specify the torrent manager it belongs to");

            this.connection = connection;
            this.peer = peer;
        }
    }
}
