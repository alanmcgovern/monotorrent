using System;
using System.Net;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public abstract class Listener : IListener
    {
        protected Listener(IPEndPoint endpoint)
        {
            Status = ListenerStatus.NotListening;
            Endpoint = endpoint;
        }

        public event EventHandler<EventArgs> StatusChanged;

        public IPEndPoint Endpoint { get; private set; }

        public ListenerStatus Status { get; private set; }

        public void ChangeEndpoint(IPEndPoint endpoint)
        {
            Endpoint = endpoint;
            if (Status == ListenerStatus.Listening)
            {
                Stop();
                Start();
            }
        }

        public abstract void Start();

        public abstract void Stop();

        protected virtual void RaiseStatusChanged(ListenerStatus status)
        {
            Status = status;
            if (StatusChanged != null)
                Toolbox.RaiseAsyncEvent(StatusChanged, this, EventArgs.Empty);
        }
    }
}