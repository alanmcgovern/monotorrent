using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using System.Net;
using MonoTorrent.Common;

namespace MonoTorrent.Dht.Listeners
{
    public delegate void MessageReceived(byte[] buffer, IPEndPoint endpoint);

    public abstract class DhtListener : Listener
    {
        public event MessageReceived MessageReceived;

        protected DhtListener(IPEndPoint endpoint)
            : base(endpoint)
        {

        }

        protected virtual void RaiseMessageReceived(byte[] buffer, IPEndPoint endpoint)
        {
            MessageReceived h = MessageReceived;
            if (h != null)
                h(buffer, endpoint);
        }

        public abstract void Send(byte[] buffer, IPEndPoint endpoint);
    }
}
