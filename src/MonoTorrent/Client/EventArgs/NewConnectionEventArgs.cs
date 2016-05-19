using System;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client
{
    public class NewConnectionEventArgs : TorrentEventArgs
    {
        public NewConnectionEventArgs(Peer peer, IConnection connection, TorrentManager manager)
            : base(manager)
        {
            if (!connection.IsIncoming && manager == null)
                throw new InvalidOperationException(
                    "An outgoing connection must specify the torrent manager it belongs to");

            Connection = connection;
            Peer = peer;
        }

        public IConnection Connection { get; }

        public Peer Peer { get; }
    }
}