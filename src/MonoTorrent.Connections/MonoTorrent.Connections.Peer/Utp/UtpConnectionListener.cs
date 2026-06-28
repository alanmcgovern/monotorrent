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
    // =========================================================================

    public sealed class UtpPeerConnectionListener : SocketListener
    {
        internal const byte UTP_VERSION = 1;
        internal const uint INITIAL_WINDOW = 1 << 18;   // 256 kB

        public event EventHandler<PeerConnectionEventArgs>? ConnectionReceived;

        readonly ConcurrentDictionary<(EndPoint, ushort), UtpPeerConnection> _connections = new ();
        readonly ConcurrentDictionary<(EndPoint, ushort), UtpPacket> inFlightPackets = new ();

        Channel<(UtpPacket, IPEndPoint)> SendQueue = Channel.CreateUnbounded<(UtpPacket, IPEndPoint)> ();

        public UtpPeerConnectionListener (IPEndPoint preferredLocalEndPoint)
            : base (preferredLocalEndPoint)
        {
            PreferredLocalEndPoint = preferredLocalEndPoint;
        }

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
            ReceiveLoopAsync (socket, token);
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
                    ProcessDatagram (socket, (IPEndPoint) received.RemoteEndPoint, owned);
                } catch (OperationCanceledException) {
                    return;
                } catch (SocketException) when (!token.IsCancellationRequested) {
                    continue;
                } catch (ObjectDisposedException) {
                    return;
                }
            }
        }

        void ProcessDatagram (Socket socket, IPEndPoint remote, byte[] owned)
        {
            var pkt = new UtpPacket (owned);

            if (pkt.Version != UTP_VERSION)
                return;

            switch (pkt.Type) {
                case PacketType.Syn:
                    HandleSyn (socket, remote, pkt);
                    break;

                case PacketType.Data:
                case PacketType.State:
                case PacketType.Fin:
                case PacketType.Reset:
                    RouteToExisting (remote, pkt);
                    break;
            }
        }

        async void HandleSyn (Socket socket, IPEndPoint remote, UtpPacket syn)
        {
            // The SYN's conn_id IS the initiator's conn_id_recv.
            ushort initiatorConnIdRecv = syn.ConnectionId;
            ushort ourConnIdSend = (ushort) (initiatorConnIdRecv + 1);

            var key = (remote, initiatorConnIdRecv);

            // Idempotent on retransmits – resend the ST_STATE.
            if (_connections.TryGetValue (key, out var existing)) {
                await existing.SendSyncAck (syn.SequenceNumber);
                return;
            }

            var connection = new UtpPeerConnection (
                sendingChannel: SendQueue.Writer,
                remote: remote,
                connIdSend: ourConnIdSend,
                connIdRecv: initiatorConnIdRecv,
                isIncoming: true,
                syn.SequenceNumber);

            if (!_connections.TryAdd (key, connection))
                return;

            await connection.SendSyncAck (syn.SequenceNumber);

            ConnectionReceived?.Invoke (this, new PeerConnectionEventArgs (connection, null));
        }

        void RouteToExisting (EndPoint remote, UtpPacket pkt)
        {
            ushort key_connId = (ushort) (pkt.ConnectionId - 1);
            if (_connections.TryGetValue ((remote, key_connId), out var conn))
                conn.Receive (pkt);
        }
    }
}
