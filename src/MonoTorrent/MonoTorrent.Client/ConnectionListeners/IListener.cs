using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public interface IListener
    {
        event EventHandler<EventArgs> StatusChanged;

        IPEndPoint Endpoint { get; }
        ListenerStatus Status { get; }

        void ChangeEndpoint(IPEndPoint port);
        void Start();
        void Stop();
    }
}
