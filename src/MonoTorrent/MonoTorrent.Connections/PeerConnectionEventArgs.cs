using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Connections
{
    public class PeerConnectionEventArgs : EventArgs
    {
        public IPeerConnection Connection { get; }

        public InfoHash InfoHash { get; }

        public PeerConnectionEventArgs (IPeerConnection connection, InfoHash infoHash)
        {
            if (!connection.IsIncoming && infoHash == null)
                throw new InvalidOperationException ("An outgoing connection must specify the torrent manager it belongs to");

            Connection = connection;
            InfoHash = infoHash;
        }
    }

}
