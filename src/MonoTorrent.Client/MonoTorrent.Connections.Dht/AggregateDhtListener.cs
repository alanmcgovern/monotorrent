using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using MonoTorrent.Connections;

using ReusableTasks;

namespace MonoTorrent.Connections.Dht
{
    class AggregateDhtListener : IDhtListener
    {
        readonly IList<IDhtListener> listeners;

        public AggregateDhtListener (IList<IDhtListener> listeners)
        {
            this.listeners = listeners ?? throw new ArgumentNullException (nameof (listeners));
            foreach (var listener in listeners) {
                listener.MessageReceived += RaiseMessageReceived;
                listener.StatusChanged += RaiseStatusChanged;
            }
        }

        public event Action<ReadOnlyMemory<byte>, CompactEndPoint>? MessageReceived;

        public event EventHandler<EventArgs>? StatusChanged;

        public IPEndPoint? LocalEndPoint => listeners.FirstOrDefault (t => t.LocalEndPoint != null)?.LocalEndPoint;

        public ListenerStatus Status => listeners.Any (t => t.Status == ListenerStatus.Listening)
            ? ListenerStatus.Listening
            : listeners.Any (t => t.Status == ListenerStatus.PortNotFree)
                ? ListenerStatus.PortNotFree
                : ListenerStatus.NotListening;

        public void Start ()
        {
            foreach (var listener in listeners)
                listener.Start ();
        }

        public void Stop ()
        {
            foreach (var listener in listeners)
                listener.Stop ();
        }

        public ReusableTask SendAsync (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
        {
            var address = new IPAddress (endpoint.Address);
            var listener = listeners.FirstOrDefault (t => t.LocalEndPoint?.AddressFamily == address.AddressFamily)
                ?? listeners.FirstOrDefault ();
            return listener?.SendAsync (buffer, endpoint) ?? ReusableTask.CompletedTask;
        }

        void RaiseMessageReceived (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
            => MessageReceived?.Invoke (buffer, endpoint);

        void RaiseStatusChanged (object? sender, EventArgs e)
            => StatusChanged?.Invoke (this, EventArgs.Empty);
    }
}
