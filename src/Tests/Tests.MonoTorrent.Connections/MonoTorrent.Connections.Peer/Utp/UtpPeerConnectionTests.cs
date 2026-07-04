#nullable enable

using System;
using System.Net;
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
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (0, 1)).WithTimeout (5000));
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (1, 1)).WithTimeout (5000));
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
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer).WithTimeout (5000));
            Assert.AreEqual ("a", System.Text.Encoding.ASCII.GetString (buffer));

            var blockedRead = connection.ReceiveAsync (buffer).AsTask ();
            Assert.IsFalse (await Task.WhenAny (blockedRead, Task.Delay (100)) == blockedRead);

            connection.Receive (CreateDataPacket (3, "b"));

            Assert.AreEqual (1, await blockedRead.WithTimeout (5000));
            Assert.AreEqual ("b", System.Text.Encoding.ASCII.GetString (buffer));
            Assert.AreEqual (0, await connection.ReceiveAsync (buffer).WithTimeout (5000));
        }

        [Test]
        public async Task UnackedDataPacketIsRetransmitted ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1, 2, 3 }).WithTimeout (5000);

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

            await connection.SendAsync (new byte[] { 1, 2, 3 }).WithTimeout (5000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (UtpPacket.HeaderSize + 3, connection.BytesInFlightForTests);

            connection.Receive (CreateStatePacket (124, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (0, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task SelectiveAckReleasesSackedPacketAndLeavesGapInFlight ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (5000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (5000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (5000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (124, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber, third.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (second.packet.SequenceNumber, unchecked((ushort) (first.packet.SequenceNumber + 1)));
            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task AckMatchingInitialAckNumberDoesNotCountAsDuplicateWhenItReleasesPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (5000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (5000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (5000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (1, first.packet.SequenceNumber);
            Assert.AreEqual (2, second.packet.SequenceNumber);

            connection.Receive (CreateStatePacket (124, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber));
            connection.Receive (CreateStatePacket (124, sequenceNumber: 10, ackNumber: first.packet.SequenceNumber));
            connection.Receive (CreateStatePacket (124, sequenceNumber: 11, ackNumber: first.packet.SequenceNumber));
            await Task.Delay (50);

            if (sendQueue.Reader.TryRead (out var retransmit))
                Assert.Fail ($"Unexpected fast retransmit of packet {retransmit.packet.SequenceNumber}");

            connection.Receive (CreateStatePacket (124, sequenceNumber: 12, ackNumber: first.packet.SequenceNumber));
            retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (second.packet.SequenceNumber, retransmit.packet.SequenceNumber);
        }

        [Test]
        public async Task ThreeSackIndicationsFastRetransmitMissingPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (5000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (5000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (5000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            for (int i = 0; i < 3; i++)
                connection.Receive (CreateStatePacket (124, sequenceNumber: (ushort) (9 + i), ackNumber: first.packet.SequenceNumber, third.packet.SequenceNumber));

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

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (5000);
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

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (5000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 100;
            connection.Receive (CreateStatePacket (124, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (500_000, connection.RetransmitTimeoutMicrosecondsForTests);
        }

        [Test]
        public async Task PureStatePacketDoesNotConsumeSequenceNumber ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendSynAck (1);
            await connection.SendAsync (new byte[] { 1 });

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
            Assert.AreEqual (1, syn.packet.SequenceNumber);

            connection.Receive (CreateStatePacket (connection.ConnectionIdSend, sequenceNumber: 9, ackNumber: 1));
            Assert.IsTrue (await connectTask.WithTimeout (5000));

            await connection.SendAsync (new byte[] { 1 });
            var data = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (2, data.packet.SequenceNumber);
        }

        [Test]
        public async Task FinConsumesSequenceNumber ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1 });
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
        public void ReceivedResetUnregistersConnection ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));

            connection.Receive (CreateResetPacket (connection.ConnectionIdSend));

            Assert.IsFalse (listener.IsRegistered (connection));
        }

        static UtpPacket CreateDataPacket (ushort sequenceNumber, string payload)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes (payload);
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize + bytes.Length]) {
                Type = PacketType.Data,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = 124,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = 1
            };
            bytes.CopyTo (packet.Payload);
            packet.SetTimestamp ();
            return packet;
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
                ConnectionId = 124,
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
    }
}
