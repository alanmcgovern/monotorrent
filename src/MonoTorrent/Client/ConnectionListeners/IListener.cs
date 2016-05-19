using System;
using System.Net;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public interface IListener
    {
        IPEndPoint Endpoint { get; }
        ListenerStatus Status { get; }
        event EventHandler<EventArgs> StatusChanged;

        void ChangeEndpoint(IPEndPoint port);
        void Start();
        void Stop();
    }
}