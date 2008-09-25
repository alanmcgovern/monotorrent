using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Connections;
using MonoTorrent.Common;
using System.Net.Sockets;

namespace MonoTorrent.Client
{
    public abstract class PeerListener : Listener
    {
        public event EventHandler<NewConnectionEventArgs> ConnectionReceived;

        protected PeerListener(IPEndPoint endpoint)
            : base(endpoint)
        {

        }

        protected virtual void RaiseConnectionReceived(Peer peer, IConnection connection, TorrentManager manager)
        {
            if (ConnectionReceived != null)
                Toolbox.RaiseAsyncEvent<NewConnectionEventArgs>(ConnectionReceived, this, new NewConnectionEventArgs(peer, connection, manager));
        }
    }
}
