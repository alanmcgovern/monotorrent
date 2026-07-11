using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using MonoTorrent.Logging;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer.Utp
{
    // =========================================================================
    // UtpPeerConnectionListener
    //
    // Listens on a single UDP socket for incoming uTP connections (BEP 29).
    // When a ST_SYN is received it completes the handshake (replies ST_STATE)
    // then raises ConnectionReceived with a UtpPeerConnection.
    //
    // Connection-ID convention (BEP 29 §connection id):
    //   Initiator  picks  conn_id_recv  (sent in the SYN header).
    //   Initiator  uses   conn_id_send = conn_id_recv + 1   after the SYN.
    //   Acceptor   uses   conn_id_send = conn_id_recv       in every reply.
    //   Acceptor   uses   conn_id_recv = conn_id_recv + 1   to demux inbound.
    //
    // Connection map key: (remote EndPoint, local conn_id_recv).
    //   Acceptor side  – keyed by syn.ConnectionId + 1.
    //   Initiator side – keyed by the random conn_id_recv we chose for the SYN.
    // =========================================================================

    public sealed class UtpPeerConnectionListener : SocketListener, IPeerConnectionListener
    {
        internal const byte UTP_VERSION = 1;
        internal const uint INITIAL_WINDOW = 1 << 18;   // 256 kB

        public event EventHandler<PeerConnectionEventArgs>? ConnectionReceived;

        sealed class RegisteredConnection
        {
            public RegisteredConnection (UtpPeerConnection connection, uint lastActivityMicroseconds, ushort? incomingSynSequenceNumber = null)
            {
                Connection = connection;
                LastActivityMicroseconds = lastActivityMicroseconds;
                IncomingSynSequenceNumber = incomingSynSequenceNumber;
            }

            public UtpPeerConnection Connection { get; }
            public ushort? IncomingSynSequenceNumber { get; }
            public uint LastActivityMicroseconds { get; set; }
        }

        readonly struct RecentResetKey : IEquatable<RecentResetKey>
        {
            public RecentResetKey (IPEndPoint remote, ushort connectionId, ushort sequenceNumber)
            {
                Remote = remote;
                ConnectionId = connectionId;
                SequenceNumber = sequenceNumber;
            }

            public IPEndPoint Remote { get; }
            public ushort ConnectionId { get; }
            public ushort SequenceNumber { get; }

            public bool Equals (RecentResetKey other)
                => EqualityComparer<IPEndPoint>.Default.Equals (Remote, other.Remote)
                    && ConnectionId == other.ConnectionId
                    && SequenceNumber == other.SequenceNumber;

            public override bool Equals (object? obj)
                => obj is RecentResetKey other && Equals (other);

            public override int GetHashCode ()
                => HashCode.Combine (Remote, ConnectionId, SequenceNumber);
        }

        static readonly ILogger Logger = LoggerFactory.Create (nameof (UtpPeerConnectionListener));
        static readonly TimeSpan StaleConnectionTimeout = TimeSpan.FromMinutes (2);
        static readonly TimeSpan StaleConnectionPruneInterval = TimeSpan.FromSeconds (10);
        const int MaxRecentResetEntries = 256;
        const uint RecentResetLifetimeMicroseconds = 10_000_000;

        readonly ConcurrentDictionary<(EndPoint remoteEndpoint, ushort remoteConnectionReceiveId), RegisteredConnection> _connections = new ();
        readonly Dictionary<RecentResetKey, uint> recentResets = new ();
        readonly object recentResetsLocker = new ();
        readonly object backgroundTasksLocker = new ();
        readonly List<Task> backgroundTasks = new ();

        public Channel<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> SendQueue = Channel.CreateUnbounded<(UtpPacket, UtpPeerConnection?, IPEndPoint)> ();

        public UtpPeerConnectionListener (IPEndPoint preferredLocalEndPoint)
            : this (preferredLocalEndPoint, null)
        {
        }

        public UtpPeerConnectionListener (IPEndPoint preferredLocalEndPoint, UtpTransportSettings? transportSettings)
            : this (preferredLocalEndPoint, StopwatchUtpClock.Instance, transportSettings)
        {
        }

        internal UtpPeerConnectionListener (IPEndPoint preferredLocalEndPoint, IUtpClock clock, UtpTransportSettings? transportSettings = null)
            : base (preferredLocalEndPoint)
        {
            PreferredLocalEndPoint = preferredLocalEndPoint;
            Clock = clock;
            TransportSettings = UtpTransportSettings.Create (transportSettings);
            Scheduler = new UtpConnectionScheduler (clock);
        }

        internal IUtpClock Clock { get; }

        internal UtpTransportSettings TransportSettings { get; }

        internal UtpConnectionScheduler Scheduler { get; }

        protected override void Start (CancellationToken token)
        {
            base.Start (token);

            var socket = new Socket (
                PreferredLocalEndPoint.AddressFamily,
                SocketType.Dgram,
                ProtocolType.Udp);

            ConfigureSocketBuffers (socket, TransportSettings);

            // Suppress Windows ICMP port-unreachable errors killing the loop.
            if (OperatingSystem.IsWindows ()) {
                const int SIO_UDP_CONNRESET = unchecked((int) 0x9800000C);
                socket.IOControl (SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }

            socket.Bind (PreferredLocalEndPoint);
            LocalEndPoint = (IPEndPoint?) socket.LocalEndPoint;

            token.Register (() => {
                try {
                    socket.Close ();
                } catch {
                }
            });

            TrackBackgroundTask (SendLoopAsync (socket, token));
            TrackBackgroundTask (ReceiveLoopAsync (socket, token));
            TrackBackgroundTask (PruneStaleConnectionsLoopAsync (token));
        }

        internal static void ConfigureSocketBuffers (Socket socket, UtpTransportSettings settings)
        {
            socket.ReceiveBufferSize = settings.SocketReceiveBufferBytes;
            socket.SendBufferSize = settings.SocketSendBufferBytes;
        }

        void TrackBackgroundTask (Task task)
        {
            lock (backgroundTasksLocker)
                backgroundTasks.Add (task);

            _ = task.ContinueWith (completed => {
                lock (backgroundTasksLocker)
                    backgroundTasks.Remove (completed);

                if (completed.Exception != null)
                    Logger.Error ($"uTP listener background task failed: {completed.Exception.GetBaseException ().Message}");
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        internal Task[] BackgroundTasksForTests {
            get {
                lock (backgroundTasksLocker)
                    return backgroundTasks.ToArray ();
            }
        }

        async Task SendLoopAsync (Socket socket, CancellationToken token)
        {
            try {
                await foreach (var (pkt, connection, remote) in SendQueue.Reader.ReadAllAsync (token)) {
                    try {
                        var packet = pkt;
                        if (connection == null) {
                            packet.SetTimestamp (Clock);
                            packet.TimestampDiff = 0;
                        } else {
                            connection.PrepareForSend (ref packet);
                        }
                        await socket.SendToAsync (packet.AsMemory (), SocketFlags.None, remote, token);
                    } catch (OperationCanceledException) {
                        return;
                    } catch (SocketException ex) when (!token.IsCancellationRequested) {
                        Logger.Debug ($"uTP send failed: {ex.SocketErrorCode}");
                    }
                }
            } catch (OperationCanceledException) {
            } catch (ObjectDisposedException) {
            } catch (Exception ex) {
                Logger.Error ($"uTP send loop failed: {ex.Message}");
            }
        }

        async Task ReceiveLoopAsync (Socket socket, CancellationToken token)
        {
            var buffer = new byte[65_536];

            var endpoint = new IPEndPoint (PreferredLocalEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);

            while (!token.IsCancellationRequested) {
                try {
                    var received = await socket.ReceiveFromAsync (buffer, endpoint, token);

                    // If the packet is too small *or* if the version header doesn't match, drop the packet immediately.
                    if (received.ReceivedBytes < UtpPacket.HeaderSize || new UtpPacket (buffer).Version != UTP_VERSION)
                        continue;

                    var owned = new byte[received.ReceivedBytes];
                    buffer.AsSpan (0, owned.Length).CopyTo (owned);
                    ProcessDatagram ((IPEndPoint) received.RemoteEndPoint, owned);
                } catch (OperationCanceledException) {
                    return;
                } catch (SocketException ex) when (!token.IsCancellationRequested) {
                    Logger.Debug ($"uTP receive failed: {ex.SocketErrorCode}");
                    continue;
                } catch (ObjectDisposedException) {
                    return;
                } catch (Exception ex) {
                    Logger.Error ($"uTP receive loop failed: {ex.Message}");
                    return;
                }
            }
        }

        async Task PruneStaleConnectionsLoopAsync (CancellationToken token)
        {
            using var timer = new PeriodicTimer (StaleConnectionPruneInterval);
            try {
                while (await timer.WaitForNextTickAsync (token))
                    PruneStaleConnections ();
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            }
        }

        internal void ProcessDatagram (IPEndPoint remote, byte[] owned)
        {
            var pkt = new UtpPacket (owned);

            if (pkt.Version != UTP_VERSION)
                return;

            switch (pkt.Type) {
                case PacketType.Syn:
                    TrackBackgroundTask (HandleSynAsync (remote, pkt));
                    break;

                case PacketType.Data:
                case PacketType.State:
                case PacketType.Fin:
                case PacketType.Reset:
                    RouteToExisting (remote, pkt);
                    break;
            }
        }

        async Task HandleSynAsync (IPEndPoint remote, UtpPacket syn)
        {
            ushort initiatorConnIdRecv = syn.ConnectionId;
            ushort ourConnIdRecv = (ushort) (initiatorConnIdRecv + 1);

            var key = (remote, ourConnIdRecv);

            // Idempotent only for an exact retransmission of the original SYN.
            if (_connections.TryGetValue (key, out var existing)) {
                if (existing.Connection.IsClosedOrReset) {
                    _connections.TryRemove (key, out _);
                } else if (existing.IncomingSynSequenceNumber == syn.SequenceNumber) {
                    existing.LastActivityMicroseconds = Clock.Microseconds;
                    await existing.Connection.SendSynAck (syn.SequenceNumber);
                    return;
                } else {
                    Logger.Debug ($"Ignored colliding uTP SYN from {remote} / {initiatorConnIdRecv}");
                    return;
                }
            }

            if (IncomingConnectionCount >= TransportSettings.MaxIncomingSynConnections) {
                Logger.Debug ($"Dropped uTP SYN from {remote} / {initiatorConnIdRecv}: incoming SYN capacity reached");
                return;
            }

            var connection = new UtpPeerConnection (
                sendingChannel: SendQueue.Writer,
                remote: remote,
                connIdSend: initiatorConnIdRecv,
                connIdRecv: ourConnIdRecv,
                initialAckNumber: syn.SequenceNumber,
                clock: Clock,
                listener: this,
                transportSettings: TransportSettings);

            connection.InitializeFromSyn (syn);

            if (!_connections.TryAdd (key, new RegisteredConnection (connection, Clock.Microseconds, syn.SequenceNumber))) {
                connection.Dispose ();
                if (_connections.TryGetValue (key, out existing) && existing.IncomingSynSequenceNumber == syn.SequenceNumber) {
                    existing.LastActivityMicroseconds = Clock.Microseconds;
                    await existing.Connection.SendSynAck (syn.SequenceNumber);
                } else {
                    Logger.Debug ($"Ignored colliding uTP SYN from {remote} / {initiatorConnIdRecv}");
                }
                return;
            }

            await connection.SendSynAck (syn.SequenceNumber);

            ConnectionReceived?.Invoke (this, new PeerConnectionEventArgs (connection, null));
        }

        void RouteToExisting (IPEndPoint remote, UtpPacket pkt)
        {
            var key = pkt.Type == PacketType.Reset
                ? FindResetKey (remote, pkt.ConnectionId)
                : (remote, pkt.ConnectionId);

            if (_connections.TryGetValue (key, out var registration)) {
                var conn = registration.Connection;
                if (!conn.IsClosedOrReset && conn.IsValidPacketForCurrentState (pkt)) {
                    registration.LastActivityMicroseconds = Clock.Microseconds;
                    conn.Receive (pkt);
                    return;
                }

                if (!conn.IsClosedOrReset && conn.IsHarmlessStalePacket (pkt)) {
                    registration.LastActivityMicroseconds = Clock.Microseconds;
                    return;
                }

                if (conn.IsClosedOrReset)
                    _connections.TryRemove (key, out _);
            }

            SendResetForUnknownNonSyn (remote, pkt);
        }

        (EndPoint remote, ushort connectionIdReceive) FindResetKey (IPEndPoint remote, ushort connectionId)
        {
            var key = ((EndPoint) remote, connectionId);
            if (_connections.TryGetValue (key, out _))
                return key;

            key = (remote, unchecked((ushort) (connectionId + 1)));
            if (_connections.TryGetValue (key, out var plusOne) && plusOne.Connection.ConnectionIdSend == connectionId)
                return key;

            key = (remote, unchecked((ushort) (connectionId - 1)));
            if (_connections.TryGetValue (key, out var minusOne) && minusOne.Connection.ConnectionIdSend == connectionId)
                return key;

            return ((EndPoint) remote, connectionId);
        }

        internal bool TryRegisterOutgoing (UtpPeerConnection connection)
            => _connections.TryAdd ((connection.EndPoint, connection.ConnectionIdReceive), new RegisteredConnection (connection, Clock.Microseconds));

        internal void Unregister (UtpPeerConnection connection)
            => _connections.TryRemove ((connection.EndPoint, connection.ConnectionIdReceive), out _);

        internal bool IsRegistered (UtpPeerConnection connection)
            => _connections.ContainsKey ((connection.EndPoint, connection.ConnectionIdReceive));

        internal bool ApplyMtuFeedback (IPEndPoint remote, ushort connectionId, int nextHopMtu)
        {
            var key = ((EndPoint) remote, connectionId);
            if (!_connections.TryGetValue (key, out var registration))
                key = FindResetKey (remote, connectionId);

            if (!_connections.TryGetValue (key, out registration))
                return false;

            registration.Connection.ApplyMtuFeedback (nextHopMtu);
            return true;
        }

        internal Task ProcessScheduledEventsForTests ()
            => Scheduler.ProcessDueEventsForTests ();

        internal static int RecentResetCapacityForTests => MaxRecentResetEntries;

        internal int RegisteredConnectionCount => _connections.Count;

        int IncomingConnectionCount {
            get {
                int count = 0;
                foreach (var connection in _connections.Values) {
                    if (connection.Connection.IsIncoming && !connection.Connection.IsClosedOrReset)
                        count++;
                }
                return count;
            }
        }

        internal void PruneStaleConnections ()
        {
            var now = Clock.Microseconds;
            var staleAfter = (uint) StaleConnectionTimeout.TotalMicroseconds;
            foreach (var pair in _connections.ToArray ()) {
                var connection = pair.Value.Connection;
                if (connection.IsClosedOrReset || unchecked(now - pair.Value.LastActivityMicroseconds) >= staleAfter) {
                    if (_connections.TryRemove (pair.Key, out _)) {
                        Logger.Debug ($"Pruned stale uTP connection {pair.Key.remoteEndpoint} / {pair.Key.remoteConnectionReceiveId}");
                        connection.Dispose ();
                    }
                }
            }
        }

        bool ShouldSendResetForUnknownNonSyn (IPEndPoint remote, UtpPacket received)
        {
            if (received.Type == PacketType.Reset || received.Type == PacketType.Syn)
                return false;

            var now = Clock.Microseconds;
            var key = new RecentResetKey (remote, received.ConnectionId, received.SequenceNumber);
            lock (recentResetsLocker) {
                List<RecentResetKey>? expired = null;
                foreach (var entry in recentResets) {
                    if (unchecked(now - entry.Value) >= RecentResetLifetimeMicroseconds)
                        (expired ??= new List<RecentResetKey> ()).Add (entry.Key);
                }

                if (expired != null) {
                    foreach (var expiredKey in expired)
                        recentResets.Remove (expiredKey);
                }

                if (recentResets.ContainsKey (key))
                    return false;

                if (recentResets.Count >= MaxRecentResetEntries)
                    return false;

                recentResets[key] = now;
                return true;
            }
        }

        void SendResetForUnknownNonSyn (IPEndPoint remote, UtpPacket received)
        {
            if (!ShouldSendResetForUnknownNonSyn (remote, received))
                return;

            SendReset (remote, received);
        }

        void SendReset (IPEndPoint remote, UtpPacket received)
        {
            if (received.Type == PacketType.Reset)
                return;

            var reset = new UtpPacket (new byte[UtpPacket.HeaderSize]) {
                Type = PacketType.Reset,
                Version = UTP_VERSION,
                Extension = 0,
                ConnectionId = received.ConnectionId,
                WindowSize = INITIAL_WINDOW,
                SequenceNumber = 0,
                AckNumber = received.SequenceNumber
            };
            SendQueue.Writer.TryWrite ((reset, null, remote));
        }
    }
}
