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
            public SentPacket (UtpPacket packet, int payloadBytes, uint sentAtMicroseconds, bool isMtuProbe)
            {
                Packet = packet;
                PayloadBytes = payloadBytes;
                SentAtMicroseconds = sentAtMicroseconds;
                Transmissions = 1;
                IsMtuProbe = isMtuProbe;
            }

            public UtpPacket Packet { get; }
            public int PayloadBytes { get; }
            public bool IsMtuProbe { get; }
            public uint SentAtMicroseconds { get; set; }
            public int Transmissions { get; set; }
            public int DuplicateAckIndications { get; set; }
            public bool FastRetransmitted { get; set; }
        }

        sealed class ParsedPacket
        {
            public ParsedPacket (UtpPacket packet, int extensionStart, int payloadOffset, List<ushort> selectiveAcks, ulong? extensionBits)
            {
                Packet = packet;
                ExtensionStart = extensionStart;
                PayloadOffset = payloadOffset;
                SelectiveAcks = selectiveAcks;
                ExtensionBits = extensionBits;
            }

            public UtpPacket Packet { get; }
            public int ExtensionStart { get; }
            public int PayloadOffset { get; }
            public int PayloadLength => Packet.AsMemory ().Length - PayloadOffset;
            public Memory<byte> Payload => Packet.AsMemory ().Slice (PayloadOffset, PayloadLength);
            public List<ushort> SelectiveAcks { get; }
            public ulong? ExtensionBits { get; }
        }

        enum ReceiveSequenceStatus
        {
            OldOrDuplicate,
            Acceptable,
            TooFarAhead
        }

        const byte SelectiveAckExtension = 1;
        const byte ExtensionBitsExtension = 2;
        const uint CControlTargetMicroseconds = 100_000;
        const int DelaySampleLifetimeMicroseconds = 120_000_000;
        const int DefaultMaxReceiveBufferBytes = (int) UtpPeerConnectionListener.INITIAL_WINDOW;
        const uint InitialRetransmitTimeoutMicroseconds = 1_000_000;
        const uint MinimumRetransmitTimeoutMicroseconds = 500_000;
        const uint MaximumRetransmitTimeoutMicroseconds = 60_000_000;
        const int MtuConvergedThreshold = 16;

        readonly object locker = new ();
        readonly Dictionary<ushort, SentPacket> sentPackets = new ();
        readonly Dictionary<ushort, ParsedPacket> receiveBuffer = new ();
        readonly Queue<(uint ReceivedAtMicroseconds, uint DelayMicroseconds)> delaySamples = new ();
        readonly SemaphoreSlim sendWindowChanged = new (0);
        readonly CancellationTokenSource cts = new ();
        readonly Task retransmitTask;
        readonly IUtpClock clock;
        readonly int maxReceiveBufferBytes;
        readonly UtpTransportSettings transportSettings;
        CancellationTokenSource? delayedAckCancellation;

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

        ushort? ReceivedFinSequence { get; set; }

        uint LastReceivedDelayMicroseconds { get; set; }

        uint PeerWindowSize { get; set; } = UtpPeerConnectionListener.INITIAL_WINDOW;

        uint MaxWindow { get; set; } = UtpPeerConnectionListener.INITIAL_WINDOW;

        int CurrentMtu { get; set; }

        int MtuFloor { get; set; }

        int MtuCeiling { get; set; }

        ushort? MtuProbeSequence { get; set; }

        int MtuProbeSize { get; set; }

        uint NextMtuProbeAt { get; set; }

        uint RetransmitTimeoutMicroseconds { get; set; } = InitialRetransmitTimeoutMicroseconds;

        uint RttMicroseconds { get; set; }

        uint RttVarianceMicroseconds { get; set; }

        uint RecentDelayMicroseconds { get; set; }

        ulong PeerExtensionBits { get; set; }

        ushort LastAckReceived { get; set; }

        uint LastSentPacketMicroseconds { get; set; }

        bool HasSentPacket { get; set; }

        uint LastZeroWindowProbeMicroseconds { get; set; }

        int ConsecutiveTimeouts { get; set; }

        int BytesInFlight { get; set; }

        int OutOfOrderBufferedBytes { get; set; }

        int QueuedInOrderBytes { get; set; }

        int CurrentUnreadPacketBytes { get; set; }

        Channel<ParsedPacket> ReceivedPackets { get; }

        ConnectionState State { get; set; }

        bool CleanReadEof { get; set; }

        internal bool IsClosedOrReset => State == ConnectionState.Closed || State == ConnectionState.Reset;

        internal int BytesInFlightForTests => CurrentWindow;

        internal int CurrentMtuForTests => CurrentMtu;

        internal int MtuFloorForTests => MtuFloor;

        internal int MtuCeilingForTests => MtuCeiling;

        internal ushort? MtuProbeSequenceForTests => MtuProbeSequence;

        internal int MtuProbeSizeForTests => MtuProbeSize;

        internal uint NextMtuProbeAtForTests {
            get => NextMtuProbeAt;
            set => NextMtuProbeAt = value;
        }

        internal uint MaxWindowForTests => MaxWindow;

        internal uint RetransmitTimeoutMicrosecondsForTests => RetransmitTimeoutMicroseconds;

        internal uint RecentDelayMicrosecondsForTests => RecentDelayMicroseconds;

        internal ulong PeerExtensionBitsForTests => PeerExtensionBits;

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
            this.transportSettings = settings;
            EndPoint = remote;
            AddressBytes = EndPoint.Address.GetAddressBytes ();
            ReceivedPackets = Channel.CreateUnbounded<ParsedPacket> ();
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
            MtuFloor = CurrentMtu;
            MtuCeiling = Math.Max (CurrentMtu, GetDefaultMtuCeiling (remote.AddressFamily));
            NextMtuProbeAt = unchecked(clock.Microseconds + (uint) settings.MtuProbeInterval.TotalMicroseconds);
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
                    return (uint) Math.Max (0, maxReceiveBufferBytes - ReceiveBufferBytes);
                }
            }
        }

        int ReceiveBufferBytes
            => OutOfOrderBufferedBytes + QueuedInOrderBytes + CurrentUnreadPacketBytes;

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

        static int GetDefaultMtuCeiling (AddressFamily addressFamily)
            => addressFamily switch {
                AddressFamily.InterNetwork => 1452,
                AddressFamily.InterNetworkV6 => 1432,
                _ => UtpTransportSettings.DefaultInitialPacketSize
            };

        ushort NextSequenceNumber ()
        {
            var result = SequenceNumber++;
            LastSentSequenceNumber = result;
            return result;
        }

        ushort StateSequenceNumber => LastSentSequenceNumber == 0 ? SequenceNumber : LastSentSequenceNumber;

        void RegisterSent (UtpPacket packet, int payloadBytes, bool isMtuProbe = false)
        {
            if (packet.Type != PacketType.Syn && packet.Type != PacketType.Data && packet.Type != PacketType.Fin)
                return;

            lock (locker) {
                if (sentPackets.TryGetValue (packet.SequenceNumber, out var existing))
                    BytesInFlight -= PacketSendCost (existing);

                sentPackets[packet.SequenceNumber] = new SentPacket (packet, payloadBytes, clock.Microseconds, isMtuProbe);
                BytesInFlight += PacketSendCost (sentPackets[packet.SequenceNumber]);

                if (isMtuProbe) {
                    MtuProbeSequence = packet.SequenceNumber;
                    MtuProbeSize = payloadBytes;
                }
            }
        }

        async Task SendPacketAsync (UtpPacket packet)
        {
            packet.WindowSize = AdvertisedReceiveWindow;
            await SendingChannel.WriteAsync ((packet, this, EndPoint), cts.Token);
            LastSentPacketMicroseconds = clock.Microseconds;
            HasSentPacket = true;
        }

        static int PacketBufferCost (ParsedPacket packet)
            => packet.Packet.AsMemory ().Length;

        static int PacketSendCost (SentPacket packet)
            => UtpPacket.HeaderSize + packet.PayloadBytes;

        bool TryBufferReceivedPacket (ParsedPacket packet)
        {
            if (receiveBuffer.ContainsKey (packet.Packet.SequenceNumber))
                return false;

            if (ReceiveBufferBytes + PacketBufferCost (packet) > maxReceiveBufferBytes)
                return false;

            receiveBuffer[packet.Packet.SequenceNumber] = packet;
            OutOfOrderBufferedBytes += PacketBufferCost (packet);
            return true;
        }

        ReceiveSequenceStatus GetReceiveSequenceStatus (UtpPacket packet)
        {
            if (!SequenceGreaterThan (packet.SequenceNumber, AckNumber))
                return ReceiveSequenceStatus.OldOrDuplicate;

            if (receiveBuffer.ContainsKey (packet.SequenceNumber))
                return ReceiveSequenceStatus.OldOrDuplicate;

            if (ReceivedFinSequence.HasValue && SequenceGreaterThan (packet.SequenceNumber, ReceivedFinSequence.Value))
                return ReceiveSequenceStatus.OldOrDuplicate;

            var nextExpected = unchecked((ushort) (AckNumber + 1));
            if (SequenceDistance (packet.SequenceNumber, nextExpected) > transportSettings.MaxReorderDistance)
                return ReceiveSequenceStatus.TooFarAhead;

            return ReceiveSequenceStatus.Acceptable;
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

            if (!TryParsePacket (pkt, out var parsed))
                return;

            if (parsed.ExtensionBits.HasValue)
                PeerExtensionBits = parsed.ExtensionBits.Value;

            UpdateDelaySample (pkt);
            var wasPeerWindowZero = PeerWindowSize == 0;
            PeerWindowSize = pkt.WindowSize;
            if (wasPeerWindowZero && PeerWindowSize > 0)
                sendWindowChanged.Release ();
            if (!wasPeerWindowZero && PeerWindowSize == 0)
                LastZeroWindowProbeMicroseconds = clock.Microseconds;
            ProcessAcks (pkt, parsed.SelectiveAcks);

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

            ReceiveSequenceStatus sequenceStatus;
            bool wasNextExpected = false;
            lock (locker) {
                sequenceStatus = GetReceiveSequenceStatus (pkt);
                if (sequenceStatus == ReceiveSequenceStatus.Acceptable) {
                    wasNextExpected = pkt.SequenceNumber == unchecked((ushort) (AckNumber + 1));
                    if (pkt.Type == PacketType.Fin && (!ReceivedFinSequence.HasValue || SequenceGreaterThan (ReceivedFinSequence.Value, pkt.SequenceNumber)))
                        ReceivedFinSequence = pkt.SequenceNumber;
                    TryBufferReceivedPacket (parsed);
                }
            }

            if (sequenceStatus == ReceiveSequenceStatus.TooFarAhead)
                return;

            var delivery = await DeliverAvailablePackets ();

            if (ShouldAckImmediately (pkt, sequenceStatus, wasNextExpected))
                await SendImmediateAckAsync ();
            else if (delivery.AckAdvanced)
                ScheduleDelayedAck ();
        }

        bool ShouldAckImmediately (UtpPacket pkt, ReceiveSequenceStatus sequenceStatus, bool wasNextExpected)
        {
            if (!transportSettings.EnableDelayedAcks)
                return true;

            if (pkt.Type == PacketType.Fin)
                return true;

            if (sequenceStatus == ReceiveSequenceStatus.OldOrDuplicate)
                return true;

            if (!wasNextExpected)
                return true;

            return HasSelectiveAckContent (AckNumber);
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

        internal bool IsHarmlessStalePacket (UtpPacket pkt)
        {
            if (pkt.ConnectionId != ConnectionIdReceive)
                return false;

            if (pkt.Type != PacketType.Data && pkt.Type != PacketType.Fin)
                return false;

            return !SequenceGreaterThan (pkt.SequenceNumber, AckNumber);
        }

        void UpdateDelaySample (UtpPacket pkt)
        {
            LastReceivedDelayMicroseconds = unchecked(clock.Microseconds - pkt.Timestamp);
        }

        void ProcessAcks (UtpPacket pkt, List<ushort> receivedSelectiveAcks)
        {
            List<SentPacket> acked = new ();
            List<UtpPacket> fastRetransmits = new ();
            List<ushort>? selectiveAcks = null;

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

                foreach (var seq in receivedSelectiveAcks) {
                    if (!SequenceGreaterThan (seq, pkt.AckNumber) || !SequenceLessThanOrEqual (seq, LastSentSequenceNumber))
                        continue;

                    if (sentPackets.Remove (seq, out var sent)) {
                        selectiveAcks ??= new List<ushort> ();
                        selectiveAcks.Add (seq);
                        acked.Add (sent);
                        BytesInFlight -= PacketSendCost (sent);
                    }
                }

                if (acked.Count > 0)
                    ConsecutiveTimeouts = 0;

                bool pureDuplicateAck = pkt.Type == PacketType.State && receivedSelectiveAcks.Count == 0 && !ackAdvanced && acked.Count == 0;
                bool sackEvidence = selectiveAcks?.Count > 0;
                if (pureDuplicateAck || sackEvidence) {
                    foreach (var sent in sentPackets.Values) {
                        int duplicateAckIndications = CountDuplicateAckIndications (sent.Packet.SequenceNumber, pkt.AckNumber, selectiveAcks, pureDuplicateAck);
                        if (duplicateAckIndications == 0)
                            continue;

                        sent.DuplicateAckIndications += duplicateAckIndications;
                        if (sent.DuplicateAckIndications >= 3 && !sent.FastRetransmitted) {
                            fastRetransmits.Add (sent.Packet);
                            sent.FastRetransmitted = true;
                            if (sent.IsMtuProbe && sent.Packet.SequenceNumber == MtuProbeSequence)
                                HandleMtuProbeTimeout (sent);
                            else
                                MaxWindow = Math.Max ((uint) UtpTransportSettings.MinimumRecoveryPacketSize, MaxWindow / 2);
                        }
                    }
                }
            }

            uint minAckedRttMicroseconds = 0;
            foreach (var sent in acked) {
                var packetRtt = UpdateRtt (sent);
                if (packetRtt != 0 && (minAckedRttMicroseconds == 0 || packetRtt < minAckedRttMicroseconds))
                    minAckedRttMicroseconds = packetRtt;
            }

            ProcessMtuProbeAcks (acked);
            ApplyCongestionControl (acked.Sum (t => t.PayloadBytes), pkt.TimestampDiff, minAckedRttMicroseconds);

            if (acked.Any (t => t.Packet.Type == PacketType.Fin) && State == ConnectionState.FinSent)
                CloseCleanly ();

            if (acked.Count > 0)
                sendWindowChanged.Release ();

            foreach (var packet in fastRetransmits)
                _ = RetransmitAsync (packet);
        }

        void ProcessMtuProbeAcks (List<SentPacket> acked)
        {
            foreach (var sent in acked) {
                if (!sent.IsMtuProbe || sent.Packet.SequenceNumber != MtuProbeSequence || sent.Transmissions != 1)
                    continue;

                MtuFloor = Math.Max (MtuFloor, sent.PayloadBytes);
                MtuProbeSequence = null;
                MtuProbeSize = 0;
                CurrentMtu = MtuFloor;
                if (MtuCeiling - MtuFloor <= MtuConvergedThreshold)
                    NextMtuProbeAt = unchecked(clock.Microseconds + (uint) transportSettings.MtuProbeInterval.TotalMicroseconds);
                else
                    NextMtuProbeAt = clock.Microseconds;
            }
        }

        static int CountDuplicateAckIndications (ushort sequenceNumber, ushort ackNumber, List<ushort>? selectiveAcks, bool pureDuplicateAck)
        {
            if (SequenceLessThanOrEqual (sequenceNumber, ackNumber))
                return 0;

            if (selectiveAcks == null || selectiveAcks.Count == 0)
                return pureDuplicateAck && sequenceNumber == unchecked((ushort) (ackNumber + 1)) ? 1 : 0;

            int count = 0;
            foreach (var sack in selectiveAcks) {
                if (SequenceGreaterThan (sack, sequenceNumber))
                    count++;
            }
            return count;
        }

        uint UpdateRtt (SentPacket sent)
        {
            if (sent.Transmissions != 1)
                return 0;

            var packetRtt = unchecked(clock.Microseconds - sent.SentAtMicroseconds);
            if (packetRtt == 0)
                return 0;

            if (RttMicroseconds == 0) {
                RttMicroseconds = packetRtt;
                RttVarianceMicroseconds = packetRtt / 2;
            } else {
                var delta = Math.Abs ((long) RttMicroseconds - packetRtt);
                RttVarianceMicroseconds = (uint) Math.Max (0, RttVarianceMicroseconds + (delta - RttVarianceMicroseconds) / 4);
                RttMicroseconds = (uint) Math.Max (0, RttMicroseconds + ((long) packetRtt - RttMicroseconds) / 8);
            }

            RetransmitTimeoutMicroseconds = Math.Max (MinimumRetransmitTimeoutMicroseconds, RttMicroseconds + RttVarianceMicroseconds * 4);
            return packetRtt;
        }

        void ApplyCongestionControl (int bytesNewlyAcked, uint delayMicroseconds, uint minAckedRttMicroseconds)
        {
            if (bytesNewlyAcked == 0 || delayMicroseconds == 0 || minAckedRttMicroseconds == 0)
                return;

            lock (locker) {
                var now = clock.Microseconds;
                var clampedDelay = Math.Min (delayMicroseconds, minAckedRttMicroseconds);
                delaySamples.Enqueue ((now, clampedDelay));
                while (delaySamples.Count > 0 && unchecked(now - delaySamples.Peek ().ReceivedAtMicroseconds) > DelaySampleLifetimeMicroseconds)
                    delaySamples.Dequeue ();

                RecentDelayMicroseconds = (uint) delaySamples.Average (t => t.DelayMicroseconds);
                uint baseDelay = delaySamples.Min (t => t.DelayMicroseconds);
                uint ourDelay = RecentDelayMicroseconds > baseDelay ? RecentDelayMicroseconds - baseDelay : 0;
                double offTarget = (long) CControlTargetMicroseconds - ourDelay;
                double delayFactor = offTarget / CControlTargetMicroseconds;
                double windowFactor = Math.Min (1, bytesNewlyAcked / Math.Max (1.0, MaxWindow));
                double gain = CurrentMtu * delayFactor * windowFactor;
                MaxWindow = (uint) Math.Max (UtpTransportSettings.MinimumRecoveryPacketSize, MaxWindow + gain);
            }
        }

        static bool TryParsePacket (UtpPacket pkt, out ParsedPacket parsed)
        {
            parsed = null!;
            var span = pkt.AsMemory ().Span;
            byte extension = pkt.Extension;
            int offset = UtpPacket.HeaderSize;
            var selectiveAcks = new List<ushort> ();
            ulong? extensionBits = null;

            while (extension != 0) {
                if (offset + 2 > span.Length)
                    return false;

                byte nextExtension = span[offset];
                int length = span[offset + 1];
                offset += 2;

                if (offset + length > span.Length)
                    return false;

                if (extension == SelectiveAckExtension) {
                    if (length < 4 || length % 4 != 0)
                        return false;

                    for (int i = 0; i < length; i++) {
                        byte mask = span[offset + i];
                        for (int bit = 0; bit < 8; bit++) {
                            if ((mask & (1 << bit)) != 0)
                                selectiveAcks.Add (unchecked((ushort) (pkt.AckNumber + 2 + i * 8 + bit)));
                        }
                    }
                } else if (extension == ExtensionBitsExtension) {
                    if (length != 8)
                        return false;

                    ulong bits = 0;
                    for (int i = 0; i < 8; i++)
                        bits = bits << 8 | (ulong) span[offset + i];
                    extensionBits = bits;
                }

                offset += length;
                extension = nextExtension;
            }

            parsed = new ParsedPacket (pkt, UtpPacket.HeaderSize, offset, selectiveAcks, extensionBits);
            return true;
        }

        async Task<(bool AckAdvanced, bool FinDelivered)> DeliverAvailablePackets ()
        {
            bool ackAdvanced = false;
            while (true) {
                ParsedPacket? buffered;
                lock (locker) {
                    var next = unchecked((ushort) (AckNumber + 1));
                    if (!receiveBuffer.Remove (next, out buffered))
                        return (ackAdvanced, false);
                    OutOfOrderBufferedBytes -= PacketBufferCost (buffered);
                    if (buffered.Packet.Type != PacketType.Fin && buffered.PayloadLength > 0)
                        QueuedInOrderBytes += PacketBufferCost (buffered);
                    AckNumber = next;
                    ackAdvanced = true;
                }

                var pkt = buffered!;
                if (pkt.Packet.Type == PacketType.Fin) {
                    State = ConnectionState.FinReceived;
                    ReceivedPackets.Writer.TryComplete ();
                    return (ackAdvanced, true);
                }

                if (pkt.PayloadLength > 0)
                    await ReceivedPackets.Writer.WriteAsync (pkt, cts.Token);
            }
        }

        bool HasSelectiveAckContent (ushort ackNr)
        {
            lock (locker)
                return receiveBuffer.Keys.Any (t => ShouldIncludeInSelectiveAck (t, ackNr));
        }

        void ScheduleDelayedAck ()
        {
            CancellationTokenSource? scheduled;
            lock (locker) {
                if (delayedAckCancellation != null || cts.IsCancellationRequested)
                    return;

                scheduled = CancellationTokenSource.CreateLinkedTokenSource (cts.Token);
                delayedAckCancellation = scheduled;
            }

            _ = SendDelayedAckAsync (scheduled);
        }

        async Task SendDelayedAckAsync (CancellationTokenSource scheduled)
        {
            try {
                await Task.Delay (transportSettings.DelayedAckDelay, scheduled.Token);

                lock (locker) {
                    if (delayedAckCancellation != scheduled)
                        return;

                    delayedAckCancellation = null;
                }

                await SendAckAsync (AckNumber);
            } catch (OperationCanceledException) {
            } finally {
                scheduled.Dispose ();
            }
        }

        void CancelDelayedAck ()
        {
            CancellationTokenSource? pending;
            lock (locker) {
                pending = delayedAckCancellation;
                delayedAckCancellation = null;
            }

            pending?.Cancel ();
        }

        async ReusableTask SendImmediateAckAsync ()
        {
            CancelDelayedAck ();
            await SendAckAsync (AckNumber);
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

        async Task SendKeepAliveAsync ()
            => await SendAckAsync (unchecked((ushort) (AckNumber - 1)));

        byte[] CreateSelectiveAckExtension (ushort ackNr)
        {
            ushort[] buffered;
            lock (locker)
                buffered = receiveBuffer.Keys.Where (t => ShouldIncludeInSelectiveAck (t, ackNr)).ToArray ();

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

        bool ShouldIncludeInSelectiveAck (ushort sequenceNumber, ushort ackNr)
        {
            if (!SequenceGreaterThan (sequenceNumber, unchecked((ushort) (ackNr + 1))))
                return false;

            return !ReceivedFinSequence.HasValue || !SequenceGreaterThan (sequenceNumber, ReceivedFinSequence.Value);
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

        ParsedPacket? currentPacket;
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

            if (CleanReadEof && currentPacket == null)
                return 0;

            try {
                if (currentPacket == null) {
                    currentPacket = await ReceivedPackets.Reader.ReadAsync (cts.Token);
                    currentPayloadRead = 0;
                    lock (locker) {
                        QueuedInOrderBytes -= PacketBufferCost (currentPacket);
                        CurrentUnreadPacketBytes += PacketBufferCost (currentPacket);
                    }
                }
            } catch (ChannelClosedException) {
                return 0;
            } catch (OperationCanceledException) when (State == ConnectionState.Reset) {
                return 0;
            }

            int read = ReadFromPacket (currentPacket.Payload.Span.Slice (currentPayloadRead), buffer.Span);
            currentPayloadRead += read;
            lock (locker)
                CurrentUnreadPacketBytes = Math.Max (0, CurrentUnreadPacketBytes - read);
            if (currentPayloadRead == currentPacket.PayloadLength) {
                lock (locker)
                    CurrentUnreadPacketBytes = 0;
                currentPacket = null;
            }

            return read;
        }

        public async ReusableTask<int> SendAsync (ReadOnlyMemory<byte> buffer)
        {
            if (IsClosedOrReset || State == ConnectionState.FinSent || State == ConnectionState.FinReceived)
                return 0;

            int totalSent = 0;

            while (!buffer.IsEmpty) {
                var (payloadLen, isMtuProbe) = SelectPayloadSize (buffer.Length);
                await WaitForSendWindow (payloadLen);
                CancelDelayedAck ();

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

                RegisterSent (pkt, payloadLen, isMtuProbe);
                await SendPacketAsync (pkt);

                buffer = buffer.Slice (payloadLen);
                totalSent += payloadLen;
            }

            return totalSent;
        }

        (int PayloadLength, bool IsMtuProbe) SelectPayloadSize (int remainingBytes)
        {
            if (!transportSettings.EnablePathMtuDiscovery || MtuProbeSequence != null || MtuCeiling - MtuFloor <= MtuConvergedThreshold)
                return (Math.Min (remainingBytes, CurrentMtu), false);

            if (unchecked(clock.Microseconds - NextMtuProbeAt) < 0x8000_0000u) {
                var probeSize = MtuFloor + (MtuCeiling - MtuFloor + 1) / 2;
                if (remainingBytes >= probeSize)
                    return (probeSize, true);
            }

            return (Math.Min (remainingBytes, CurrentMtu), false);
        }

        internal async ReusableTask SendFinAsync ()
        {
            if (IsClosedOrReset || State == ConnectionState.FinSent)
                return;

            CancelDelayedAck ();

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
                var packetCost = payloadLen + UtpPacket.HeaderSize;
                var allowed = Math.Min (MaxWindow, PeerWindowSize);
                if (PeerWindowSize != 0 && (CurrentWindow + packetCost <= allowed || CurrentWindow == 0 && packetCost <= PeerWindowSize))
                    return;

                if (PeerWindowSize == 0 && CanSendZeroWindowProbe ())
                    return;

                await sendWindowChanged.WaitAsync (TimeSpan.FromMilliseconds (100), cts.Token);
            }
            cts.Token.ThrowIfCancellationRequested ();
        }

        bool CanSendZeroWindowProbe ()
        {
            var now = clock.Microseconds;
            if (unchecked(now - LastZeroWindowProbeMicroseconds) < (uint) transportSettings.ZeroWindowProbeInterval.TotalMicroseconds)
                return false;

            LastZeroWindowProbeMicroseconds = now;
            return true;
        }

        async Task RetransmitLoopAsync ()
        {
            try {
                while (!cts.IsCancellationRequested) {
                    await Task.Delay (50, cts.Token);

                    SentPacket? timedOut = null;
                    bool mtuProbeOnlyTimedOut = false;
                    lock (locker) {
                        foreach (var packet in sentPackets.Values.OrderBy (t => t.SentAtMicroseconds)) {
                            if (unchecked(clock.Microseconds - packet.SentAtMicroseconds) >= RetransmitTimeoutMicroseconds) {
                                timedOut = packet;
                                break;
                            }
                        }

                        mtuProbeOnlyTimedOut = timedOut?.IsMtuProbe == true && timedOut.Packet.SequenceNumber == MtuProbeSequence && sentPackets.Count == 1;
                        if (mtuProbeOnlyTimedOut) {
                            HandleMtuProbeTimeout (timedOut!);
                        } else if (timedOut != null) {
                            CurrentMtu = UtpTransportSettings.MinimumRecoveryPacketSize;
                            MaxWindow = (uint) (UtpTransportSettings.MinimumRecoveryPacketSize + UtpPacket.HeaderSize);
                            ConsecutiveTimeouts++;
                            if (ConsecutiveTimeouts < MaxConsecutiveTimeouts)
                                RetransmitTimeoutMicroseconds = Math.Min (
                                    MaximumRetransmitTimeoutMicroseconds,
                                    Math.Max (MinimumRetransmitTimeoutMicroseconds, RetransmitTimeoutMicroseconds) * 2);
                        }
                    }

                    if (timedOut != null && ConsecutiveTimeouts >= MaxConsecutiveTimeouts) {
                        Close (ConnectionState.Reset);
                        continue;
                    }

                    if (timedOut != null)
                        await RetransmitAsync (timedOut.Packet);
                    else if (ShouldSendKeepAlive ())
                        await SendKeepAliveAsync ();
                }
            } catch (OperationCanceledException) {
            }
        }

        void HandleMtuProbeTimeout (SentPacket timedOut)
        {
            if (timedOut.Packet.SequenceNumber != MtuProbeSequence)
                return;

            MtuCeiling = Math.Max (MtuFloor, timedOut.PayloadBytes - 1);
            MtuProbeSequence = null;
            MtuProbeSize = 0;
            CurrentMtu = MtuFloor;

            if (MtuCeiling - MtuFloor <= MtuConvergedThreshold)
                NextMtuProbeAt = unchecked(clock.Microseconds + (uint) transportSettings.MtuProbeInterval.TotalMicroseconds);
            else
                NextMtuProbeAt = clock.Microseconds;
        }

        bool ShouldSendKeepAlive ()
        {
            if (State != ConnectionState.Connected)
                return false;

            if (!HasSentPacket)
                return false;

            if (CurrentWindow != 0)
                return false;

            return unchecked(clock.Microseconds - LastSentPacketMicroseconds) >= (uint) transportSettings.KeepAliveInterval.TotalMicroseconds;
        }

        int MaxConsecutiveTimeouts
            => State == ConnectionState.SynSent ? transportSettings.MaxSynTimeouts : transportSettings.MaxConnectedTimeouts;

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

        void CloseCleanly ()
        {
            if (cts.IsCancellationRequested)
                return;

            CancelDelayedAck ();
            State = ConnectionState.Closed;
            CleanReadEof = true;
            ReceivedPackets.Writer.TryComplete ();
            _listener?.Unregister (this);
            HandshakeCompleted?.TrySetResult (false);
            cts.Cancel ();
            sendWindowChanged.Release ();
        }

        void Close (ConnectionState finalState)
        {
            if (cts.IsCancellationRequested)
                return;

            CancelDelayedAck ();
            State = finalState;
            if (finalState == ConnectionState.Reset)
                ReceivedPackets.Writer.TryComplete ();
            _listener?.Unregister (this);
            HandshakeCompleted?.TrySetResult (false);
            cts.Cancel ();
            if (finalState != ConnectionState.Reset)
                ReceivedPackets.Writer.TryComplete ();
            sendWindowChanged.Release ();
        }
    }
}
