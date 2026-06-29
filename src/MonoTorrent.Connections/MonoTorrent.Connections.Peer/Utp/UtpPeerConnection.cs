//
// UtpPeerConnection.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2026 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Utp;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer.Utp
{
    public sealed class UtpPeerConnection : IPeerConnection
    {
        // Most likely 'safe' limit on public internet is 1452 bytes.
        // 1500 - (28 byte ip header overhead) - (20 byte utp header overhead)
        // This will be lower if data is encapsulated in another protocol.
        //
        // Start with 1400 as that should allow a full 16kB piece to be sent
        // in 12 packets and also provide some headroom for encapsulation.
        //
        // Can be probed at runtime to increase/decrease as necessary.
        static readonly int InitialMtuSize = 1400;

        ChannelWriter<(UtpPacket, uint, IPEndPoint)> SendingChannel { get; }

        // BUG FIX 3: ConnectAsync needs a reference back to the listener so it can
        // register the connection in _connections before sending the SYN, and so it
        // can await the ST_STATE reply via HandshakeCompleted.
        readonly UtpPeerConnectionListener? _listener;

        // Set by ConnectAsync (outgoing); awaited on the first ST_STATE receipt.
        TaskCompletionSource<bool>? HandshakeCompleted { get; set; }

        public bool Disposed => cts.IsCancellationRequested;

        public ReadOnlyMemory<byte> AddressBytes => default;

        public IPEndPoint EndPoint { get; }

        ushort SequenceNumber { get; set; }

        internal ushort ConnectionIdSend { get; }

        internal ushort ConnectionIdReceive { get; }

        internal ushort AckNumber { get; set; }

        uint LastPacketTimestamp { get; set; }

        // Implement path MTU discovery to optimise this for the uncommon case.
        // e.g. small MTU or jumbo frames.
        CancellationTokenSource cts = new ();
        int CurrentMtu { get; set; } = InitialMtuSize;
        Channel<UtpPacket> ReceivedPackets { get; }

        public bool IsIncoming { get; }
        public bool CanReconnect => false;
        public Uri Uri { get; }

        public UtpPeerConnection (ChannelWriter<(UtpPacket, uint, IPEndPoint)> sendingChannel, IPEndPoint remote, ushort connIdSend, ushort connIdRecv, ushort initialAckNumber)
        {
            SendingChannel = sendingChannel;
            EndPoint = remote;
            ReceivedPackets = Channel.CreateUnbounded<UtpPacket> ();
            ConnectionIdSend = connIdSend;
            ConnectionIdReceive = connIdRecv;
            IsIncoming = true;

            var ep = (IPEndPoint) remote;
            Uri = new Uri ($"utp://{ep.Address}:{ep.Port}");

            // The ack for the syn sends sequence number 1. The next message will be 2.
            SequenceNumber = 2;
            AckNumber = initialAckNumber;
        }

        // Constructor for outgoing connections.
        public UtpPeerConnection (UtpPeerConnectionListener listener, ChannelWriter<(UtpPacket, uint, IPEndPoint)> sendingChannel, IPEndPoint remote, ushort connIdRecv)
            : this (sendingChannel, remote, (ushort) (connIdRecv + 1), connIdRecv, 0)
        {
            _listener = listener;
            IsIncoming = false;
        }

        public async ReusableTask<bool> ConnectAsync ()
        {
            if (_listener == null)
                throw new InvalidOperationException ("ConnectAsync called on an incoming connection.");

            HandshakeCompleted = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);

            // Register before sending SYN so the ST_STATE reply is never lost.
            if (!_listener.TryRegisterOutgoing (this))
                return false;

            var buf = new byte[UtpPacket.HeaderSize];
            var syn = new UtpPacket (buf);
            syn.Type = PacketType.Syn;
            syn.Version = UtpPeerConnectionListener.UTP_VERSION;
            syn.Extension = 0;
            syn.ConnectionId = ConnectionIdReceive;
            syn.WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW;
            syn.SequenceNumber = 1;
            syn.AckNumber = 0;

            await SendingChannel.WriteAsync ((syn, 0, EndPoint));

            try {
                return await HandshakeCompleted.Task.WaitAsync (cts.Token);
            } catch (OperationCanceledException) {
                return false;
            }
        }

        // Called by the listener for every packet routed to this connection.
        internal async void Receive (UtpPacket pkt)
        {
            if (pkt.Type == PacketType.State && HandshakeCompleted != null && !HandshakeCompleted.Task.IsCompleted) {
                AckNumber = pkt.SequenceNumber;
                HandshakeCompleted.TrySetResult (true);
                return;
            }

            // FIXME: Instead of ignoring out of order packets, use a reorder buffer. We can save the received data and wait for the earlier one to arrive.
            // We can ack the most recent one then.
            if (pkt.SequenceNumber != (AckNumber + 1))
                return;

            AckNumber = pkt.SequenceNumber;
            LastPacketTimestamp = pkt.Timestamp;

            if (pkt.Type == PacketType.Fin || pkt.Type == PacketType.Reset) {
                cts.Cancel ();
                return;
            }

            // Enqueue the ack now that the message has been received
            await SendAckAsync (AckNumber);

            if (pkt.Type != PacketType.Data)
                return;

            if (pkt.Payload.IsEmpty)
                return;

            await ReceivedPackets.Writer.WriteAsync (pkt);
        }

        async ReusableTask SendAckAsync (ushort ackNr)
        {
            var buf = new byte[UtpPacket.HeaderSize];
            var pkt = new UtpPacket (buf);
            pkt.Type = PacketType.State;
            pkt.Version = UtpPeerConnectionListener.UTP_VERSION;
            pkt.Extension = 0;
            pkt.ConnectionId = ConnectionIdSend;
            pkt.WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW;
            pkt.SequenceNumber = SequenceNumber;
            pkt.AckNumber = ackNr;
            await SendingChannel.WriteAsync ((pkt, LastPacketTimestamp, EndPoint));
        }

        internal async ReusableTask SendSynAck (ushort peerSeqNr)
        {
            var buf = new byte[UtpPacket.HeaderSize];
            var pkt = new UtpPacket (buf);

            pkt.Type = PacketType.State;
            pkt.Version = UtpPeerConnectionListener.UTP_VERSION;
            pkt.Extension = 0;
            pkt.ConnectionId = ConnectionIdSend;
            pkt.WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW;
            pkt.SequenceNumber = 1;
            pkt.AckNumber = peerSeqNr;
            pkt.TimestampDiff = 0;

            await SendingChannel.WriteAsync ((pkt, 0, EndPoint));
        }

        UtpPacket? currentPacket;
        int currentPayloadRead = 0;
        public async ReusableTask<int> ReceiveAsync (Memory<byte> buffer)
        {
            static int ReadFromPacket (ReadOnlySpan<byte> src, Span<byte> dest)
            {
                var toRead = Math.Min (src.Length, dest.Length);
                src.Slice (0, toRead).CopyTo (dest);
                return toRead;
            }

            if (currentPacket == null) {
                currentPacket = await ReceivedPackets.Reader.ReadAsync (cts.Token);
                currentPayloadRead = 0;
            }

            int read = ReadFromPacket (currentPacket.Value.Payload.Slice (currentPayloadRead), buffer.Span);
            currentPayloadRead += read;
            if (currentPayloadRead == currentPacket.Value.Payload.Length)
                currentPacket = null;
            return read;
        }

        public async ReusableTask<int> SendAsync (ReadOnlyMemory<byte> buffer)
        {
            // figure out the best way to use a reusablesemaphore to pause sending
            // when the window is full.
            int totalSent = 0;

            while (!buffer.IsEmpty) {
                int payloadLen = Math.Min (buffer.Span.Length, CurrentMtu);
                var pktBuf = new byte[UtpPacket.HeaderSize + payloadLen];
                var pkt = new UtpPacket (pktBuf);

                pkt.Type = PacketType.Data;
                pkt.Version = 1;
                pkt.Extension = 0;
                pkt.ConnectionId = ConnectionIdSend;
                pkt.WindowSize = 1 << 18;
                pkt.SequenceNumber = SequenceNumber++;
                pkt.AckNumber = AckNumber;

                buffer.Span.Slice (0, payloadLen).CopyTo (pkt.Payload);

                await SendingChannel.WriteAsync ((pkt, LastPacketTimestamp, EndPoint));

                buffer = buffer.Slice (payloadLen);
                totalSent += payloadLen;
            }

            return totalSent;
        }


        public void Dispose ()
        {
            ReceivedPackets.Writer.Complete ();
            cts.Cancel ();
        }
    }
}
