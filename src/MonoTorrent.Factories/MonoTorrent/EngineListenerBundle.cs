using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;

using MonoTorrent.Connections;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Connections.Peer;
using MonoTorrent.PortForwarding;

using ReusableTasks;

namespace MonoTorrent
{
    public sealed class BoundEndPointsChangedEventArgs : EventArgs
    {
        public ImmutableArray<(Protocol Protocol, IPEndPoint EndPoint)> NewEndPoints { get; }
        public ImmutableArray<(Protocol Protocol, IPEndPoint EndPoint)> OldEndPoints { get; }

        internal BoundEndPointsChangedEventArgs (ImmutableArray<(Protocol Protocol, IPEndPoint EndPoint)> oldEndPoints, ImmutableArray<(Protocol Protocol, IPEndPoint EndPoint)> newEndPoints)
        {
            OldEndPoints = oldEndPoints;
            NewEndPoints = newEndPoints;
        }
    }

    /// <summary>
    /// Owns the physical TCP and UDP listeners for an engine. The object remains stable while its
    /// internal listeners are replaced to apply changes to the configured endpoints or transports.
    /// </summary>
    public sealed class EngineListenerBundle : IDhtListener
    {
        const int MaximumSharedPortAttempts = 100;
        const int FirstEphemeralPort = 49152;
        const int LastEphemeralPortExclusive = 65536;

        sealed class ListenerSet
        {
            public IDhtListener? DhtListener { get; init; }
            public IPeerConnectionListener? TcpListener { get; init; }
            public UdpListener? UdpListener { get; init; }
            public IPeerConnectionListener? UtpListener { get; init; }

            public void Start ()
            {
                TcpListener?.Start ();
                UdpListener?.Start ();
                if (UdpListener?.Status == ListenerStatus.Listening)
                    UtpListener?.Start ();
            }

            public void Stop ()
            {
                UtpListener?.Stop ();
                TcpListener?.Stop ();
                UdpListener?.Stop ();
            }
        }

        bool enableDht;
        bool enableTcp;
        bool enableUtp;
        readonly Factories factories;
        ImmutableArray<IPEndPoint> configuredEndPoints;
        List<ListenerSet> listenerSets = new ();
        EventHandler<EventArgs>? statusChanged;

        /// <summary>The successfully bound physical TCP and UDP endpoints.</summary>
        public ImmutableArray<(Protocol Protocol, IPEndPoint EndPoint)> BoundEndPoints { get; private set; }

        /// <summary>Raised after the set of successfully bound physical endpoints changes.</summary>
        public event EventHandler<BoundEndPointsChangedEventArgs>? BoundEndPointsChanged;

        internal event EventHandler<PeerConnectionEventArgs>? ConnectionReceived;
        internal event Action<ReadOnlyMemory<byte>, CompactEndPoint>? MessageReceived;

        internal EngineListenerBundle (
            IEnumerable<IPEndPoint> listenEndPoints,
            bool enableTcp,
            bool enableUtp,
            bool enableDht,
            Factories factories)
        {
            this.factories = factories ?? throw new ArgumentNullException (nameof (factories));
            BoundEndPoints = ImmutableArray<(Protocol Protocol, IPEndPoint EndPoint)>.Empty;
            UpdateConfiguration (listenEndPoints, enableTcp, enableUtp, enableDht);
        }

        ListenerSet CreateListenerSet (IPEndPoint endPoint)
        {
            var tcp = enableTcp ? factories.CreatePeerConnectionListener (endPoint) : null;
            var udp = enableUtp || enableDht ? factories.CreateUdpListener (endPoint) : null;
            var utp = enableUtp && udp is not null ? factories.CreateUtpPeerConnectionListener (udp) : null;
            var dht = enableDht && udp is not null ? factories.CreateDhtListener (udp) : null;

            if (tcp is not null) {
                tcp.ConnectionReceived += RaiseConnectionReceived;
                tcp.StatusChanged += RaiseStatusChanged;
            }
            if (utp is not null) {
                utp.ConnectionReceived += RaiseConnectionReceived;
                utp.StatusChanged += RaiseStatusChanged;
            }
            if (udp is not null)
                udp.StatusChanged += RaiseStatusChanged;
            if (dht is not null)
                dht.MessageReceived += RaiseMessageReceived;

            return new ListenerSet {
                DhtListener = dht,
                TcpListener = tcp,
                UdpListener = udp,
                UtpListener = utp,
            };
        }

        static IPEndPoint WithRandomPort (IPEndPoint endPoint)
            => new IPEndPoint (endPoint.Address, Random.Shared.Next (FirstEphemeralPort, LastEphemeralPortExclusive));

        static bool IsListening (IListener? listener)
            => listener?.Status == ListenerStatus.Listening;

        ImmutableArray<(Protocol Protocol, IPEndPoint EndPoint)> GetBoundEndPoints ()
            => ImmutableArray.CreateRange (listenerSets
                .SelectMany (t => new[] {
                    (Protocol.Tcp, t.TcpListener?.LocalEndPoint),
                    (Protocol.Udp, t.UdpListener?.LocalEndPoint),
                })
                .Where (t => t.Item2 is not null)
                .Select (t => (t.Item1, t.Item2!))
                .Distinct ());

