using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

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
    //   Acceptor   uses   conn_id_send = conn_id_recv + 1   in every reply.
    //   Acceptor   uses   conn_id_recv = conn_id_recv (same) to demux inbound.
    //
    // Connection map key: (remote EndPoint, conn_id_recv of the initiator).
    //   Acceptor side  – keyed by syn.ConnectionId (the initiator's conn_id_recv).
    //   Initiator side – keyed by the random conn_id_recv we chose for the SYN.
    //   Every packet sent by the remote peer carries conn_id_send, which is
    //   conn_id_recv + 1, so RouteToExisting subtracts 1 before the lookup.
    // =========================================================================

    public sealed class UtpPeerConnectionListener : SocketListener, IPeerConnectionListener
    {
        internal const byte UTP_VERSION = 1;
        internal const uint INITIAL_WINDOW = 1 << 18;   // 256 kB

        public event EventHandler<PeerConnectionEventArgs>? ConnectionReceived;

        readonly ConcurrentDictionary<(EndPoint remoteEndpoint, ushort remoteConnectionReceiveId), UtpPeerConnection> _connections = new ();

        public Channel<(UtpPacket packet, UtpPeerConnection connection, IPEndPoint remoteEndPoint)> SendQueue = Channel.CreateUnbounded<(UtpPacket, UtpPeerConnection, IPEndPoint)> ();

        public UtpPeerConnectionListener (IPEndPoint preferredLocalEndPoint)
            : this (preferredLocalEndPoint, StopwatchUtpClock.Instance)
        {
        }

        internal UtpPeerConnectionListener (IPEndPoint preferredLocalEndPoint, IUtpClock clock)
            : base (preferredLocalEndPoint)
        {
            PreferredLocalEndPoint = preferredLocalEndPoint;
            Clock = clock;
        }

        internal IUtpClock Clock { get; }

        protected override void Start (CancellationToken token)
        {
            base.Start (token);

            var socket = new Socket (
                PreferredLocalEndPoint.AddressFamily,
                SocketType.Dgram,
                ProtocolType.Udp);

            // Suppress Windows ICMP port-unreachable errors killing the loop.
            if (OperatingSystem.IsWindows ()) {
                const int SIO_UDP_CONNRESET = unchecked((int) 0x9800000C);
                socket.IOControl (SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }

            socket.Bind (PreferredLocalEndPoint);
            LocalEndPoint = (IPEndPoint?) socket.LocalEndPoint;

            // Start both send and receive loops
            SendLoopAsync (socket, token);
            ReceiveLoopAsync (socket, token);
        }

        async void SendLoopAsync (Socket socket, CancellationToken token)
        {
            try {
                await foreach (var (pkt, connection, remote) in SendQueue.Reader.ReadAllAsync (token)) {
                    try {
                        var packet = pkt;
                        connection.PrepareForSend (ref packet);
                        await socket.SendToAsync (packet.AsMemory (), SocketFlags.None, remote, token);
                    } catch (OperationCanceledException) {
                        return;
                    } catch (SocketException) when (!token.IsCancellationRequested) {
                        // Keep looping if one socket is closed.
                    }
                }
            } catch (OperationCanceledException) {
                // Listener stopped.
            }
        }

        async void ReceiveLoopAsync (Socket socket, CancellationToken token)
        {
            var buffer = new byte[65_536];

            using var closer = token.Register (() => socket.Close ());
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
                } catch (SocketException) when (!token.IsCancellationRequested) {
                    continue;
                } catch (ObjectDisposedException) {
                    return;
                }
            }
        }

        void ProcessDatagram (IPEndPoint remote, byte[] owned)
        {
            var pkt = new UtpPacket (owned);

            if (pkt.Version != UTP_VERSION)
                return;

            switch (pkt.Type) {
                case PacketType.Syn:
                    HandleSyn (remote, pkt);
                    break;

                case PacketType.Data:
                case PacketType.State:
                case PacketType.Fin:
                case PacketType.Reset:
                    RouteToExisting (remote, pkt);
                    break;
            }
        }

        async void HandleSyn (IPEndPoint remote, UtpPacket syn)
        {
            ushort initiatorConnIdRecv = syn.ConnectionId;
            ushort ourConnIdSend = (ushort) (initiatorConnIdRecv + 1);

            var key = (remote, initiatorConnIdRecv);

            // Idempotent on retransmits – resend the ST_STATE.
            if (_connections.TryGetValue (key, out var existing)) {
                await existing.SendSynAck (syn.SequenceNumber);
                return;
            }

            var connection = new UtpPeerConnection (
                sendingChannel: SendQueue.Writer,
                remote: remote,
                connIdSend: ourConnIdSend,
                connIdRecv: initiatorConnIdRecv,
                initialAckNumber: syn.SequenceNumber,
                clock: Clock);

            if (!_connections.TryAdd (key, connection))
                return;

            await connection.SendSynAck (syn.SequenceNumber);

            ConnectionReceived?.Invoke (this, new PeerConnectionEventArgs (connection, null));
        }

        void RouteToExisting (EndPoint remote, UtpPacket pkt)
        {
            ushort initiatorConnIdRecv = (ushort) (pkt.ConnectionId - 1);
            if (_connections.TryGetValue ((remote, initiatorConnIdRecv), out var conn))
                conn.Receive (pkt);
        }

        internal bool TryRegisterOutgoing (UtpPeerConnection connection)
            => _connections.TryAdd ((connection.EndPoint, connection.ConnectionIdReceive), connection);
    }
}
