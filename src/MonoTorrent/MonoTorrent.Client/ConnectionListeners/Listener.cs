using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public abstract class Listener : IListener
    {
        public event EventHandler<EventArgs> StatusChanged;

        private IPEndPoint endpoint;
        private ListenerStatus status;

        public IPEndPoint Endpoint
        {
            get { return endpoint; }
        }

        public ListenerStatus Status
        {
            get { return status; }
        }


        protected Listener(IPEndPoint endpoint)
        {
            this.status = ListenerStatus.NotListening;
            this.endpoint = endpoint;
        }

        public void ChangeEndpoint(IPEndPoint endpoint)
        {
            this.endpoint = endpoint;
            if (Status == ListenerStatus.Listening)
            {
                Stop();
                Start();
            }
        }

        protected virtual void RaiseStatusChanged(ListenerStatus status)
        {
            this.status = status;
            if (StatusChanged != null)
                Toolbox.RaiseAsyncEvent<EventArgs>(StatusChanged, this, EventArgs.Empty);
        }

        public abstract void Start();

        public abstract void Stop();
    }
}
