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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer.Utp
{
    public sealed class UtpPeerConnection : IPeerConnection
    {
        enum ConnectionState
        {
            SynSent,
            SynReceived,
            Connected,
            FinSent,
            FinReceived,
            Closed,
            Reset
        }

        sealed class SentPacket
        {
            public SentPacket (UtpPacket packet, int payloadBytes, uint sentAtMicroseconds)
            {
                Packet = packet;
                PayloadBytes = payloadBytes;
                SentAtMicroseconds = sentAtMicroseconds;
                Transmissions = 1;
            }

            public UtpPacket Packet { get; }
            public int PayloadBytes { get; }
            public uint SentAtMicroseconds { get; set; }
            public int Transmissions { get; set; }
            public int DuplicateAckIndications { get; set; }
            public bool FastRetransmitted { get; set; }
        }

        const byte SelectiveAckExtension = 1;
        const uint CControlTargetMicroseconds = 100_000;
        const int DelaySampleLifetimeMicroseconds = 120_000_000;
        const int DefaultMaxReceiveBufferBytes = (int) UtpPeerConnectionListener.INITIAL_WINDOW;
        const uint InitialRetransmitTimeoutMicroseconds = 1_000_000;
        const uint MinimumRetransmitTimeoutMicroseconds = 500_000;
        const uint MaximumRetransmitTimeoutMicroseconds = 60_000_000;

        readonly object locker = new ();
        readonly Dictionary<ushort, SentPacket> sentPackets = new ();
        readonly Dictionary<ushort, UtpPacket> receiveBuffer = new ();
        readonly Queue<(uint ReceivedAtMicroseconds, uint DelayMicroseconds)> delaySamples = new ();
        readonly SemaphoreSlim sendWindowChanged = new (0);
        readonly CancellationTokenSource cts = new ();
        readonly Task retransmitTask;
        readonly IUtpClock clock;
        readonly int maxReceiveBufferBytes;

        ChannelWriter<(UtpPacket, UtpPeerConnection?, IPEndPoint)> SendingChannel { get; }

        readonly UtpPeerConnectionListener? _listener;

        TaskCompletionSource<bool>? HandshakeCompleted { get; set; }

        public bool Disposed => cts.IsCancellationRequested;

        public ReadOnlyMemory<byte> AddressBytes { get; }

        public IPEndPoint EndPoint { get; }

        ushort SequenceNumber { get; set; }

        ushort LastSentSequenceNumber { get; set; }

        internal ushort ConnectionIdSend { get; }

        internal ushort ConnectionIdReceive { get; }

        internal ushort InitialSequenceNumber { get; }

        internal ushort AckNumber { get; set; }

        uint LastReceivedDelayMicroseconds { get; set; }

        uint PeerWindowSize { get; set; } = UtpPeerConnectionListener.INITIAL_WINDOW;

        uint MaxWindow { get; set; } = UtpPeerConnectionListener.INITIAL_WINDOW;

        int CurrentMtu { get; set; }

        uint RetransmitTimeoutMicroseconds { get; set; } = InitialRetransmitTimeoutMicroseconds;

        uint RttMicroseconds { get; set; }

        uint RttVarianceMicroseconds { get; set; }

        ushort LastAckReceived { get; set; }

        int ConsecutiveTimeouts { get; set; }

        int BytesInFlight { get; set; }

        Channel<UtpPacket> ReceivedPackets { get; }

        ConnectionState State { get; set; }

        internal bool IsClosedOrReset => State == ConnectionState.Closed || State == ConnectionState.Reset;

        internal int BytesInFlightForTests => CurrentWindow;

        internal int CurrentMtuForTests => CurrentMtu;

        internal uint MaxWindowForTests => MaxWindow;

        internal uint RetransmitTimeoutMicrosecondsForTests => RetransmitTimeoutMicroseconds;

        public bool IsIncoming { get; }
        public bool CanReconnect => false;
        public Uri Uri { get; }

        public UtpPeerConnection (ChannelWriter<(UtpPacket, UtpPeerConnection?, IPEndPoint)> sendingChannel, IPEndPoint remote, ushort connIdSend, ushort connIdRecv, ushort initialAckNumber)
            : this (sendingChannel, remote, connIdSend, connIdRecv, initialAckNumber, StopwatchUtpClock.Instance)
        {
        }

        internal UtpPeerConnection (ChannelWriter<(UtpPacket, UtpPeerConnection?, IPEndPoint)> sendingChannel, IPEndPoint remote, ushort connIdSend, ushort connIdRecv, ushort initialAckNumber, IUtpClock clock, UtpPeerConnectionListener? listener = null, int maxReceiveBufferBytes = DefaultMaxReceiveBufferBytes, UtpTransportSettings? transportSettings = null)
        {
            SendingChannel = sendingChannel;
            _listener = listener;
            this.clock = clock;
            this.maxReceiveBufferBytes = maxReceiveBufferBytes;
            var settings = UtpTransportSettings.Create (transportSettings ?? listener?.TransportSettings);
            EndPoint = remote;
            AddressBytes = EndPoint.Address.GetAddressBytes ();
            ReceivedPackets = Channel.CreateUnbounded<UtpPacket> ();
            ConnectionIdSend = connIdSend;
            ConnectionIdReceive = connIdRecv;
            IsIncoming = true;
            InitialSequenceNumber = GenerateInitialSequenceNumber ();

            Uri = CreatePeerUri (remote);

            SequenceNumber = InitialSequenceNumber;
            LastSentSequenceNumber = InitialSequenceNumber;
            AckNumber = initialAckNumber;
            LastAckReceived = unchecked((ushort) (InitialSequenceNumber - 1));
            CurrentMtu = settings.InitialPacketSize;
            State = ConnectionState.SynReceived;
            retransmitTask = RetransmitLoopAsync ();
        }

        // Constructor for outgoing connections.
        public UtpPeerConnection (UtpPeerConnectionListener listener, ChannelWriter<(UtpPacket, UtpPeerConnection?, IPEndPoint)> sendingChannel, IPEndPoint remote, ushort connIdRecv)
            : this (sendingChannel, remote, (ushort) (connIdRecv + 1), connIdRecv, 0, listener.Clock, listener)
        {
            IsIncoming = false;
            LastSentSequenceNumber = 0;
            State = ConnectionState.SynSent;
        }

        public UtpPeerConnection (UtpPeerConnectionListener listener, IPEndPoint remote, ushort connIdRecv)
            : this (listener, listener.SendQueue.Writer, remote, connIdRecv)
        {
        }

        static Uri CreatePeerUri (IPEndPoint endpoint)
            => endpoint.AddressFamily switch {
                AddressFamily.InterNetwork => new Uri ($"ipv4://{endpoint}"),
                AddressFamily.InterNetworkV6 => new Uri ($"ipv6://{endpoint}"),
                _ => throw new NotSupportedException ($"Unsupported address family: {endpoint.AddressFamily}")
            };

        public async ReusableTask<bool> ConnectAsync ()
        {
            if (_listener == null)
                throw new InvalidOperationException ("ConnectAsync called on an incoming connection.");

            HandshakeCompleted = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);

            // Register before sending SYN so the ST_STATE reply is never lost.
            if (!_listener.TryRegisterOutgoing (this))
                return false;

            var buf = new byte[UtpPacket.HeaderSize];
            var syn = new UtpPacket (buf) {
                Type = PacketType.Syn,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                Extension = 0,
                ConnectionId = ConnectionIdReceive,
                WindowSize = AdvertisedReceiveWindow,
                SequenceNumber = NextSequenceNumber (),
                AckNumber = 0
            };

            RegisterSent (syn, 0);
            await SendPacketAsync (syn);

            try {
                return await HandshakeCompleted.Task.WaitAsync (cts.Token);
            } catch (OperationCanceledException) {
                return false;
            }
        }

        uint AdvertisedReceiveWindow {
            get {
                lock (locker) {
                    return (uint) Math.Max (0, (int) UtpPeerConnectionListener.INITIAL_WINDOW - ReceiveBufferBytes);
                }
            }
        }

        int ReceiveBufferBytes
            => receiveBuffer.Values.Sum (PacketBufferCost);

        int CurrentWindow {
            get {
                lock (locker)
                    return BytesInFlight;
            }
        }

        internal static bool SequenceLessThanOrEqual (ushort left, ushort right)
            => left == right || unchecked((short) (left - right)) < 0;

        internal static bool SequenceGreaterThan (ushort left, ushort right)
            => unchecked((short) (left - right)) > 0;

        internal static int SequenceDistance (ushort newer, ushort older)
            => unchecked((ushort) (newer - older));

        static ushort GenerateInitialSequenceNumber ()
            => (ushort) RandomNumberGenerator.GetInt32 (0, ushort.MaxValue + 1);

        ushort NextSequenceNumber ()
        {
            var result = SequenceNumber++;
            LastSentSequenceNumber = result;
            return result;
        }

        ushort StateSequenceNumber => LastSentSequenceNumber == 0 ? SequenceNumber : LastSentSequenceNumber;

        void RegisterSent (UtpPacket packet, int payloadBytes)
        {
            if (packet.Type != PacketType.Syn && packet.Type != PacketType.Data && packet.Type != PacketType.Fin)
                return;

            lock (locker) {
                if (sentPackets.TryGetValue (packet.SequenceNumber, out var existing))
                    BytesInFlight -= PacketSendCost (existing);

                sentPackets[packet.SequenceNumber] = new SentPacket (packet, payloadBytes, clock.Microseconds);
                BytesInFlight += PacketSendCost (sentPackets[packet.SequenceNumber]);
            }
        }

        async Task SendPacketAsync (UtpPacket packet)
        {
            packet.WindowSize = AdvertisedReceiveWindow;
            await SendingChannel.WriteAsync ((packet, this, EndPoint), cts.Token);
        }

        static int PacketBufferCost (UtpPacket packet)
            => UtpPacket.HeaderSize + packet.Payload.Length;

        static int PacketSendCost (SentPacket packet)
            => UtpPacket.HeaderSize + packet.PayloadBytes;

        bool TryBufferReceivedPacket (UtpPacket packet)
        {
            if (!SequenceGreaterThan (packet.SequenceNumber, AckNumber))
                return false;

            if (receiveBuffer.ContainsKey (packet.SequenceNumber))
                return false;

            if (ReceiveBufferBytes + PacketBufferCost (packet) > maxReceiveBufferBytes)
                return false;

            receiveBuffer[packet.SequenceNumber] = packet;
            return true;
        }

        internal void PrepareForSend (ref UtpPacket packet)
        {
            packet.SetTimestamp (clock);
            packet.TimestampDiff = LastReceivedDelayMicroseconds;

            lock (locker) {
                if (sentPackets.TryGetValue (packet.SequenceNumber, out var sent))
                    sent.SentAtMicroseconds = packet.Timestamp;
            }
        }

        // Called by the listener for every packet routed to this connection.
        internal void Receive (UtpPacket pkt)
        {
            _ = ReceiveAsync (pkt).ContinueWith (task => {
                if (!task.IsCanceled && task.Exception != null)
                    Dispose ();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        async Task ReceiveAsync (UtpPacket pkt)
        {
            if (!IsValidPacketForCurrentState (pkt))
                return;

            UpdateDelaySample (pkt);
            PeerWindowSize = pkt.WindowSize;
            ProcessAcks (pkt);

            if (pkt.Type == PacketType.Reset) {
                Close (ConnectionState.Reset);
                return;
            }

            if (pkt.Type == PacketType.State && HandshakeCompleted != null && !HandshakeCompleted.Task.IsCompleted) {
                if (pkt.AckNumber == LastSentSequenceNumber) {
                    AckNumber = unchecked((ushort) (pkt.SequenceNumber - 1));
                    State = ConnectionState.Connected;
                    HandshakeCompleted.TrySetResult (true);
                }
                return;
            }

            if (pkt.Type == PacketType.State)
                return;

            if (pkt.Type != PacketType.Data && pkt.Type != PacketType.Fin)
                return;

            lock (locker) {
                TryBufferReceivedPacket (pkt);
            }

            await DeliverAvailablePackets ();

            // ACK duplicates too. This helps the remote side recover from lost ACKs.
            await SendAckAsync (AckNumber);
        }

        internal bool IsValidPacketForCurrentState (UtpPacket pkt)
        {
            if (pkt.ConnectionId != ConnectionIdReceive && (pkt.Type != PacketType.Reset || pkt.ConnectionId != ConnectionIdSend))
                return false;

            return State switch {
                ConnectionState.SynSent => pkt.Type == PacketType.State && pkt.AckNumber == LastSentSequenceNumber || pkt.Type == PacketType.Reset,
                ConnectionState.SynReceived => pkt.Type == PacketType.State || pkt.Type == PacketType.Data || pkt.Type == PacketType.Fin || pkt.Type == PacketType.Reset,
                ConnectionState.Connected => pkt.Type == PacketType.State || pkt.Type == PacketType.Data || pkt.Type == PacketType.Fin || pkt.Type == PacketType.Reset,
                ConnectionState.FinSent => pkt.Type == PacketType.State || pkt.Type == PacketType.Data || pkt.Type == PacketType.Fin || pkt.Type == PacketType.Reset,
                ConnectionState.FinReceived => pkt.Type == PacketType.State || pkt.Type == PacketType.Fin || pkt.Type == PacketType.Reset,
                _ => false,
            };
        }

        void UpdateDelaySample (UtpPacket pkt)
        {
            LastReceivedDelayMicroseconds = unchecked(clock.Microseconds - pkt.Timestamp);
        }

        void ProcessAcks (UtpPacket pkt)
        {
            List<SentPacket> acked = new ();
            List<UtpPacket> fastRetransmits = new ();
            var selectiveAcks = ReadSelectiveAcks (pkt);

            lock (locker) {
                bool ackAdvanced = SequenceGreaterThan (pkt.AckNumber, LastAckReceived);
                if (ackAdvanced)
                    LastAckReceived = pkt.AckNumber;

                foreach (var seq in sentPackets.Keys.ToArray ()) {
                    if (SequenceLessThanOrEqual (seq, pkt.AckNumber)) {
                        acked.Add (sentPackets[seq]);
                        BytesInFlight -= PacketSendCost (sentPackets[seq]);
                        sentPackets.Remove (seq);
                    }
                }

                foreach (var seq in selectiveAcks) {
                    if (sentPackets.Remove (seq, out var sent)) {
                        acked.Add (sent);
                        BytesInFlight -= PacketSendCost (sent);
                    }
                }

                if (acked.Count > 0)
                    ConsecutiveTimeouts = 0;

                if ((!ackAdvanced && acked.Count == 0) || selectiveAcks.Count > 0) {
                    foreach (var sent in sentPackets.Values) {
                        if (!IsPacketIndicatedMissing (sent.Packet.SequenceNumber, pkt.AckNumber, selectiveAcks))
                            continue;

                        sent.DuplicateAckIndications++;
                        if (sent.DuplicateAckIndications >= 3 && !sent.FastRetransmitted) {
                            fastRetransmits.Add (sent.Packet);
                            sent.FastRetransmitted = true;
                            MaxWindow = Math.Max ((uint) UtpTransportSettings.MinimumRecoveryPacketSize, MaxWindow / 2);
                        }
                    }
                }
            }

            foreach (var sent in acked)
                UpdateRtt (sent);

            ApplyCongestionControl (acked.Sum (t => t.PayloadBytes), pkt.TimestampDiff);

            if (acked.Any (t => t.Packet.Type == PacketType.Fin) && State == ConnectionState.FinSent)
                Close (ConnectionState.Closed);

            if (acked.Count > 0)
                sendWindowChanged.Release ();

            foreach (var packet in fastRetransmits)
                _ = RetransmitAsync (packet);
        }

        static bool IsPacketIndicatedMissing (ushort sequenceNumber, ushort ackNumber, List<ushort> selectiveAcks)
        {
            if (SequenceLessThanOrEqual (sequenceNumber, ackNumber))
                return false;

            if (selectiveAcks.Count == 0)
                return sequenceNumber == unchecked((ushort) (ackNumber + 1));

            return selectiveAcks.Any (sack => SequenceGreaterThan (sack, sequenceNumber));
        }

        void UpdateRtt (SentPacket sent)
        {
            if (sent.Transmissions != 1)
                return;

            var packetRtt = unchecked(clock.Microseconds - sent.SentAtMicroseconds);
            if (packetRtt == 0)
                return;

            if (RttMicroseconds == 0) {
                RttMicroseconds = packetRtt;
                RttVarianceMicroseconds = packetRtt / 2;
            } else {
                var delta = Math.Abs ((long) RttMicroseconds - packetRtt);
                RttVarianceMicroseconds = (uint) Math.Max (0, RttVarianceMicroseconds + (delta - RttVarianceMicroseconds) / 4);
                RttMicroseconds = (uint) Math.Max (0, RttMicroseconds + ((long) packetRtt - RttMicroseconds) / 8);
            }

            RetransmitTimeoutMicroseconds = Math.Max (MinimumRetransmitTimeoutMicroseconds, RttMicroseconds + RttVarianceMicroseconds * 4);
        }

        void ApplyCongestionControl (int bytesNewlyAcked, uint delayMicroseconds)
        {
            if (bytesNewlyAcked == 0)
                return;

            lock (locker) {
                var now = clock.Microseconds;
                delaySamples.Enqueue ((now, delayMicroseconds));
                while (delaySamples.Count > 0 && unchecked(now - delaySamples.Peek ().ReceivedAtMicroseconds) > DelaySampleLifetimeMicroseconds)
                    delaySamples.Dequeue ();

                uint baseDelay = delaySamples.Min (t => t.DelayMicroseconds);
                uint ourDelay = delayMicroseconds > baseDelay ? delayMicroseconds - baseDelay : 0;
                double offTarget = (long) CControlTargetMicroseconds - ourDelay;
                double delayFactor = offTarget / CControlTargetMicroseconds;
                double windowFactor = Math.Min (1, bytesNewlyAcked / Math.Max (1.0, MaxWindow));
                double gain = CurrentMtu * delayFactor * windowFactor;
                MaxWindow = (uint) Math.Max (UtpTransportSettings.MinimumRecoveryPacketSize, MaxWindow + gain);
            }
        }

        static List<ushort> ReadSelectiveAcks (UtpPacket pkt)
        {
            var result = new List<ushort> ();
            var span = pkt.AsMemory ().Span;
            byte extension = pkt.Extension;
            int offset = UtpPacket.HeaderSize;

            while (extension != 0 && offset + 2 <= span.Length) {
                byte nextExtension = span[offset];
                int length = span[offset + 1];
                offset += 2;

                if (offset + length > span.Length)
                    return result;

                if (extension == SelectiveAckExtension) {
                    for (int i = 0; i < length; i++) {
                        byte mask = span[offset + i];
                        for (int bit = 0; bit < 8; bit++) {
                            if ((mask & (1 << bit)) != 0)
                                result.Add (unchecked((ushort) (pkt.AckNumber + 2 + i * 8 + bit)));
                        }
                    }
                }

                offset += length;
                extension = nextExtension;
            }
            return result;
        }

        async Task DeliverAvailablePackets ()
        {
            while (true) {
                UtpPacket pkt;
                lock (locker) {
                    var next = unchecked((ushort) (AckNumber + 1));
                    if (!receiveBuffer.Remove (next, out pkt))
                        return;
                    AckNumber = next;
                }

                if (pkt.Type == PacketType.Fin) {
                    State = ConnectionState.FinReceived;
                    ReceivedPackets.Writer.TryComplete ();
                    return;
                }

                if (pkt.Payload.Length > 0)
                    await ReceivedPackets.Writer.WriteAsync (pkt, cts.Token);
            }
        }

        async ReusableTask SendAckAsync (ushort ackNr)
        {
            var sack = CreateSelectiveAckExtension (ackNr);
            var buf = new byte[UtpPacket.HeaderSize + sack.Length];
            var pkt = new UtpPacket (buf) {
                Type = PacketType.State,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                Extension = sack.Length == 0 ? (byte) 0 : SelectiveAckExtension,
                ConnectionId = ConnectionIdSend,
                WindowSize = AdvertisedReceiveWindow,
                SequenceNumber = SequenceNumber,
                AckNumber = ackNr
            };
            sack.CopyTo (buf.AsSpan (UtpPacket.HeaderSize));
            await SendPacketAsync (pkt);
        }

        byte[] CreateSelectiveAckExtension (ushort ackNr)
        {
            ushort[] buffered;
            lock (locker)
                buffered = receiveBuffer.Keys.Where (t => SequenceGreaterThan (t, unchecked((ushort) (ackNr + 1)))).ToArray ();

            if (buffered.Length == 0)
                return Array.Empty<byte> ();

            int maxBit = buffered.Max (t => SequenceDistance (t, unchecked((ushort) (ackNr + 2))));
            int length = Math.Max (4, ((maxBit / 8) + 4) / 4 * 4);
            var result = new byte[2 + length];
            result[0] = 0;
            result[1] = (byte) length;

            foreach (var seq in buffered) {
                int bit = SequenceDistance (seq, unchecked((ushort) (ackNr + 2)));
                result[2 + bit / 8] |= (byte) (1 << (bit % 8));
            }

            return result;
        }

        internal async ReusableTask SendSynAck (ushort peerSeqNr)
        {
            var buf = new byte[UtpPacket.HeaderSize];
            var pkt = new UtpPacket (buf) {
                Type = PacketType.State,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                Extension = 0,
                ConnectionId = ConnectionIdSend,
                WindowSize = AdvertisedReceiveWindow,
                SequenceNumber = StateSequenceNumber,
                AckNumber = peerSeqNr,
                TimestampDiff = 0
            };

            State = ConnectionState.Connected;
            await SendPacketAsync (pkt);
        }

        UtpPacket? currentPacket;
        int currentPayloadRead;
        public async ReusableTask<int> ReceiveAsync (Memory<byte> buffer)
        {
            static int ReadFromPacket (ReadOnlySpan<byte> src, Span<byte> dest)
            {
                var toRead = Math.Min (src.Length, dest.Length);
                src.Slice (0, toRead).CopyTo (dest);
                return toRead;
            }

            if (buffer.IsEmpty)
                return 0;

            try {
                if (currentPacket == null) {
                    currentPacket = await ReceivedPackets.Reader.ReadAsync (cts.Token);
                    currentPayloadRead = 0;
                }
            } catch (ChannelClosedException) {
                return 0;
            }

            int read = ReadFromPacket (currentPacket.Value.Payload.Slice (currentPayloadRead), buffer.Span);
            currentPayloadRead += read;
            if (currentPayloadRead == currentPacket.Value.Payload.Length)
                currentPacket = null;

            return read;
        }

        public async ReusableTask<int> SendAsync (ReadOnlyMemory<byte> buffer)
        {
            if (IsClosedOrReset || State == ConnectionState.FinSent || State == ConnectionState.FinReceived)
                return 0;

            int totalSent = 0;

            while (!buffer.IsEmpty) {
                int payloadLen = Math.Min (buffer.Length, CurrentMtu);
                await WaitForSendWindow (payloadLen);

                var pktBuf = new byte[UtpPacket.HeaderSize + payloadLen];
                var pkt = new UtpPacket (pktBuf) {
                    Type = PacketType.Data,
                    Version = UtpPeerConnectionListener.UTP_VERSION,
                    Extension = 0,
                    ConnectionId = ConnectionIdSend,
                    WindowSize = AdvertisedReceiveWindow,
                    SequenceNumber = NextSequenceNumber (),
                    AckNumber = AckNumber
                };

                buffer.Span.Slice (0, payloadLen).CopyTo (pkt.Payload);

                RegisterSent (pkt, payloadLen);
                await SendPacketAsync (pkt);

                buffer = buffer.Slice (payloadLen);
                totalSent += payloadLen;
            }

            return totalSent;
        }

        internal async ReusableTask SendFinAsync ()
        {
            if (IsClosedOrReset || State == ConnectionState.FinSent)
                return;

            var pktBuf = new byte[UtpPacket.HeaderSize];
            var pkt = new UtpPacket (pktBuf) {
                Type = PacketType.Fin,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                Extension = 0,
                ConnectionId = ConnectionIdSend,
                WindowSize = AdvertisedReceiveWindow,
                SequenceNumber = NextSequenceNumber (),
                AckNumber = AckNumber
            };

            State = ConnectionState.FinSent;
            RegisterSent (pkt, 0);
            await SendPacketAsync (pkt);
        }

        async Task WaitForSendWindow (int payloadLen)
        {
            while (!cts.IsCancellationRequested) {
                var allowed = Math.Min (MaxWindow, PeerWindowSize);
                if (CurrentWindow + payloadLen + UtpPacket.HeaderSize <= allowed || CurrentWindow == 0)
                    return;

                await sendWindowChanged.WaitAsync (TimeSpan.FromMilliseconds (100), cts.Token);
            }
            cts.Token.ThrowIfCancellationRequested ();
        }

        async Task RetransmitLoopAsync ()
        {
            try {
                while (!cts.IsCancellationRequested) {
                    await Task.Delay (50, cts.Token);

                    SentPacket? timedOut = null;
                    lock (locker) {
                        foreach (var packet in sentPackets.Values.OrderBy (t => t.SentAtMicroseconds)) {
                            if (unchecked(clock.Microseconds - packet.SentAtMicroseconds) >= RetransmitTimeoutMicroseconds) {
                                timedOut = packet;
                                break;
                            }
                        }

                        if (timedOut != null) {
                            CurrentMtu = UtpTransportSettings.MinimumRecoveryPacketSize;
                            MaxWindow = UtpTransportSettings.MinimumRecoveryPacketSize;
                            ConsecutiveTimeouts++;
                            RetransmitTimeoutMicroseconds = Math.Min (
                                MaximumRetransmitTimeoutMicroseconds,
                                Math.Max (MinimumRetransmitTimeoutMicroseconds, RetransmitTimeoutMicroseconds) * 2);
                        }
                    }

                    if (timedOut != null)
                        await RetransmitAsync (timedOut.Packet);
                }
            } catch (OperationCanceledException) {
            }
        }

        async Task RetransmitAsync (UtpPacket packet)
        {
            lock (locker) {
                if (sentPackets.TryGetValue (packet.SequenceNumber, out var sent)) {
                    sent.Transmissions++;
                    sent.DuplicateAckIndications = 0;
                    sent.SentAtMicroseconds = clock.Microseconds;
                } else {
                    return;
                }
            }

            await SendPacketAsync (packet);
        }

        public void Dispose ()
            => Close (State == ConnectionState.Reset ? ConnectionState.Reset : ConnectionState.Closed);

        void Close (ConnectionState finalState)
        {
            if (cts.IsCancellationRequested)
                return;

            State = finalState;
            cts.Cancel ();
            ReceivedPackets.Writer.TryComplete ();
            sendWindowChanged.Release ();
            _listener?.Unregister (this);
        }
    }
}
