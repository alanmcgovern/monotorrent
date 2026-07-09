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
using System.Buffers;
using System.Collections.Generic;
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

        sealed class LedbatDelayHistory
        {
            const int RecentSampleCapacity = 256;
            const int BaseDelayBuckets = 3;

            readonly uint[] recentReceivedAt = new uint[RecentSampleCapacity];
            readonly uint[] recentDelays = new uint[RecentSampleCapacity];
            readonly long[] baseBucketMinutes = new long[BaseDelayBuckets];
            readonly uint[] baseBucketDelays = new uint[BaseDelayBuckets];

            int nextRecentSample;
            int recentSampleCount;

            public LedbatDelayHistory ()
            {
                for (int i = 0; i < baseBucketMinutes.Length; i++)
                    baseBucketMinutes[i] = -1;
            }

            public void AddSample (uint now, uint delayMicroseconds, out uint recentAverageMicroseconds, out uint baseDelayMicroseconds)
            {
                recentReceivedAt[nextRecentSample] = now;
                recentDelays[nextRecentSample] = delayMicroseconds;
                nextRecentSample = (nextRecentSample + 1) % RecentSampleCapacity;
                if (recentSampleCount < RecentSampleCapacity)
                    recentSampleCount++;

                long currentMinute = now / 60_000_000u;
                int bucket = (int) (currentMinute % BaseDelayBuckets);
                if (baseBucketMinutes[bucket] != currentMinute) {
                    baseBucketMinutes[bucket] = currentMinute;
                    baseBucketDelays[bucket] = delayMicroseconds;
                } else {
                    baseBucketDelays[bucket] = Math.Min (baseBucketDelays[bucket], delayMicroseconds);
                }

                uint recentMinimum = uint.MaxValue;
                ulong recentSum = 0;
                int activeSamples = 0;
                for (int i = 0; i < recentSampleCount; i++) {
                    if (unchecked(now - recentReceivedAt[i]) > DelaySampleLifetimeMicroseconds)
                        continue;

                    var delay = recentDelays[i];
                    recentMinimum = Math.Min (recentMinimum, delay);
                    recentSum += delay;
                    activeSamples++;
                }

                recentAverageMicroseconds = activeSamples == 0 ? delayMicroseconds : (uint) (recentSum / (uint) activeSamples);

                baseDelayMicroseconds = recentMinimum == uint.MaxValue ? delayMicroseconds : recentMinimum;
                for (int i = 0; i < BaseDelayBuckets; i++) {
                    if (baseBucketMinutes[i] < 0)
                        continue;

                    if (currentMinute - baseBucketMinutes[i] >= BaseDelayBuckets - 1)
                        continue;

                    baseDelayMicroseconds = Math.Min (baseDelayMicroseconds, baseBucketDelays[i]);
                }
            }
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
        const uint DelaySampleLifetimeMicroseconds = 120_000_000;
        const int DefaultMaxReceiveBufferBytes = (int) UtpPeerConnectionListener.INITIAL_WINDOW;
        const uint InitialRetransmitTimeoutMicroseconds = 1_000_000;
        const uint MinimumRetransmitTimeoutMicroseconds = 500_000;
        const uint MaximumRetransmitTimeoutMicroseconds = 60_000_000;
        const int MtuConvergedThreshold = 16;

        readonly object locker = new ();
        readonly Dictionary<ushort, SentPacket> sentPackets = new ();
        readonly Dictionary<ushort, ParsedPacket> receiveBuffer = new ();
        readonly LedbatDelayHistory delayHistory = new ();
        readonly SemaphoreSlim sendWindowChanged = new (0);
        readonly CancellationTokenSource cts = new ();
        readonly IUtpClock clock;
        readonly UtpConnectionScheduler scheduler;
        readonly bool ownsScheduler;
        readonly int maxReceiveBufferBytes;
        readonly UtpTransportSettings transportSettings;
        uint? DelayedAckAt { get; set; }
        bool WaitingForSendWindow { get; set; }

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

        public UtpConnectionDiagnosticSnapshot DiagnosticSnapshot {
            get {
                lock (locker) {
                    return new UtpConnectionDiagnosticSnapshot (
                        SendWindowBytes: MaxWindow,
                        PeerWindowBytes: PeerWindowSize,
                        BytesInFlight: BytesInFlight,
                        ReceiveWindowBytes: AdvertisedReceiveWindow,
                        RttMicroseconds: RttMicroseconds,
                        RetransmitTimeoutMicroseconds: RetransmitTimeoutMicroseconds,
                        MtuFloorBytes: MtuFloor,
                        MtuCeilingBytes: MtuCeiling,
                        CurrentMtuBytes: CurrentMtu,
                        RecentDelayMicroseconds: RecentDelayMicroseconds,
                        State: State.ToString ());
                }
            }
        }

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
            scheduler = listener?.Scheduler ?? new UtpConnectionScheduler (clock);
            ownsScheduler = listener == null;
            scheduler.Register (this);
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
            Reschedule ();
        }

        async Task SendPacketAsync (UtpPacket packet)
        {
            packet.WindowSize = AdvertisedReceiveWindow;
            await SendingChannel.WriteAsync ((packet, this, EndPoint), cts.Token);
            LastSentPacketMicroseconds = clock.Microseconds;
            HasSentPacket = true;
            Reschedule ();
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
            var previousPeerWindow = PeerWindowSize;
            PeerWindowSize = pkt.WindowSize;
            if (PeerWindowSize > previousPeerWindow)
                sendWindowChanged.Release ();
            if (!wasPeerWindowZero && PeerWindowSize == 0)
                LastZeroWindowProbeMicroseconds = clock.Microseconds;
            ProcessAcks (pkt, parsed.SelectiveAcks);
            Reschedule ();

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
            bool wasWindowLimited;

            lock (locker) {
                wasWindowLimited = IsWindowLimited ();

                bool ackAdvanced = SequenceGreaterThan (pkt.AckNumber, LastAckReceived);
                if (ackAdvanced)
                    LastAckReceived = pkt.AckNumber;

                ushort[]? sequencesToRemove = null;
                int sequencesToRemoveCount = 0;
                try {
                    if (sentPackets.Count > 0) {
                        sequencesToRemove = ArrayPool<ushort>.Shared.Rent (sentPackets.Count);
                        foreach (var seq in sentPackets.Keys) {
                            if (SequenceLessThanOrEqual (seq, pkt.AckNumber))
                                sequencesToRemove[sequencesToRemoveCount++] = seq;
                        }

                        for (int i = 0; i < sequencesToRemoveCount; i++) {
                            var seq = sequencesToRemove[i];
                            if (sentPackets.Remove (seq, out var sent)) {
                                acked.Add (sent);
                                BytesInFlight -= PacketSendCost (sent);
                            }
                        }
                    }
                } finally {
                    if (sequencesToRemove != null)
                        ArrayPool<ushort>.Shared.Return (sequencesToRemove);
                }

                bool pureDuplicateAck = pkt.Type == PacketType.State && receivedSelectiveAcks.Count == 0 && !ackAdvanced && acked.Count == 0;
                bool sackEvidence = false;
                if (receivedSelectiveAcks.Count > 0) {
                    foreach (var seq in receivedSelectiveAcks) {
                        if (IsSelectiveAckInSendWindow (seq, pkt.AckNumber) && sentPackets.ContainsKey (seq)) {
                            sackEvidence = true;
                            break;
                        }
                    }
                }

                if (pureDuplicateAck || sackEvidence) {
                    foreach (var sent in sentPackets.Values) {
                        int duplicateAckIndications = CountDuplicateAckIndications (sent.Packet.SequenceNumber, pkt.AckNumber, receivedSelectiveAcks, sentPackets, pureDuplicateAck);
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

                foreach (var seq in receivedSelectiveAcks) {
                    if (!IsSelectiveAckInSendWindow (seq, pkt.AckNumber))
                        continue;

                    if (sentPackets.Remove (seq, out var sent)) {
                        acked.Add (sent);
                        BytesInFlight -= PacketSendCost (sent);
                    }
                }

                if (acked.Count > 0)
                    ConsecutiveTimeouts = 0;
            }

            uint minAckedRttMicroseconds = 0;
            int bytesNewlyAcked = 0;
            bool finAcked = false;
            foreach (var sent in acked) {
                bytesNewlyAcked += sent.PayloadBytes;
                if (sent.Packet.Type == PacketType.Fin)
                    finAcked = true;

                var packetRtt = UpdateRtt (sent);
                if (packetRtt != 0 && (minAckedRttMicroseconds == 0 || packetRtt < minAckedRttMicroseconds))
                    minAckedRttMicroseconds = packetRtt;
            }

            ProcessMtuProbeAcks (acked);
            ApplyCongestionControl (bytesNewlyAcked, pkt.TimestampDiff, minAckedRttMicroseconds, wasWindowLimited);

            if (finAcked && State == ConnectionState.FinSent)
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

        bool IsSelectiveAckInSendWindow (ushort selectiveAck, ushort ackNumber)
            => SequenceGreaterThan (selectiveAck, ackNumber) && SequenceLessThanOrEqual (selectiveAck, LastSentSequenceNumber);

        static int CountDuplicateAckIndications (ushort sequenceNumber, ushort ackNumber, List<ushort> selectiveAcks, Dictionary<ushort, SentPacket> sentPackets, bool pureDuplicateAck)
        {
            if (SequenceLessThanOrEqual (sequenceNumber, ackNumber))
                return 0;

            if (selectiveAcks.Count == 0)
                return pureDuplicateAck && sequenceNumber == unchecked((ushort) (ackNumber + 1)) ? 1 : 0;

            int count = 0;
            foreach (var sack in selectiveAcks) {
                if (sentPackets.ContainsKey (sack) && SequenceGreaterThan (sack, sequenceNumber))
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

        bool IsWindowLimited ()
        {
            var allowed = Math.Min (MaxWindow, PeerWindowSize);
            if (allowed == 0)
                return false;

            return BytesInFlight + CurrentMtu + UtpPacket.HeaderSize > allowed;
        }

        void ApplyCongestionControl (int bytesNewlyAcked, uint delayMicroseconds, uint minAckedRttMicroseconds, bool wasWindowLimited)
        {
            if (bytesNewlyAcked == 0 || delayMicroseconds == 0 || minAckedRttMicroseconds == 0)
                return;

            lock (locker) {
                var now = clock.Microseconds;
                var clampedDelay = Math.Min (delayMicroseconds, minAckedRttMicroseconds);
                delayHistory.AddSample (now, clampedDelay, out var recentDelay, out var baseDelay);
                RecentDelayMicroseconds = recentDelay;
                uint ourDelay = RecentDelayMicroseconds > baseDelay ? RecentDelayMicroseconds - baseDelay : 0;
                double offTarget = (long) CControlTargetMicroseconds - ourDelay;
                if (offTarget > 0 && !wasWindowLimited)
                    return;

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
            lock (locker) {
                foreach (var sequenceNumber in receiveBuffer.Keys) {
                    if (ShouldIncludeInSelectiveAck (sequenceNumber, ackNr))
                        return true;
                }

                return false;
            }
        }

        void ScheduleDelayedAck ()
        {
            lock (locker) {
                if (DelayedAckAt.HasValue || cts.IsCancellationRequested)
                    return;

                DelayedAckAt = unchecked(clock.Microseconds + (uint) transportSettings.DelayedAckDelay.TotalMicroseconds);
            }
            Reschedule ();
        }

        void CancelDelayedAck ()
        {
            lock (locker) {
                DelayedAckAt = null;
            }
            Reschedule ();
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
            int maxBit = -1;
            lock (locker) {
                foreach (var seq in receiveBuffer.Keys) {
                    if (!ShouldIncludeInSelectiveAck (seq, ackNr))
                        continue;

                    maxBit = Math.Max (maxBit, SequenceDistance (seq, unchecked((ushort) (ackNr + 2))));
                }

                if (maxBit < 0)
                    return Array.Empty<byte> ();
            }

            int length = Math.Max (4, ((maxBit / 8) + 4) / 4 * 4);
            var result = new byte[2 + length];
            result[0] = 0;
            result[1] = (byte) length;

            lock (locker) {
                foreach (var seq in receiveBuffer.Keys) {
                    if (!ShouldIncludeInSelectiveAck (seq, ackNr))
                        continue;

                    int bit = SequenceDistance (seq, unchecked((ushort) (ackNr + 2)));
                    if (bit <= maxBit)
                        result[2 + bit / 8] |= (byte) (1 << (bit % 8));
                }
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
            try {
                while (!cts.IsCancellationRequested) {
                    var packetCost = payloadLen + UtpPacket.HeaderSize;
                    var allowed = Math.Min (MaxWindow, PeerWindowSize);
                    if (PeerWindowSize != 0 && (CurrentWindow + packetCost <= allowed || CurrentWindow == 0 && packetCost <= PeerWindowSize))
                        return;

                    if (PeerWindowSize == 0 && CanSendZeroWindowProbe ())
                        return;

                    lock (locker)
                        WaitingForSendWindow = true;
                    Reschedule ();
                    await sendWindowChanged.WaitAsync (cts.Token);
                }
            } finally {
                lock (locker)
                    WaitingForSendWindow = false;
                Reschedule ();
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

        internal uint? NextScheduledEventMicroseconds {
            get {
                lock (locker) {
                    if (cts.IsCancellationRequested)
                        return null;

                    uint? result = null;
                    void Add (uint deadline)
                    {
                        if (!result.HasValue || IsBefore (deadline, result.Value))
                            result = deadline;
                    }

                    if (DelayedAckAt.HasValue)
                        Add (DelayedAckAt.Value);

                    foreach (var packet in sentPackets.Values)
                        Add (unchecked(packet.SentAtMicroseconds + RetransmitTimeoutMicroseconds));

                    if (State == ConnectionState.Connected && HasSentPacket && CurrentWindow == 0)
                        Add (unchecked(LastSentPacketMicroseconds + (uint) transportSettings.KeepAliveInterval.TotalMicroseconds));

                    if (WaitingForSendWindow && PeerWindowSize == 0)
                        Add (unchecked(LastZeroWindowProbeMicroseconds + (uint) transportSettings.ZeroWindowProbeInterval.TotalMicroseconds));

                    if (transportSettings.EnablePathMtuDiscovery && MtuProbeSequence == null && MtuCeiling - MtuFloor > MtuConvergedThreshold)
                        Add (NextMtuProbeAt);

                    return result;
                }
            }
        }

        internal async Task ProcessScheduledEventsAsync (uint? forcedDeadline = null)
        {
            try {
                SentPacket? timedOut = null;
                ushort? delayedAck = null;
                bool sendKeepAlive = false;
                bool releaseSendWindow = false;

                lock (locker) {
                    var now = clock.Microseconds;
                    if (DelayedAckAt.HasValue && (IsDue (DelayedAckAt.Value, now) || IsForcedDue (DelayedAckAt.Value, forcedDeadline))) {
                        DelayedAckAt = null;
                        delayedAck = AckNumber;
                    }

                    uint timedOutAge = 0;
                    foreach (var packet in sentPackets.Values) {
                        var age = unchecked(now - packet.SentAtMicroseconds);
                        if (age >= RetransmitTimeoutMicroseconds && (timedOut == null || age > timedOutAge)) {
                            timedOut = packet;
                            timedOutAge = age;
                        }
                    }

                    var mtuProbeOnlyTimedOut = timedOut?.IsMtuProbe == true && timedOut.Packet.SequenceNumber == MtuProbeSequence && sentPackets.Count == 1;
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
                    } else if (ShouldSendKeepAliveLocked () && (IsDue (unchecked(LastSentPacketMicroseconds + (uint) transportSettings.KeepAliveInterval.TotalMicroseconds), now) || IsForcedDue (unchecked(LastSentPacketMicroseconds + (uint) transportSettings.KeepAliveInterval.TotalMicroseconds), forcedDeadline))) {
                        sendKeepAlive = true;
                    }

                    releaseSendWindow = WaitingForSendWindow && PeerWindowSize == 0
                        && (IsDue (unchecked(LastZeroWindowProbeMicroseconds + (uint) transportSettings.ZeroWindowProbeInterval.TotalMicroseconds), now) || IsForcedDue (unchecked(LastZeroWindowProbeMicroseconds + (uint) transportSettings.ZeroWindowProbeInterval.TotalMicroseconds), forcedDeadline));
                }

                if (timedOut != null && ConsecutiveTimeouts >= MaxConsecutiveTimeouts) {
                    Close (ConnectionState.Reset);
                    return;
                }

                if (delayedAck.HasValue)
                    await SendAckAsync (delayedAck.Value);

                if (timedOut != null)
                    await RetransmitAsync (timedOut.Packet);
                else if (sendKeepAlive)
                    await SendKeepAliveAsync ();

                if (releaseSendWindow)
                    sendWindowChanged.Release ();
            } catch (OperationCanceledException) {
            } finally {
                Reschedule ();
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

        bool ShouldSendKeepAliveLocked ()
        {
            if (State != ConnectionState.Connected)
                return false;

            if (!HasSentPacket)
                return false;

            if (CurrentWindow != 0)
                return false;

            return unchecked(clock.Microseconds - LastSentPacketMicroseconds) >= (uint) transportSettings.KeepAliveInterval.TotalMicroseconds;
        }

        internal void ApplyMtuFeedback (int nextHopMtu)
        {
            var payloadCeiling = nextHopMtu - (EndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? 48 : 28);
            if (payloadCeiling < UtpTransportSettings.MinimumRecoveryPacketSize)
                payloadCeiling = UtpTransportSettings.MinimumRecoveryPacketSize;

            lock (locker) {
                if (payloadCeiling >= MtuCeiling)
                    return;

                MtuCeiling = payloadCeiling;
                if (MtuFloor > MtuCeiling)
                    MtuFloor = MtuCeiling;
                if (CurrentMtu > MtuCeiling)
                    CurrentMtu = MtuCeiling;
                if (MtuProbeSize > MtuCeiling) {
                    MtuProbeSequence = null;
                    MtuProbeSize = 0;
                }

                if (MtuCeiling - MtuFloor <= MtuConvergedThreshold)
                    NextMtuProbeAt = unchecked(clock.Microseconds + (uint) transportSettings.MtuProbeInterval.TotalMicroseconds);
                else
                    NextMtuProbeAt = clock.Microseconds;
            }
            Reschedule ();
        }

        void Reschedule ()
            => scheduler.Reschedule ();

        static bool IsDue (uint deadline, uint now)
            => unchecked(now - deadline) < 0x8000_0000u;

        static bool IsBefore (uint left, uint right)
            => unchecked(left - right) >= 0x8000_0000u;

        static bool IsForcedDue (uint deadline, uint? forcedDeadline)
            => forcedDeadline.HasValue && !IsBefore (forcedDeadline.Value, deadline);

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
            scheduler.Unregister (this);
            if (ownsScheduler)
                scheduler.Dispose ();
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
            scheduler.Unregister (this);
            if (ownsScheduler)
                scheduler.Dispose ();
            _listener?.Unregister (this);
            HandshakeCompleted?.TrySetResult (false);
            cts.Cancel ();
            if (finalState != ConnectionState.Reset)
                ReceivedPackets.Writer.TryComplete ();
            sendWindowChanged.Release ();
        }
    }
}