        void UpdateBoundEndPoints ()
        {
            var newEndPoints = GetBoundEndPoints ();
            if (BoundEndPoints.SequenceEqual (newEndPoints))
                return;

            var oldEndPoints = BoundEndPoints;
            BoundEndPoints = newEndPoints;
            BoundEndPointsChanged?.Invoke (this, new BoundEndPointsChangedEventArgs (oldEndPoints, newEndPoints));
        }

        void UpdateConfiguration (IEnumerable<IPEndPoint> listenEndPoints, bool enableTcp, bool enableUtp, bool enableDht)
        {
            configuredEndPoints = ImmutableArray.CreateRange (listenEndPoints ?? throw new ArgumentNullException (nameof (listenEndPoints)));
            this.enableTcp = enableTcp;
            this.enableUtp = enableUtp;
            this.enableDht = enableDht;
            listenerSets = configuredEndPoints.Select (CreateListenerSet).ToList ();
        }

        /// <summary>Replaces the internal listeners with ones matching the new configuration.</summary>
        public void Update (IEnumerable<IPEndPoint> listenEndPoints, bool enableTcp, bool enableUtp, bool enableDht)
        {
            bool wasListening = listenerSets.Any (t => IsListening (t.TcpListener) || IsListening (t.UdpListener));
            Stop ();
            UpdateConfiguration (listenEndPoints, enableTcp, enableUtp, enableDht);
            if (wasListening)
                Start ();
        }

        /// <summary>
        /// Starts the physical listeners. For a port-zero endpoint that needs both protocols, retries
        /// random ports until both listeners bind or the retry limit is reached.
        /// </summary>
        public void Start ()
        {
            for (int i = 0; i < listenerSets.Count; i++) {
                var current = listenerSets[i];
                var configuredEndPoint = configuredEndPoints[i];
                bool requireSharedPort = configuredEndPoint.Port == 0 && enableTcp && (enableUtp || enableDht);
                if (!requireSharedPort) {
                    current.Start ();
                    continue;
                }

                for (int attempt = 0; attempt < MaximumSharedPortAttempts; attempt++) {
                    var candidate = CreateListenerSet (WithRandomPort (configuredEndPoint));
                    candidate.Start ();

                    if (IsListening (candidate.TcpListener) && IsListening (candidate.UdpListener)
                        || attempt == MaximumSharedPortAttempts - 1) {
                        current.Stop ();
                        listenerSets[i] = candidate;
                        break;
                    }

                    candidate.Stop ();
                }
            }
            UpdateBoundEndPoints ();
        }

        /// <summary>Stops all TCP, UDP, and uTP listeners owned by this bundle.</summary>
        public void Stop ()
        {
            foreach (var listenerSet in listenerSets)
                listenerSet.Stop ();
            UpdateBoundEndPoints ();
        }

        internal IPeerConnection? CreateUtpPeerConnection (IPAddress address, int port, ushort connectionIdReceive)
        {
            var listener = listenerSets
                .Select (t => t.UtpListener)
                .FirstOrDefault (t => t?.PreferredLocalEndPoint.AddressFamily == address.AddressFamily);
            return listener is null ? null : factories.CreateUtpPeerConnection (listener, new IPEndPoint (address, port), connectionIdReceive);
        }

        IPEndPoint? ISocketListener.LocalEndPoint
            => listenerSets.Select (t => t.DhtListener?.LocalEndPoint).FirstOrDefault (t => t is not null);

        ListenerStatus IListener.Status
            => listenerSets.Any (t => t.DhtListener?.Status == ListenerStatus.Listening)
                ? ListenerStatus.Listening
                : listenerSets.Any (t => t.DhtListener?.Status == ListenerStatus.PortNotFree)
                    ? ListenerStatus.PortNotFree
                    : ListenerStatus.NotListening;

        event Action<ReadOnlyMemory<byte>, CompactEndPoint>? ISocketMessageListener.MessageReceived {
            add => MessageReceived += value;
            remove => MessageReceived -= value;
        }

        event EventHandler<EventArgs>? IListener.StatusChanged {
            add => statusChanged += value;
            remove => statusChanged -= value;
        }

        void IListener.Start ()
            => Start ();

        void IListener.Stop ()
            => Stop ();

        ReusableTask ISocketMessageListener.SendAsync (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
        {
            var address = new IPAddress (endpoint.Address);
            var listener = listenerSets.FirstOrDefault (t => t.DhtListener?.LocalEndPoint?.AddressFamily == address.AddressFamily)?.DhtListener
                ?? listenerSets.FirstOrDefault (t => t.DhtListener is not null)?.DhtListener;
            return listener?.SendAsync (buffer, endpoint) ?? ReusableTask.CompletedTask;
        }

        void RaiseConnectionReceived (object? sender, PeerConnectionEventArgs e)
            => ConnectionReceived?.Invoke (this, e);

        void RaiseMessageReceived (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
            => MessageReceived?.Invoke (buffer, endpoint);

        void RaiseStatusChanged (object? sender, EventArgs e)
            => statusChanged?.Invoke (this, EventArgs.Empty);
    }
}
