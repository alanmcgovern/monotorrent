using System;
using System.Net;
using MonoTorrent.Client.Connections;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public abstract class PeerListener : Listener
    {
        protected PeerListener(IPEndPoint endpoint)
            : base(endpoint)
        {
        }

        public event EventHandler<NewConnectionEventArgs> ConnectionReceived;

        protected virtual void RaiseConnectionReceived(Peer peer, IConnection connection, TorrentManager manager)
        {
            if (ConnectionReceived != null)
                Toolbox.RaiseAsyncEvent(ConnectionReceived, this,
                    new NewConnectionEventArgs(peer, connection, manager));
        }
    }
}