#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using System.Threading.Tasks;

using MonoTorrent.Connections.Peer.Utp;

using NUnit.Framework;

namespace MonoTorrent.Connections.Peer
{
    [TestFixture]
    public class UtpPeerConnectionTests
    {
        [Test]
        public async Task ReceiveOutOfOrderPackets ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (3, "b"));
            connection.Receive (CreateDataPacket (2, "a"));

            var buffer = new byte[2];
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (0, 1)).WithTimeout (10_000));
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (1, 1)).WithTimeout (10_000));
            Assert.AreEqual ("ab", System.Text.Encoding.ASCII.GetString (buffer));
        }

        [Test]
        public async Task ReceiveOutOfOrderPacketSendsSelectiveAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (4, "d"));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (1, ack.packet.AckNumber);
            Assert.AreEqual (1, ack.packet.Extension);
            Assert.AreEqual (0b_0000_0010, ack.packet.AsMemory ().Span[UtpPacket.HeaderSize + 2]);
        }

        [Test]
        public async Task AckOnlyPacketsDoNotConsumeSequenceNumbers ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (2, "a"));
            connection.Receive (CreateDataPacket (3, "b"));

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, first.packet.Type);
            Assert.AreEqual (PacketType.State, second.packet.Type);
            Assert.AreEqual (2, first.packet.AckNumber);
            Assert.AreEqual (3, second.packet.AckNumber);
            Assert.AreEqual (first.packet.SequenceNumber, second.packet.SequenceNumber);
        }

        [Test]
        public async Task AckOnlyPacketDoesNotCreateGapBeforeNextDataPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (2, "a"));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (ack.packet.SequenceNumber, data.packet.SequenceNumber);
        }

        [Test]
        public async Task SelectiveAckBitOrderingMatchesBep29 ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 10);

            connection.Receive (CreateDataPacket (12, "a"));
            connection.Receive (CreateDataPacket (13, "b"));
            connection.Receive (CreateDataPacket (20, "c"));

            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var bytes = ack.packet.AsMemory ().ToArray ();
            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (10, ack.packet.AckNumber);
            Assert.AreEqual (1, ack.packet.Extension);
            Assert.AreEqual (0, bytes[UtpPacket.HeaderSize]);
            Assert.AreEqual (4, bytes[UtpPacket.HeaderSize + 1]);
            Assert.AreEqual (0b_0000_0011, bytes[UtpPacket.HeaderSize + 2]);
            Assert.AreEqual (0b_0000_0001, bytes[UtpPacket.HeaderSize + 3]);
        }

        [Test]
        public async Task ReceiveWindowAccountsForBufferedPacketBytes ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (3, "abcd"));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (UtpPeerConnectionListener.INITIAL_WINDOW - UtpPacket.HeaderSize - 4, ack.packet.WindowSize);
        }

        [Test]
        public async Task ReorderBufferDropsPacketsBeyondLimit ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                maxReceiveBufferBytes: UtpPacket.HeaderSize + 1);

            connection.Receive (CreateDataPacket (3, "a"));
            connection.Receive (CreateDataPacket (4, "b"));

            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (1, ack.packet.AckNumber);
            Assert.AreEqual (1, ack.packet.Extension);
            Assert.AreEqual (0b_0000_0001, ack.packet.AsMemory ().Span[UtpPacket.HeaderSize + 2]);
        }

        [Test]
        public async Task FinWaitsForPriorDataBeforeEof ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (2, "a"));
            connection.Receive (CreateFinPacket (4));

            var buffer = new byte[1];
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer).WithTimeout (10_000));
            Assert.AreEqual ("a", System.Text.Encoding.ASCII.GetString (buffer));

            var blockedRead = connection.ReceiveAsync (buffer).AsTask ().WithTimeout (10_000);
            Assert.IsFalse (await Task.WhenAny (blockedRead, Task.Delay (100)) == blockedRead);

            connection.Receive (CreateDataPacket (3, "b"));

            Assert.AreEqual (1, await blockedRead);
            Assert.AreEqual ("b", System.Text.Encoding.ASCII.GetString (buffer));
            Assert.AreEqual (0, await connection.ReceiveAsync (buffer).WithTimeout (10_000));
        }

        [Test]
        public async Task UnackedDataPacketIsRetransmitted ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1, 2, 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Data, first.packet.Type);
            Assert.AreEqual (first.packet.SequenceNumber, second.packet.SequenceNumber);
            Assert.IsTrue (first.packet.Payload.SequenceEqual (second.packet.Payload));
        }

        [Test]
        public async Task CumulativeAckReleasesPacketsAndBytesInFlight ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1, 2, 3 }).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (UtpPacket.HeaderSize + 3, connection.BytesInFlightForTests);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (0, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task SelectiveAckReleasesSackedPacketAndLeavesGapInFlight ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber, third.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (second.packet.SequenceNumber, unchecked((ushort) (first.packet.SequenceNumber + 1)));
            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task AckMatchingInitialAckNumberDoesNotCountAsDuplicateWhenItReleasesPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (connection.InitialSequenceNumber, first.packet.SequenceNumber);
            Assert.AreEqual (unchecked((ushort) (first.packet.SequenceNumber + 1)), second.packet.SequenceNumber);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber));
            connection.Receive (CreateStatePacket (123, sequenceNumber: 10, ackNumber: first.packet.SequenceNumber));
            connection.Receive (CreateStatePacket (123, sequenceNumber: 11, ackNumber: first.packet.SequenceNumber));
            await Task.Delay (50);

            if (sendQueue.Reader.TryRead (out var retransmit))
                Assert.Fail ($"Unexpected fast retransmit of packet {retransmit.packet.SequenceNumber}");

            connection.Receive (CreateStatePacket (123, sequenceNumber: 12, ackNumber: first.packet.SequenceNumber));
            retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (second.packet.SequenceNumber, retransmit.packet.SequenceNumber);
        }

        [Test]
        public async Task ThreeSackIndicationsFastRetransmitMissingPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            for (int i = 0; i < 3; i++)
                connection.Receive (CreateStatePacket (123, sequenceNumber: (ushort) (9 + i), ackNumber: first.packet.SequenceNumber, third.packet.SequenceNumber));

            var retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (second.packet.SequenceNumber, retransmit.packet.SequenceNumber);
            Assert.AreEqual (PacketType.Data, retransmit.packet.Type);
            Assert.AreEqual (second.packet.Payload.ToArray (), retransmit.packet.Payload.ToArray ());
        }

        [Test]
        public async Task TimeoutRetransmitsOldestPacketAndBacksOffRto ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;
            var retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (first.packet.SequenceNumber, retransmit.packet.SequenceNumber);
            Assert.AreEqual (150, connection.CurrentMtuForTests);
            Assert.AreEqual (2_000_000, connection.RetransmitTimeoutMicrosecondsForTests);
        }

        [Test]
        public async Task RtoUsesBep29MinimumAfterShortRttSample ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 100;
            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (500_000, connection.RetransmitTimeoutMicrosecondsForTests);
        }

        [Test]
        public async Task DefaultTransportSettingUsesConservativeInitialPacketSize ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize + 1]).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, first.packet.Payload.Length);
            Assert.AreEqual (1, second.packet.Payload.Length);
        }

        [Test]
        public async Task TransportSettingControlsInitialPacketSize ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { InitialPacketSize = 512 });

            await connection.SendAsync (new byte[513]).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (512, first.packet.Payload.Length);
            Assert.AreEqual (1, second.packet.Payload.Length);
        }

        [Test]
        public void ListenerPassesTransportSettingToConnections ()
        {
            var listener = new UtpPeerConnectionListener (
                new IPEndPoint (IPAddress.Loopback, 0),
                new UtpTransportSettings { InitialPacketSize = 512 });
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.AreEqual (512, connection.CurrentMtuForTests);
        }

        [TestCase ("127.0.0.1", "ipv4")]
        [TestCase ("::1", "ipv6")]
        public void UriUsesPeerAddressScheme (string address, string scheme)
        {
            using var connection = new UtpPeerConnection (
                Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ().Writer,
                new IPEndPoint (IPAddress.Parse (address), 12345),
                124,
                123,
                1);

            Assert.AreEqual (scheme, connection.Uri.Scheme);
            Assert.AreEqual (IPAddress.Parse (address), IPAddress.Parse (connection.Uri.Host));
            Assert.AreEqual (12345, connection.Uri.Port);
        }

        [Test]
        public void InitialPacketSizeCannotBeBelowRecoveryMinimum ()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException> (() => UtpTransportSettings.Create (new UtpTransportSettings {
                InitialPacketSize = UtpTransportSettings.MinimumRecoveryPacketSize - 1
            }));

            Assert.AreEqual ("settings", ex!.ParamName);
        }

        [Test]
        public async Task LedbatGrowsWindowWhenDelayIsBelowTarget ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            var initialWindow = connection.MaxWindowForTests;

            await connection.SendAsync (new byte[1400]).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber);
            ack.TimestampDiff = 0;
            connection.Receive (ack);
            await Task.Delay (50);

            Assert.Greater (connection.MaxWindowForTests, initialWindow);
        }

        [Test]
        public async Task LedbatYieldsWhenDelayRisesAboveTarget ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            await connection.SendAsync (new byte[1400]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var lowDelayAck = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            lowDelayAck.TimestampDiff = 10_000;
            connection.Receive (lowDelayAck);
            await Task.Delay (50);

            var afterLowDelay = connection.MaxWindowForTests;

            await connection.SendAsync (new byte[1400]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var highDelayAck = CreateStatePacket (123, sequenceNumber: 10, ackNumber: second.packet.SequenceNumber);
            highDelayAck.TimestampDiff = 220_000;
            connection.Receive (highDelayAck);
            await Task.Delay (50);

            Assert.Less (connection.MaxWindowForTests, afterLowDelay);
        }

        [Test]
        public async Task LedbatBaseDelayExpiresAfterTwoMinutes ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            await connection.SendAsync (new byte[1400]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var lowDelayAck = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            lowDelayAck.TimestampDiff = 10_000;
            connection.Receive (lowDelayAck);
            await Task.Delay (50);

            var afterLowDelay = connection.MaxWindowForTests;

            clock.Microseconds = 120_000_001;

            await connection.SendAsync (new byte[1400]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var highDelayAck = CreateStatePacket (123, sequenceNumber: 10, ackNumber: second.packet.SequenceNumber);
            highDelayAck.TimestampDiff = 220_000;
            connection.Receive (highDelayAck);
            await Task.Delay (50);

            Assert.Greater (connection.MaxWindowForTests, afterLowDelay);
        }

        [Test]
        public async Task LedbatHalvesWindowOnPacketLoss ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            var initialWindow = connection.MaxWindowForTests;

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            for (int i = 0; i < 3; i++)
                connection.Receive (CreateStatePacket (123, sequenceNumber: (ushort) (9 + i), ackNumber: first.packet.SequenceNumber, third.packet.SequenceNumber));

            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (initialWindow / 2, connection.MaxWindowForTests);
        }

        [Test]
        public async Task LedbatRecoversWindowAfterTimeoutMinimum ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (150, connection.MaxWindowForTests);

            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            ack.TimestampDiff = 0;
            connection.Receive (ack);
            await Task.Delay (50);

            Assert.Greater (connection.MaxWindowForTests, 150);
        }

        [Test]
        public async Task PureStatePacketDoesNotConsumeSequenceNumber ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendSynAck (1);
            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);

            var state = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, state.packet.Type);
            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (state.packet.SequenceNumber, data.packet.SequenceNumber);
        }

        [Test]
        public async Task SynAndDataConsumeSequenceNumbers ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            var connectTask = connection.ConnectAsync ().AsTask ();
            var syn = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Syn, syn.packet.Type);
            Assert.AreEqual (connection.ConnectionIdReceive, syn.packet.ConnectionId);
            Assert.AreEqual (connection.InitialSequenceNumber, syn.packet.SequenceNumber);
            Assert.AreEqual (0, syn.packet.AckNumber);

            connection.Receive (CreateStatePacket (connection.ConnectionIdReceive, sequenceNumber: 9, ackNumber: syn.packet.SequenceNumber));
            Assert.IsTrue (await connectTask.WithTimeout (5000));

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var data = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (connection.ConnectionIdSend, data.packet.ConnectionId);
            Assert.AreEqual (unchecked((ushort) (syn.packet.SequenceNumber + 1)), data.packet.SequenceNumber);
            Assert.AreEqual (8, data.packet.AckNumber);
        }

        [Test]
        public async Task IncomingSynAckUsesLibutpConnectionIdsAndSequenceNumber ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 125, 7);

            await connection.SendSynAck (7);
            var state = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, state.packet.Type);
            Assert.AreEqual (124, state.packet.ConnectionId);
            Assert.AreEqual (connection.InitialSequenceNumber, state.packet.SequenceNumber);
            Assert.AreEqual (7, state.packet.AckNumber);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (124, data.packet.ConnectionId);
            Assert.AreEqual (state.packet.SequenceNumber, data.packet.SequenceNumber);
            Assert.AreEqual (7, data.packet.AckNumber);
        }

        [Test]
        public async Task ConnectedPairCanSendMixedPayloadSizes ()
        {
            var localListener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            var remoteListener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            var accepted = new TaskCompletionSource<UtpPeerConnection> (TaskCreationOptions.RunContinuationsAsynchronously);

            remoteListener.ConnectionReceived += (o, e) => accepted.TrySetResult ((UtpPeerConnection) e.Connection);

            try {
                localListener.Start ();
                remoteListener.Start ();
                Assert.NotNull (localListener.LocalEndPoint);
                Assert.NotNull (remoteListener.LocalEndPoint);

                using var local = new UtpPeerConnection (localListener, localListener.SendQueue, remoteListener.LocalEndPoint!, 123);

                Assert.IsTrue (await local.ConnectAsync ().WithTimeout (5000));
                using var remote = await accepted.Task.WithTimeout (5000);

                List<byte[]> expectedResults = new List<byte[]> ();
                foreach (var size in new[] { 68, 100, 3, 16 * 1024 }) {
                    var expected = CreatePayload (size);
                    await local.SendAsync (expected).WithTimeout (10_000);
                    expectedResults.Add (expected);
                }

                foreach (var expected in expectedResults) {
                    var receiveTask = ReceiveExactlyAsync (remote, expected.Length);
                    var actual = await receiveTask.WithTimeout (10_000);
                    CollectionAssert.AreEqual (expected, actual);
                }
            } finally {
                localListener.Stop ();
                remoteListener.Stop ();
                Assert.DoesNotThrowAsync (async () => await Task.WhenAll (localListener.BackgroundTasksForTests).WaitAsync (TimeSpan.FromSeconds (5)));
                Assert.DoesNotThrowAsync (async () => await Task.WhenAll (remoteListener.BackgroundTasksForTests).WaitAsync (TimeSpan.FromSeconds (5)));
            }
        }

        [Test]
        public async Task ClosedPairCanReconnectAndSendMixedPayloadSizes (
            [Values (true, false)] bool closerInitiatesSecondConnection,
            [Values (true, false)] bool gracefulShutdown)
        {
            var localListener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            var remoteListener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            var localAccepted = new TaskCompletionSource<UtpPeerConnection> (TaskCreationOptions.RunContinuationsAsynchronously);
            var remoteAccepted = new TaskCompletionSource<UtpPeerConnection> (TaskCreationOptions.RunContinuationsAsynchronously);

            localListener.ConnectionReceived += (o, e) => localAccepted.TrySetResult ((UtpPeerConnection) e.Connection);
            remoteListener.ConnectionReceived += (o, e) => remoteAccepted.TrySetResult ((UtpPeerConnection) e.Connection);

            UtpPeerConnection? firstLocal = null;
            UtpPeerConnection? firstRemote = null;
            UtpPeerConnection? secondInitiator = null;
            UtpPeerConnection? secondAcceptor = null;

            try {
                localListener.Start ();
                remoteListener.Start ();
                Assert.NotNull (localListener.LocalEndPoint);
                Assert.NotNull (remoteListener.LocalEndPoint);

                firstLocal = new UtpPeerConnection (localListener, localListener.SendQueue, remoteListener.LocalEndPoint!, 123);

                Assert.IsTrue (await firstLocal.ConnectAsync ().WithTimeout (5000));
                firstRemote = await remoteAccepted.Task.WithTimeout (5000);

                if (gracefulShutdown) {
                    await firstLocal.SendFinAsync ();
                    Assert.AreEqual (0, await firstRemote.ReceiveAsync (new byte[1]).WithTimeout (10_000));
                } else {
                    firstRemote.Dispose ();
                    firstLocal.Dispose ();
                }

                if (closerInitiatesSecondConnection) {
                    remoteAccepted = new TaskCompletionSource<UtpPeerConnection> (TaskCreationOptions.RunContinuationsAsynchronously);
                    secondInitiator = new UtpPeerConnection (localListener, localListener.SendQueue, remoteListener.LocalEndPoint!, 456);
                    Assert.IsTrue (await secondInitiator.ConnectAsync ().WithTimeout (5000));
                    secondAcceptor = await remoteAccepted.Task.WithTimeout (5000);
                } else {
                    secondInitiator = new UtpPeerConnection (remoteListener, remoteListener.SendQueue, localListener.LocalEndPoint!, 456);
                    Assert.IsTrue (await secondInitiator.ConnectAsync ().WithTimeout (5000));
                    secondAcceptor = await localAccepted.Task.WithTimeout (5000);
                }

                List<byte[]> expectedResults = new List<byte[]> ();
                foreach (var size in new[] { 68, 100, 3, 16 * 1024 }) {
                    var expected = CreatePayload (size);
                    await secondInitiator.SendAsync (expected).WithTimeout (10_000);
                    expectedResults.Add (expected);
                }

                foreach (var expected in expectedResults) {
                    var receiveTask = ReceiveExactlyAsync (secondAcceptor, expected.Length);
                    var actual = await receiveTask.WithTimeout (10_000);
                    CollectionAssert.AreEqual (expected, actual);
                }
            } finally {
                firstLocal?.Dispose ();
                firstRemote?.Dispose ();
                secondInitiator?.Dispose ();
                secondAcceptor?.Dispose ();
                localListener.Stop ();
                remoteListener.Stop ();
                Assert.DoesNotThrowAsync (async () => await Task.WhenAll (localListener.BackgroundTasksForTests).WaitAsync (TimeSpan.FromSeconds (5)));
                Assert.DoesNotThrowAsync (async () => await Task.WhenAll (remoteListener.BackgroundTasksForTests).WaitAsync (TimeSpan.FromSeconds (5)));
            }
        }

        [Test]
        public async Task FinConsumesSequenceNumber ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendFinAsync ();

            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fin = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (PacketType.Fin, fin.packet.Type);
            Assert.AreEqual (unchecked((ushort) (data.packet.SequenceNumber + 1)), fin.packet.SequenceNumber);
        }

        [Test]
        public async Task InvalidRoutedPacketSendsReset ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));
            listener.ProcessDatagram ((IPEndPoint) connection.EndPoint, CreateDataPacket (2, "x").AsMemory ().ToArray ());

            var reset = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (PacketType.Reset, reset.packet.Type);
            Assert.IsNull (reset.connection);
        }

        [Test]
        public async Task DuplicateSynReusesExistingConnectionAndResendsState ()
        {
            using var harness = new InMemoryUtpHarness ();
            int received = 0;
            harness.Listener.ConnectionReceived += (o, e) => received++;

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            var first = await harness.ReadOutbound ().WithTimeout (5000);

            harness.DeliverDuplicate (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            var second = await harness.ReadOutbound ().WithTimeout (5000);

            Assert.AreEqual (1, received);
            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);
            Assert.AreEqual (PacketType.State, first.packet.Type);
            Assert.AreEqual (PacketType.State, second.packet.Type);
            Assert.AreEqual (7, first.packet.AckNumber);
            Assert.AreEqual (7, second.packet.AckNumber);
        }

        [Test]
        public async Task UnknownConnectionPacketSendsReset ()
        {
            using var harness = new InMemoryUtpHarness ();

            harness.Deliver (CreateDataPacket (2, "x"));

            var reset = await harness.ReadOutbound ().WithTimeout (5000);
            Assert.AreEqual (PacketType.Reset, reset.packet.Type);
            Assert.IsNull (reset.connection);
        }

        [Test]
        public async Task SynCollidingWithOutgoingConnectionSendsReset ()
        {
            using var harness = new InMemoryUtpHarness ();
            using var outgoing = new UtpPeerConnection (harness.Listener, harness.Remote, 123);

            Assert.IsTrue (harness.Listener.TryRegisterOutgoing (outgoing));

            harness.Deliver (CreateSynPacket (connectionId: 122, sequenceNumber: 7));

            var reset = await harness.ReadOutbound ().WithTimeout (5000);
            Assert.AreEqual (PacketType.Reset, reset.packet.Type);
            Assert.IsNull (reset.connection);
        }

        [Test]
        public async Task StaleConnectionsArePrunedAndThenResetUnknownPackets ()
        {
            var clock = new ManualClock ();
            using var harness = new InMemoryUtpHarness (clock);

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            await harness.ReadOutbound ().WithTimeout (5000);
            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);

            clock.Microseconds = 120_000_000;
            harness.Listener.PruneStaleConnections ();

            Assert.AreEqual (0, harness.Listener.RegisteredConnectionCount);

            harness.Deliver (CreateDataPacket (2, "x"));
            var reset = await harness.ReadOutbound ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Reset, reset.packet.Type);
        }

        [Test]
        public async Task InMemoryHarnessCanReorderAndDuplicatePackets ()
        {
            using var harness = new InMemoryUtpHarness ();

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 1));
            await harness.ReadOutbound ().WithTimeout (5000);

            harness.DeliverReordered (
                CreateDataPacket (3, "b", connectionId: 124),
                CreateDataPacket (2, "a", connectionId: 124));
            harness.DeliverDuplicate (CreateDataPacket (3, "b", connectionId: 124));

            var ack1 = await harness.ReadOutbound ().WithTimeout (5000);
            var ack2 = await harness.ReadOutbound ().WithTimeout (5000);
            var ack3 = await harness.ReadOutbound ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, ack1.packet.Type);
            Assert.AreEqual (PacketType.State, ack2.packet.Type);
            Assert.AreEqual (PacketType.State, ack3.packet.Type);
        }

        [Test]
        public async Task InMemoryHarnessCanDropAndDelayAckDelivery ()
        {
            using var harness = new InMemoryUtpHarness ();

            harness.Drop (CreateSynPacket (connectionId: 123, sequenceNumber: 1));
            Assert.IsFalse (harness.Listener.SendQueue.Reader.TryRead (out _));

            harness.DeliverDelayed (CreateSynPacket (connectionId: 123, sequenceNumber: 1));
            var state = await harness.ReadOutbound ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, state.packet.Type);
        }

        [Test]
        public async Task InMemoryHarnessCanDelayAcknowledgements ()
        {
            using var harness = new InMemoryUtpHarness ();
            using var connection = new UtpPeerConnection (harness.Listener, harness.Listener.SendQueue, harness.Remote, 123);

            Assert.IsTrue (harness.Listener.TryRegisterOutgoing (connection));

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var data = await harness.ReadOutbound ().WithTimeout (5000);

            Assert.Greater (connection.BytesInFlightForTests, 0);

            harness.DeliverDelayed (CreateStatePacket (connection.ConnectionIdReceive, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (0, connection.BytesInFlightForTests);
        }

        [Test]
        public void ListenerTracksBackgroundTasksAndStopsGracefully ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));

            listener.Start ();
            Assert.IsNotEmpty (listener.BackgroundTasksForTests);

            listener.Stop ();

            Assert.DoesNotThrowAsync (async () => await Task.WhenAll (listener.BackgroundTasksForTests).WaitAsync (TimeSpan.FromSeconds (5)));
        }

        [Test]
        public async Task StalePacketSendsReset ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            var remote = new IPEndPoint (IPAddress.Loopback, 12345);

            listener.ProcessDatagram (remote, CreateDataPacket (2, "x").AsMemory ().ToArray ());

            var reset = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (PacketType.Reset, reset.packet.Type);
            Assert.IsNull (reset.connection);
        }

        [Test]
        public void DisposeUnregistersConnection ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));
            Assert.IsTrue (listener.IsRegistered (connection));

            connection.Dispose ();

            Assert.IsFalse (listener.IsRegistered (connection));
        }

        [Test]
        public void DisposeCancelsPendingReceive ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);
            var receiveTask = connection.ReceiveAsync (new byte[1]).AsTask ();

            connection.Dispose ();

            Assert.ThrowsAsync<OperationCanceledException> (async () => await receiveTask.WithTimeout (5000));
        }

        [Test]
        public void DisposeCancelsPendingSend ()
        {
            var sendQueue = Channel.CreateBounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> (1);
            Assert.IsTrue (sendQueue.Writer.TryWrite ((new UtpPacket (new byte[UtpPacket.HeaderSize]), null, new IPEndPoint (IPAddress.Loopback, 12345))));

            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);
            var sendTask = connection.SendAsync (new byte[] { 1 }).AsTask ();

            connection.Dispose ();

            Assert.ThrowsAsync<OperationCanceledException> (async () => await sendTask.WithTimeout (5000));
        }

        [Test]
        public void ReceivedResetUnregistersConnection ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));

            connection.Receive (CreateResetPacket (connection.ConnectionIdReceive));

            Assert.IsFalse (listener.IsRegistered (connection));
        }

        [Test]
        public void ReceivedResetWithSendConnectionIdUnregistersConnection ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));

            listener.ProcessDatagram ((IPEndPoint) connection.EndPoint, CreateResetPacket (connection.ConnectionIdSend).AsMemory ().ToArray ());

            Assert.IsFalse (listener.IsRegistered (connection));
        }

        static UtpPacket CreateDataPacket (ushort sequenceNumber, string payload, ushort connectionId = 123)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes (payload);
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize + bytes.Length]) {
                Type = PacketType.Data,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = connectionId,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = 1
            };
            bytes.CopyTo (packet.Payload);
            packet.SetTimestamp ();
            return packet;
        }

        static byte[] CreatePayload (int length)
        {
            var result = new byte[length];
            for (int i = 0; i < result.Length; i++)
                result[i] = (byte) (i % 251);
            return result;
        }

        static async Task<byte[]> ReceiveExactlyAsync (UtpPeerConnection connection, int length)
        {
            var buffer = new byte[length];
            int received = 0;
            while (received < buffer.Length) {
                var read = await connection.ReceiveAsync (buffer.AsMemory (received)).WithTimeout (5000);
                if (read == 0)
                    throw new InvalidOperationException ("The remote uTP connection closed before the expected payload was received.");
                received += read;
            }
            return buffer;
        }

        static UtpPacket CreateStatePacket (ushort connectionId, ushort sequenceNumber, ushort ackNumber, params ushort[] selectiveAcks)
        {
            var extensionLength = selectiveAcks.Length == 0 ? 0 : 6;
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize + extensionLength]) {
                Type = PacketType.State,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                Extension = extensionLength == 0 ? (byte) 0 : (byte) 1,
                ConnectionId = connectionId,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = ackNumber
            };
            if (selectiveAcks.Length > 0) {
                var span = packet.AsMemory ().Span;
                span[UtpPacket.HeaderSize] = 0;
                span[UtpPacket.HeaderSize + 1] = 4;
                foreach (var sequenceNumberAcked in selectiveAcks) {
                    int bit = UtpPeerConnection.SequenceDistance (sequenceNumberAcked, unchecked((ushort) (ackNumber + 2)));
                    span[UtpPacket.HeaderSize + 2 + bit / 8] |= (byte) (1 << (bit % 8));
                }
            }
            packet.SetTimestamp ();
            return packet;
        }

        static UtpPacket CreateSynPacket (ushort connectionId, ushort sequenceNumber)
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize]) {
                Type = PacketType.Syn,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = connectionId,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = 0
            };
            packet.SetTimestamp ();
            return packet;
        }

        static UtpPacket CreateResetPacket (ushort connectionId)
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize]) {
                Type = PacketType.Reset,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = connectionId,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = 2,
                AckNumber = 1
            };
            packet.SetTimestamp ();
            return packet;
        }

        static UtpPacket CreateFinPacket (ushort sequenceNumber)
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize]) {
                Type = PacketType.Fin,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = 123,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = 1
            };
            packet.SetTimestamp ();
            return packet;
        }

        sealed class ManualClock : IUtpClock
        {
            public uint Microseconds { get; set; }
        }

        sealed class InMemoryUtpHarness : IDisposable
        {
            public InMemoryUtpHarness ()
                : this (new ManualClock ())
            {
            }

            public InMemoryUtpHarness (ManualClock clock)
            {
                Clock = clock;
                Listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0), clock);
                Remote = new IPEndPoint (IPAddress.Loopback, 12345);
            }

            public ManualClock Clock { get; }
            public UtpPeerConnectionListener Listener { get; }
            public IPEndPoint Remote { get; }

            public void Deliver (UtpPacket packet)
                => Listener.ProcessDatagram (Remote, packet.AsMemory ().ToArray ());

            public void DeliverDelayed (UtpPacket packet)
            {
                Clock.Microseconds += 50_000;
                Deliver (packet);
            }

            public void DeliverDuplicate (UtpPacket packet)
            {
                Deliver (packet);
                Deliver (packet);
            }

            public void DeliverReordered (params UtpPacket[] packets)
            {
                for (int i = packets.Length - 1; i >= 0; i--)
                    Deliver (packets[i]);
            }

            public void Drop (UtpPacket packet)
            {
            }

            public Task<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ReadOutbound ()
                => Listener.SendQueue.Reader.ReadAsync ().AsTask ();

            public void Dispose ()
                => Listener.Stop ();
        }
    }
}
