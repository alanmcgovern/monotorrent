#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
        public void SharedListenerRoutesBencodedDictionaryToDht ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            ReadOnlyMemory<byte> received = default;
            listener.MessageReceived += (packet, _) => received = packet;
            var datagram = new byte[] { (byte) 'd', (byte) 'e' };

            listener.ProcessDatagram (new IPEndPoint (IPAddress.Loopback, 12345), datagram);
            datagram[0] = 0;

            CollectionAssert.AreEqual (new byte[] { (byte) 'd', (byte) 'e' }, received.ToArray ());
        }

        [Test]
        public async Task ReceiveOutOfOrderPackets ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (3, "b"));
            connection.Receive (CreateDataPacket (2, "a"));

            var buffer = new byte[2];
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (0, 1)).WithTimeout (10_000));
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (1, 1)).WithTimeout (10_000));
            Assert.AreEqual ("ab", System.Text.Encoding.ASCII.GetString (buffer));
        }

        [Test]
        public async Task ReceiveFillsBufferAcrossPackets ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);
            connection.Receive (CreateDataPacket (2, "ab"));
            connection.Receive (CreateDataPacket (3, "cd"));

            var buffer = new byte[4];
            Assert.AreEqual (4, await connection.ReceiveAsync (buffer).WithTimeout (10_000));
            Assert.AreEqual ("abcd", System.Text.Encoding.ASCII.GetString (buffer));
        }

        [Test]
        public async Task ReceiveReturnsPartialBufferAtCleanEof ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);
            connection.Receive (CreateDataPacket (2, "ab"));
            connection.Receive (CreateFinPacket (3));

            var buffer = new byte[4];
            Assert.AreEqual (2, await connection.ReceiveAsync (buffer).WithTimeout (10_000));
            Assert.AreEqual ("ab", System.Text.Encoding.ASCII.GetString (buffer, 0, 2));
        }

        [Test]
        public async Task ReceiveOutOfOrderPacketSendsSelectiveAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { DelayedAckDelay = TimeSpan.FromSeconds (30) });

            connection.Receive (CreateDataPacket (4, "d"));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (500);
            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (1, ack.packet.AckNumber);
            Assert.AreEqual (1, ack.packet.Extension);
            Assert.AreEqual (0b_0000_0010, ack.packet.AsMemory ().Span[UtpPacket.HeaderSize + 2]);
        }

        [Test]
        public async Task AckOnlyPacketsDoNotConsumeSequenceNumbers ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (4, "a"));
            connection.Receive (CreateDataPacket (5, "b"));

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, first.packet.Type);
            Assert.AreEqual (PacketType.State, second.packet.Type);
            Assert.AreEqual (1, first.packet.AckNumber);
            Assert.AreEqual (1, second.packet.AckNumber);
            Assert.AreEqual (first.packet.SequenceNumber, second.packet.SequenceNumber);
        }

        [Test]
        public async Task AckOnlyPacketDoesNotCreateGapBeforeNextDataPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
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
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
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
            CollectionAssert.AreEqual (
                new byte[] { 0b_0000_0011, 0b_0000_0001, 0, 0 },
                bytes.AsSpan (UtpPacket.HeaderSize + 2, 4).ToArray ());
        }

        [Test]
        public async Task UnknownExtensionIsSkippedBeforeSelectiveAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var firstSequence = first.packet.SequenceNumber;
            var secondSequence = second.packet.SequenceNumber;
            var thirdSequence = third.packet.SequenceNumber;

            connection.Receive (CreateStatePacketWithExtensions (123, 9, firstSequence,
                new ExtensionBlock (99, new byte[] { 1, 2, 3 }),
                new ExtensionBlock (1, new byte[] { 0b_0000_0001, 0, 0, 0 })));
            await Task.Delay (50);

            Assert.AreEqual (secondSequence, unchecked((ushort) (firstSequence + 1)));
            Assert.AreEqual (thirdSequence, unchecked((ushort) (firstSequence + 2)));
            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task MalformedExtensionLengthDropsPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateMalformedExtensionPacket (123, 9, data.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
            await AssertNoOutboundPacket (sendQueue);
        }

        [Test]
        public async Task OneByteSelectiveAckIsAccepted ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var firstSequence = first.packet.SequenceNumber;
            var secondSequence = second.packet.SequenceNumber;
            var thirdSequence = third.packet.SequenceNumber;

            connection.Receive (CreateStatePacketWithExtensions (123, 9, firstSequence,
                new ExtensionBlock (1, new byte[] { 0b_0000_0001 })));
            await Task.Delay (50);

            Assert.AreEqual (secondSequence, unchecked((ushort) (firstSequence + 1)));
            Assert.AreEqual (thirdSequence, unchecked((ushort) (firstSequence + 2)));
            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task ExtensionBitsAreParsedAndIgnored ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacketWithExtensions (123, 9, data.packet.SequenceNumber,
                new ExtensionBlock (2, new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })));
            await Task.Delay (50);

            Assert.AreEqual (0, connection.BytesInFlightForTests);
            Assert.AreEqual (0x0123456789abcdefUL, connection.PeerExtensionBitsForTests);
        }

        [Test]
        public async Task DataPacketWithExtensionsDeliversOnlyPayload ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacketWithExtensions (2, "ok",
                new ExtensionBlock (99, new byte[] { 1, 2, 3 }),
                new ExtensionBlock (2, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 })));

            var buffer = new byte[2];
            Assert.AreEqual (2, await connection.ReceiveAsync (buffer).WithTimeout (10_000));
            Assert.AreEqual ("ok", System.Text.Encoding.ASCII.GetString (buffer));
            Assert.AreEqual (1UL, connection.PeerExtensionBitsForTests);
        }

        [Test]
        public async Task DataPacketWithOneByteSelectiveAckExtensionAdvancesReceiveAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                0,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { EnableDelayedAcks = false });

            connection.Receive (CreateDataPacketWithExtensions (1, "ok",
                new ExtensionBlock (1, new byte[] { 0b_0100_0000 })));

            var buffer = new byte[2];
            Assert.AreEqual (2, await connection.ReceiveAsync (buffer).WithTimeout (10_000));
            Assert.AreEqual ("ok", System.Text.Encoding.ASCII.GetString (buffer));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (1, ack.packet.AckNumber);
        }

        [Test]
        public async Task ReceiveWindowAccountsForBufferedPacketBytes ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (3, "abcd"));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (UtpTransportSettings.DefaultMaxReceiveBufferBytes - 4, ack.packet.WindowSize);
        }

        [Test]
        public async Task ReceiveWindowAccountsForQueuedInOrderBytesUntilAppReads ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                maxReceiveBufferBytes: 3,
                transportSettings: new UtpTransportSettings { EnableDelayedAcks = false });

            connection.Receive (CreateDataPacket (2, "abc"));

            var zeroWindow = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (0, zeroWindow.packet.WindowSize);

            connection.Receive (CreateDataPacket (2, "abc"));
            zeroWindow = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (0, zeroWindow.packet.WindowSize);

            var buffer = new byte[1];
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer).WithTimeout (10_000));

            var reopened = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (1, reopened.packet.WindowSize);

            Assert.AreEqual (1, await connection.ReceiveAsync (buffer).WithTimeout (10_000));
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer).WithTimeout (10_000));

            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (100));
        }

        [Test]
        public async Task AppReadSendsReceiveWindowUpdateWhenWindowReopens ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                maxReceiveBufferBytes: 3,
                transportSettings: new UtpTransportSettings { EnableDelayedAcks = false });

            connection.Receive (CreateDataPacket (2, "abc"));

            var zeroWindow = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (0, zeroWindow.packet.WindowSize);

            var buffer = new byte[3];
            Assert.AreEqual (3, await connection.ReceiveAsync (buffer).WithTimeout (10_000));

            var windowUpdate = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (PacketType.State, windowUpdate.packet.Type);
            Assert.AreEqual (3, windowUpdate.packet.WindowSize);
            Assert.AreEqual (2, windowUpdate.packet.AckNumber);
        }

        [Test]
        public async Task AppReadSendsReceiveWindowUpdateAfterWindowExpandsByOneMtu ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                maxReceiveBufferBytes: 5000,
                transportSettings: new UtpTransportSettings { EnableDelayedAcks = false, InitialPacketSize = 1400 });

            connection.Receive (CreateDataPacket (2, new string ('x', 2800)));
            var initialAck = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (2200, initialAck.packet.WindowSize);

            Assert.AreEqual (1399, await connection.ReceiveAsync (new byte[1399]).WithTimeout (10_000));
            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (100));

            Assert.AreEqual (1, await connection.ReceiveAsync (new byte[1]).WithTimeout (10_000));
            var windowUpdate = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (3600, windowUpdate.packet.WindowSize);
        }

        [Test]
        public async Task AppReadCoalescesWindowUpdatesAcrossQueuedPackets ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                maxReceiveBufferBytes: 5000,
                transportSettings: new UtpTransportSettings { EnableDelayedAcks = false });

            connection.Receive (CreateDataPacket (2, new string ('a', 1400)));
            connection.Receive (CreateDataPacket (3, new string ('b', 1400)));
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var buffer = new byte[2800];
            Assert.AreEqual (2800, await connection.ReceiveAsync (buffer).WithTimeout (10_000));

            var windowUpdate = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (5000, windowUpdate.packet.WindowSize);
            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (100));
        }

        [Test]
        public async Task ReceiveCapIncludesQueuedInOrderBytes ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                maxReceiveBufferBytes: 1,
                transportSettings: new UtpTransportSettings { EnableDelayedAcks = false });

            connection.Receive (CreateDataPacket (2, "a"));
            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (0, ack.packet.WindowSize);

            connection.Receive (CreateDataPacket (3, "b"));
            ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (2, ack.packet.AckNumber);
            Assert.AreEqual (0, ack.packet.WindowSize);

            var buffer = new byte[2];
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (0, 1)).WithTimeout (10_000));

            var windowUpdate = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (2, windowUpdate.packet.AckNumber);
            Assert.AreEqual (1, windowUpdate.packet.WindowSize);

            connection.Receive (CreateDataPacket (3, "b"));
            ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (3, ack.packet.AckNumber);
            Assert.AreEqual (0, ack.packet.WindowSize);

            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (1, 1)).WithTimeout (10_000));
            Assert.AreEqual ("ab", System.Text.Encoding.ASCII.GetString (buffer));

            windowUpdate = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (3, windowUpdate.packet.AckNumber);
            Assert.AreEqual (1, windowUpdate.packet.WindowSize);
        }

        [Test]
        public async Task ReorderBufferDropsPacketsBeyondLimit ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                maxReceiveBufferBytes: 1);

            connection.Receive (CreateDataPacket (3, "a"));
            connection.Receive (CreateDataPacket (4, "b"));

            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (1, ack.packet.AckNumber);
            Assert.AreEqual (1, ack.packet.Extension);
            Assert.AreEqual (0b_0000_0001, ack.packet.AsMemory ().Span[UtpPacket.HeaderSize + 2]);
        }

        [Test]
        public async Task DuplicateDataPacketSendsAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { DelayedAckDelay = TimeSpan.FromSeconds (30) });

            connection.Receive (CreateDataPacket (2, "a"));
            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (100));

            connection.Receive (CreateDataPacket (2, "a"));
            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (2, ack.packet.AckNumber);
        }

        [Test]
        public async Task SecondInOrderDataPacketProducesImmediateAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { DelayedAckDelay = TimeSpan.FromMilliseconds (100) });

            connection.Receive (CreateDataPacket (2, "a"));
            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (50));

            connection.Receive (CreateDataPacket (3, "b"));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (3, ack.packet.AckNumber);

            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (150));
        }

        [Test]
        public async Task PendingDelayedAckIsPiggybackedOnOutboundData ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { DelayedAckDelay = TimeSpan.FromSeconds (30) });

            connection.Receive (CreateDataPacket (2, "a"));
            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (100));

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);

            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (500);
            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (2, data.packet.AckNumber);

            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (100));
        }

        [Test]
        public async Task StaleDataPacketSendsAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 10);

            connection.Receive (CreateDataPacket (9, "a"));
            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (10, ack.packet.AckNumber);
        }

        [Test]
        public async Task FarFutureDataPacketIsDroppedWithoutAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { MaxReorderDistance = 32 });

            connection.Receive (CreateDataPacket (35, "a"));

            await AssertNoOutboundPacket (sendQueue);
        }

        [Test]
        public async Task FarFutureFinPacketIsDroppedWithoutAck ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { MaxReorderDistance = 32 });

            connection.Receive (CreateFinPacket (35));

            await AssertNoOutboundPacket (sendQueue);
        }

        [Test]
        public async Task FinWaitsForPriorDataBeforeEof ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
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
        public async Task DataBeyondReceivedFinIsIgnoredAndNotSacked ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { DelayedAckDelay = TimeSpan.FromSeconds (30) });

            connection.Receive (CreateFinPacket (4));
            var finAck = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateDataPacket (5, "x"));
            var beyondFinAck = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, finAck.packet.Type);
            Assert.AreEqual (PacketType.State, beyondFinAck.packet.Type);
            Assert.AreEqual (1, beyondFinAck.packet.AckNumber);
            Assert.AreEqual (SelectiveAckByte (finAck.packet), SelectiveAckByte (beyondFinAck.packet));

            connection.Receive (CreateDataPacket (2, "a"));
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            connection.Receive (CreateDataPacket (3, "b"));

            var buffer = new byte[3];
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (0, 1)).WithTimeout (10_000));
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer.AsMemory (1, 1)).WithTimeout (10_000));
            Assert.AreEqual (0, await connection.ReceiveAsync (buffer.AsMemory (2, 1)).WithTimeout (10_000));
            Assert.AreEqual ("ab", System.Text.Encoding.ASCII.GetString (buffer, 0, 2));
        }

        [Test]
        public async Task UnackedDataPacketIsRetransmitted ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1, 2, 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Data, first.packet.Type);
            Assert.AreEqual (first.packet.SequenceNumber, second.packet.SequenceNumber);
            Assert.IsTrue (first.packet.Payload.SequenceEqual (second.packet.Payload));
        }

        [Test]
        public async Task AcknowledgingOldestPacketPromotesNextRetransmitDeadline ()
        {
            var clock = new ManualClock { Microseconds = 100 };
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            clock.Microseconds = 200;
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (100 + connection.RetransmitTimeoutMicrosecondsForTests, connection.NextScheduledEventMicroseconds);

            clock.Microseconds = 300;
            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (200 + connection.RetransmitTimeoutMicrosecondsForTests, connection.NextScheduledEventMicroseconds);
        }

        [Test]
        public void UnchangedRescheduleDoesNotGrowSchedulerHeap ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0), new ManualClock ());
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);
            var initialEntryCount = listener.Scheduler.HeapEntryCountForTests;

            for (int i = 0; i < 100; i++)
                listener.Scheduler.Reschedule (connection);

            Assert.AreEqual (initialEntryCount, listener.Scheduler.HeapEntryCountForTests);
        }

        [Test]
        public async Task RetransmittedDataRefreshesCumulativeAckBeforeSend ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var original = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (1, original.packet.AckNumber);

            connection.AckNumber = 7;
            clock.Microseconds = connection.RetransmitTimeoutMicrosecondsForTests;
            await connection.ProcessScheduledEventsAsync ().WaitAsync (TimeSpan.FromSeconds (5));
            var retransmission = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var packet = retransmission.packet;
            connection.PrepareForSend (ref packet);
            Assert.AreEqual (7, packet.AckNumber);
        }

        [Test]
        public async Task RetransmittedFinRefreshesCumulativeAckBeforeSend ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            await connection.SendFinAsync ();
            var original = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (1, original.packet.AckNumber);

            connection.AckNumber = 9;
            clock.Microseconds = connection.RetransmitTimeoutMicrosecondsForTests;
            await connection.ProcessScheduledEventsAsync ().WaitAsync (TimeSpan.FromSeconds (5));
            var retransmission = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var packet = retransmission.packet;
            connection.PrepareForSend (ref packet);
            Assert.AreEqual (9, packet.AckNumber);
        }

        [Test]
        public async Task CumulativeAckReleasesPacketsAndBytesInFlight ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1, 2, 3 }).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (UtpPacket.HeaderSize + 3, connection.BytesInFlightForTests);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.AreEqual (0, connection.BytesInFlightForTests);
        }

        [Test]
        public void ReceiveBufferSizeCanBeConfigured ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { MaxReceiveBufferBytes = 512 * 1024 });

            Assert.AreEqual (512 * 1024, connection.DiagnosticSnapshot.ReceiveWindowBytes);
        }

        [Test]
        public async Task DiagnosticSnapshotReportsConnectionState ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            var initial = connection.DiagnosticSnapshot;
            Assert.AreEqual ("SynReceived", initial.State);
            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, initial.SendWindowBytes);
            Assert.AreEqual (UtpPeerConnectionListener.INITIAL_WINDOW, initial.PeerWindowBytes);
            Assert.AreEqual (UtpTransportSettings.DefaultMaxReceiveBufferBytes, initial.ReceiveWindowBytes);
            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, initial.CurrentMtuBytes);

            await connection.SendAsync (new byte[] { 1, 2, 3 }).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var inFlight = connection.DiagnosticSnapshot;
            Assert.AreEqual (UtpPacket.HeaderSize + 3, inFlight.BytesInFlight);
            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, inFlight.MtuFloorBytes);
            Assert.AreEqual (1452, inFlight.MtuCeilingBytes);

            clock.Microseconds = 50_000;
            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber);
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await Task.Delay (50);

            var afterAck = connection.DiagnosticSnapshot;
            Assert.AreEqual (0, afterAck.BytesInFlight);
            Assert.AreEqual (50_000, afterAck.RttMicroseconds);
            Assert.AreEqual (500_000, afterAck.RetransmitTimeoutMicroseconds);
            Assert.AreEqual (0, afterAck.RecentDelayMicroseconds);
        }

        [Test]
        public async Task SelectiveAckReleasesSackedPacketAndLeavesGapInFlight ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var firstSequence = first.packet.SequenceNumber;
            var secondSequence = second.packet.SequenceNumber;
            var thirdSequence = third.packet.SequenceNumber;

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: firstSequence, thirdSequence));
            await Task.Delay (50);

            Assert.AreEqual (secondSequence, unchecked((ushort) (firstSequence + 1)));
            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task UnknownSelectiveAckBitsDoNotCreateLossEvidence ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var firstSequence = first.packet.SequenceNumber;
            var unknown = unchecked((ushort) (second.packet.SequenceNumber + 8));

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: firstSequence));
            await Task.Delay (50);

            for (int i = 0; i < 3; i++)
                connection.Receive (CreateStatePacket (123, sequenceNumber: (ushort) (10 + i), ackNumber: firstSequence, unknown));

            await Task.Delay (100);

            Assert.IsFalse (sendQueue.Reader.TryRead (out _));
            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task FutureCumulativeAckIsIgnored ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var sent = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: unchecked((ushort) (sent.packet.SequenceNumber + 1))));
            await Task.Delay (50);

            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task MaximumReorderDistanceProducesValidSelectiveAckLength ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { MaxReorderDistance = 2015 });

            connection.Receive (CreateDataPacket (2017, "a"));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (1, ack.packet.Extension);
            var selectiveAck = ack.packet.AsMemory ().Slice (UtpPacket.HeaderSize + 2).ToArray ();
            Assert.AreEqual (252, selectiveAck.Length);
            CollectionAssert.AreEqual (new byte[251], selectiveAck.AsSpan (0, 251).ToArray ());
            Assert.AreEqual (0b_0100_0000, selectiveAck[251]);
        }

        [Test]
        public async Task RepeatedAcksPiggybackedOnDataDoNotFastRetransmit ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var firstSequence = first.packet.SequenceNumber;
            var secondSequence = second.packet.SequenceNumber;

            for (int i = 0; i < 3; i++) {
                connection.Receive (CreateDataPacket ((ushort) (2 + i), "x", ackNumber: firstSequence));

                var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
                Assert.AreEqual (PacketType.State, ack.packet.Type);
            }

            await Task.Delay (100);

            if (sendQueue.Reader.TryRead (out var unexpected))
                Assert.Fail ($"Unexpected {unexpected.packet.Type} packet for sequence {unexpected.packet.SequenceNumber}");
            Assert.AreEqual ((UtpPacket.HeaderSize + 1) * 2, connection.BytesInFlightForTests);
            Assert.AreEqual (unchecked((ushort) (firstSequence + 1)), secondSequence);
        }

        [Test]
        public async Task AckMatchingInitialAckNumberDoesNotCountAsDuplicateWhenItReleasesPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
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
        public async Task StaleAckDoesNotFastRetransmit ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 4 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var firstSequence = first.packet.SequenceNumber;
            var secondSequence = second.packet.SequenceNumber;
            var thirdSequence = third.packet.SequenceNumber;

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: secondSequence));

            for (int i = 0; i < 3; i++)
                connection.Receive (CreateStatePacket (123, sequenceNumber: (ushort) (10 + i), ackNumber: firstSequence));

            await Task.Delay (100);

            if (sendQueue.Reader.TryRead (out var retransmit))
                Assert.Fail ($"Unexpected fast retransmit of packet {retransmit.packet.SequenceNumber}");

            Assert.AreEqual ((UtpPacket.HeaderSize + 1) * 2, connection.BytesInFlightForTests);
            Assert.AreEqual (unchecked((ushort) (secondSequence + 1)), thirdSequence);
        }

        [Test]
        public async Task ThreeSackIndicationsFastRetransmitMissingPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 4 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 5 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fourth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fifth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber, third.packet.SequenceNumber));
            connection.Receive (CreateStatePacket (123, sequenceNumber: 10, ackNumber: first.packet.SequenceNumber, fourth.packet.SequenceNumber));
            connection.Receive (CreateStatePacket (123, sequenceNumber: 11, ackNumber: first.packet.SequenceNumber, fifth.packet.SequenceNumber));

            var retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (second.packet.SequenceNumber, retransmit.packet.SequenceNumber);
            Assert.AreEqual (PacketType.Data, retransmit.packet.Type);
            Assert.AreEqual (second.packet.Payload.ToArray (), retransmit.packet.Payload.ToArray ());
        }

        [Test]
        public async Task TimedOutPacketBatchBacksOffOnce ()
        {
            var clock = new ManualClock ();
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0), clock);
            using var connection = new UtpPeerConnection (listener, listener.SendQueue, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var first = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = connection.RetransmitTimeoutMicrosecondsForTests;
            await listener.ProcessScheduledEventsForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            var retransmit1 = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var retransmit2 = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var retransmit3 = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            CollectionAssert.AreEquivalent (
                new[] { first.packet.SequenceNumber, second.packet.SequenceNumber, third.packet.SequenceNumber },
                new[] { retransmit1.packet.SequenceNumber, retransmit2.packet.SequenceNumber, retransmit3.packet.SequenceNumber });
            Assert.AreEqual (2_000_000, connection.RetransmitTimeoutMicrosecondsForTests);
        }

        [Test]
        public async Task TimedOutPacketBatchDrainsThroughRecoveryWindow ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            int recoveryPayloadSize = UtpTransportSettings.DefaultInitialPacketSize - UtpPacket.HeaderSize;

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var warmup = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            clock.Microseconds = 50_000;
            var ack = CreateStatePacket (123, sequenceNumber: 7, ackNumber: warmup.packet.SequenceNumber);
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            await connection.SendAsync (new byte[recoveryPayloadSize]).WithTimeout (10_000);
            var acked = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            await connection.SendAsync (new byte[recoveryPayloadSize]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            clock.Microseconds = 100_000;
            ack = CreateStatePacket (123, sequenceNumber: 8, ackNumber: acked.packet.SequenceNumber);
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            await connection.SendAsync (new byte[recoveryPayloadSize]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            await connection.SendAsync (new byte[recoveryPayloadSize]).WithTimeout (10_000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 100_000 + connection.RetransmitTimeoutMicrosecondsForTests;
            await connection.ProcessScheduledEventsAsync ().WaitAsync (TimeSpan.FromSeconds (5));

            var retransmit1 = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (first.packet.SequenceNumber, retransmit1.packet.SequenceNumber);
            Assert.IsFalse (sendQueue.Reader.TryRead (out _));
            Assert.AreEqual (recoveryPayloadSize + UtpPacket.HeaderSize, connection.BytesInFlightForTests);
            Assert.AreEqual (2, connection.PendingRetransmitCountForTests);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: retransmit1.packet.SequenceNumber));

            var retransmit2 = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (second.packet.SequenceNumber, retransmit2.packet.SequenceNumber);
            Assert.IsFalse (sendQueue.Reader.TryRead (out _));
            Assert.AreEqual (recoveryPayloadSize + UtpPacket.HeaderSize, connection.BytesInFlightForTests);
            Assert.AreEqual (1, connection.PendingRetransmitCountForTests);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 10, ackNumber: retransmit2.packet.SequenceNumber));

            var retransmit3 = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (third.packet.SequenceNumber, retransmit3.packet.SequenceNumber);
            Assert.IsFalse (sendQueue.Reader.TryRead (out _));
            Assert.AreEqual (0, connection.PendingRetransmitCountForTests);
        }

        [Test]
        public async Task OneSackMaskWithThreeLaterPacketsFastRetransmitsMissingPacket ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 4 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 5 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fourth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fifth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber,
                third.packet.SequenceNumber,
                fourth.packet.SequenceNumber,
                fifth.packet.SequenceNumber));

            var retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (second.packet.SequenceNumber, retransmit.packet.SequenceNumber);
            Assert.AreEqual (PacketType.Data, retransmit.packet.Type);
            Assert.AreEqual (second.packet.Payload.ToArray (), retransmit.packet.Payload.ToArray ());
        }

        [Test]
        public async Task TimeoutRetransmitsOldestPacketAndBacksOffRto ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;
            var retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (first.packet.SequenceNumber, retransmit.packet.SequenceNumber);
            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, connection.CurrentMtuForTests);
            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, connection.MaxWindowForTests);
            Assert.AreEqual (2_000_000, connection.RetransmitTimeoutMicrosecondsForTests);
        }

        [Test]
        public async Task IdleConnectedConnectionSendsKeepAliveAckForPreviousSequence ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                7,
                clock,
                transportSettings: new UtpTransportSettings { KeepAliveInterval = TimeSpan.FromMilliseconds (100) });

            clock.Microseconds = 1;
            await connection.SendSynAck (7);
            var synAck = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 100_001;
            var keepAlive = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.State, keepAlive.packet.Type);
            Assert.AreEqual (synAck.packet.SequenceNumber, keepAlive.packet.SequenceNumber);
            Assert.AreEqual (6, keepAlive.packet.AckNumber);
            Assert.AreEqual (0, keepAlive.packet.Payload.Length);
        }

        [Test]
        public async Task ZeroWindowPeerAllowsProbeAfterInterval ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                clock,
                transportSettings: new UtpTransportSettings { ZeroWindowProbeInterval = TimeSpan.FromMilliseconds (100) });

            var zeroWindow = CreateStatePacket (123, sequenceNumber: 9, ackNumber: 0);
            zeroWindow.WindowSize = 0;
            connection.Receive (zeroWindow);
            await Task.Delay (50);

            var sendTask = connection.SendAsync (new byte[] { 1 }).AsTask ();
            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (50));
            Assert.IsFalse (sendTask.IsCompleted);

            clock.Microseconds = 100_000;
            var probe = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (1, await sendTask.WithTimeout (5000));
            Assert.AreEqual (PacketType.Data, probe.packet.Type);
            Assert.AreEqual (1, probe.packet.Payload.Length);
        }

        [Test]
        public async Task SynTimeoutFailureCompletesConnectFalse ()
        {
            var clock = new ManualClock ();
            var listener = new UtpPeerConnectionListener (
                new IPEndPoint (IPAddress.Loopback, 0),
                clock,
                new UtpTransportSettings { MaxSynTimeouts = 2 });
            using var connection = new UtpPeerConnection (listener, listener.SendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            var connectTask = connection.ConnectAsync ().AsTask ();
            var syn = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;
            var retransmit = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (syn.packet.SequenceNumber, retransmit.packet.SequenceNumber);

            clock.Microseconds = 3_000_000;

            Assert.IsFalse (await connectTask.WithTimeout (5000));
            Assert.IsTrue (connection.IsClosedOrReset);
            Assert.IsFalse (listener.IsRegistered (connection));
        }

        [Test]
        public async Task ConnectedTimeoutTeardownCompletesReceiveAndUnregisters ()
        {
            var clock = new ManualClock ();
            var listener = new UtpPeerConnectionListener (
                new IPEndPoint (IPAddress.Loopback, 0),
                clock,
                new UtpTransportSettings { MaxConnectedTimeouts = 1 });
            using var connection = new UtpPeerConnection (listener, listener.SendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            var connectTask = connection.ConnectAsync ().AsTask ();
            var syn = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            connection.Receive (CreateStatePacket (connection.ConnectionIdReceive, 9, syn.packet.SequenceNumber));

            Assert.IsTrue (await connectTask.WithTimeout (5000));

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;

            Assert.AreEqual (0, await connection.ReceiveAsync (new byte[1]).WithTimeout (5000));
            Assert.IsTrue (connection.IsClosedOrReset);
            Assert.IsFalse (listener.IsRegistered (connection));
        }

        [Test]
        public async Task TimeoutExhaustionStopsRetransmitting ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                clock,
                transportSettings: new UtpTransportSettings { MaxConnectedTimeouts = 2 });

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;
            var retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (first.packet.SequenceNumber, retransmit.packet.SequenceNumber);

            clock.Microseconds = 3_000_000;

            Assert.AreEqual (0, await connection.ReceiveAsync (new byte[1]).WithTimeout (5000));

            clock.Microseconds = 60_000_000;
            await Task.Delay (150);

            Assert.IsFalse (sendQueue.Reader.TryRead (out _));
        }

        [Test]
        public async Task RtoUsesBep29MinimumAfterShortRttSample ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
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
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            var sendTask = connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize + 1]).AsTask ();

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber));
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendTask.WithTimeout (10_000);

            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, first.packet.Payload.Length);
            Assert.AreEqual (1, second.packet.Payload.Length);
        }

        [Test]
        public async Task TransportSettingControlsInitialPacketSize ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                new ManualClock (),
                transportSettings: new UtpTransportSettings { InitialPacketSize = 512 });

            var sendTask = connection.SendAsync (new byte[513]).AsTask ();

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber));
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendTask.WithTimeout (10_000);

            Assert.AreEqual (512, first.packet.Payload.Length);
            Assert.AreEqual (1, second.packet.Payload.Length);
        }

        [Test]
        public async Task MtuProbeAckRaisesFloor ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                clock,
                transportSettings: new UtpTransportSettings { InitialPacketSize = 512 });

            await connection.SendAsync (new byte[512]).WithTimeout (10_000);
            var warmup = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            clock.Microseconds = 50_000;
            var warmupAck = CreateStatePacket (123, sequenceNumber: 7, ackNumber: warmup.packet.SequenceNumber);
            warmupAck.TimestampDiff = 10_000;
            connection.Receive (warmupAck);
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            connection.NextMtuProbeAtForTests = 0;
            var expectedProbeSize = connection.MtuFloorForTests + (connection.MtuCeilingForTests - connection.MtuFloorForTests + 1) / 2;

            var sendTask = connection.SendAsync (new byte[expectedProbeSize]).AsTask ();
            var probe = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.Greater (probe.packet.Payload.Length, 512);
            Assert.AreEqual (probe.packet.SequenceNumber, connection.MtuProbeSequenceForTests);
            Assert.IsTrue (connection.IsActiveMtuProbe (probe.packet));

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: probe.packet.SequenceNumber));
            await Task.Delay (50);
            await sendTask.WithTimeout (10_000);

            Assert.AreEqual (probe.packet.Payload.Length, connection.MtuFloorForTests);
            Assert.AreEqual (probe.packet.Payload.Length, connection.CurrentMtuForTests);
            Assert.IsNull (connection.MtuProbeSequenceForTests);
            Assert.IsFalse (connection.IsActiveMtuProbe (probe.packet));
        }

        [Test]
        public async Task MtuProbeTimeoutLowersCeilingWithoutCongestionLoss ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                clock,
                transportSettings: new UtpTransportSettings { InitialPacketSize = 512 });

            await connection.SendAsync (new byte[512]).WithTimeout (10_000);
            var warmup = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            clock.Microseconds = 50_000;
            var warmupAck = CreateStatePacket (123, sequenceNumber: 7, ackNumber: warmup.packet.SequenceNumber);
            warmupAck.TimestampDiff = 10_000;
            connection.Receive (warmupAck);
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            connection.NextMtuProbeAtForTests = 0;
            var initialWindow = connection.MaxWindowForTests;
            var expectedProbeSize = connection.MtuFloorForTests + (connection.MtuCeilingForTests - connection.MtuFloorForTests + 1) / 2;

            await connection.SendAsync (new byte[expectedProbeSize]).WithTimeout (10_000);
            var probe = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;
            var retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (probe.packet.SequenceNumber, retransmit.packet.SequenceNumber);
            Assert.AreEqual (probe.packet.Payload.Length - 1, connection.MtuCeilingForTests);
            Assert.AreEqual (512, connection.CurrentMtuForTests);
            Assert.AreEqual (initialWindow, connection.MaxWindowForTests);
            Assert.IsNull (connection.MtuProbeSequenceForTests);
        }

        [Test]
        public async Task MtuProbeSackLossLowersCeilingWithoutCongestionLoss ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                clock,
                transportSettings: new UtpTransportSettings { InitialPacketSize = 512 });

            int warmupPayloadSize = 512 - UtpPacket.HeaderSize;

            await connection.SendAsync (new byte[512]).WithTimeout (10_000);
            var warmup = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            clock.Microseconds = 50_000;
            var ack = CreateStatePacket (123, sequenceNumber: 7, ackNumber: warmup.packet.SequenceNumber);
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            await connection.SendAsync (new byte[warmupPayloadSize]).WithTimeout (10_000);
            var warmup2 = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await connection.SendAsync (new byte[warmupPayloadSize]).WithTimeout (10_000);
            var warmup3 = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            clock.Microseconds = 100_000;
            ack = CreateStatePacket (123, sequenceNumber: 8, ackNumber: warmup3.packet.SequenceNumber);
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            var initialWindow = connection.MaxWindowForTests;
            connection.NextMtuProbeAtForTests = 0;
            var expectedProbeSize = connection.MtuFloorForTests + (connection.MtuCeilingForTests - connection.MtuFloorForTests + 1) / 2;

            await connection.SendAsync (new byte[expectedProbeSize]).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);

            var probe = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fourth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: unchecked((ushort) (probe.packet.SequenceNumber - 1)),
                second.packet.SequenceNumber,
                third.packet.SequenceNumber,
                fourth.packet.SequenceNumber));

            var retransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (probe.packet.SequenceNumber, retransmit.packet.SequenceNumber);
            Assert.AreEqual (probe.packet.Payload.Length - 1, connection.MtuCeilingForTests);
            Assert.AreEqual (512, connection.CurrentMtuForTests);
            Assert.AreEqual (initialWindow, connection.MaxWindowForTests);
            Assert.IsNull (connection.MtuProbeSequenceForTests);
        }

        [Test]
        public async Task MtuFeedbackLowersCeilingAndKeepsProbeSemantics ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                1,
                clock,
                transportSettings: new UtpTransportSettings { InitialPacketSize = 512 });

            connection.ApplyMtuFeedback (1000);
            connection.NextMtuProbeAtForTests = 0;

            Assert.AreEqual (952, connection.MtuCeilingForTests);
            Assert.AreEqual (1000, connection.MtuCeilingForTests + UtpPacket.HeaderSize + 8 + 20);
            Assert.AreEqual (512, connection.MtuFloorForTests);
            Assert.AreEqual (512, connection.CurrentMtuForTests);

            await connection.SendAsync (new byte[512]).WithTimeout (10_000);
            var warmup = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            clock.Microseconds = 50_000;
            var warmupAck = CreateStatePacket (123, sequenceNumber: 7, ackNumber: warmup.packet.SequenceNumber);
            warmupAck.TimestampDiff = 10_000;
            connection.Receive (warmupAck);
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            var expectedProbeSize = connection.MtuFloorForTests + (connection.MtuCeilingForTests - connection.MtuFloorForTests + 1) / 2;
            await connection.SendAsync (new byte[expectedProbeSize]).WithTimeout (10_000);
            var probe = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (expectedProbeSize, probe.packet.Payload.Length);
            Assert.AreEqual (probe.packet.SequenceNumber, connection.MtuProbeSequenceForTests);
        }

        [Test]
        public void ListenerRoutesMtuFeedbackToRegisteredConnection ()
        {
            var clock = new ManualClock ();
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0), clock);
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));
            Assert.IsTrue (listener.ApplyMtuFeedback ((IPEndPoint) connection.EndPoint, connection.ConnectionIdReceive, 1000));

            Assert.AreEqual (952, connection.MtuCeilingForTests);
        }

        [Test]
        public async Task ListenerSchedulerRetransmitsDueConnection ()
        {
            var clock = new ManualClock ();
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0), clock);
            using var connection = new UtpPeerConnection (listener, listener.SendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var first = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;
            await listener.ProcessScheduledEventsForTests ().WaitAsync (TimeSpan.FromSeconds (5));
            var retransmit = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (first.packet.SequenceNumber, retransmit.packet.SequenceNumber);
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
                Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ().Writer,
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
        public void TransportSettingsUseLibutpCompatibleDefaults ()
        {
            var settings = UtpTransportSettings.Create (null);

            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, settings.InitialPacketSize);
            Assert.AreEqual (512, UtpTransportSettings.DefaultInitialPacketSize);
            Assert.AreEqual (150, UtpTransportSettings.MinimumRecoveryPacketSize);
            Assert.AreEqual (4, settings.MaxConnectedTimeouts);
            Assert.AreEqual (2, settings.MaxSynTimeouts);
            Assert.AreEqual (TimeSpan.FromSeconds (29), settings.KeepAliveInterval);
            Assert.AreEqual (TimeSpan.FromSeconds (15), settings.ZeroWindowProbeInterval);
            Assert.AreEqual (1024, settings.MaxReorderDistance);
            Assert.AreEqual (1024, settings.MaxIncomingSynConnections);
            Assert.AreEqual (1024 * 1024, settings.MaxReceiveBufferBytes);
            Assert.AreEqual (4096, settings.MaxSendQueuePackets);
            Assert.AreEqual (2 * 1024 * 1024, settings.SocketReceiveBufferBytes);
            Assert.AreEqual (1024 * 1024, settings.SocketSendBufferBytes);
            Assert.AreEqual (TimeSpan.FromMilliseconds (10), settings.DelayedAckDelay);
            Assert.AreEqual (TimeSpan.FromMilliseconds (100), settings.CongestionControlTarget);
            Assert.IsTrue (settings.EnableDelayedAcks);
            Assert.IsTrue (settings.EnablePathMtuDiscovery);
            Assert.AreEqual (TimeSpan.FromMinutes (30), settings.MtuProbeInterval);
            Assert.AreEqual (3000, settings.LinearIncreaseBytesPerRtt);
        }

        [Test]
        public void TransportSettingsValidateTuningProperties ()
        {
            AssertInvalidSetting (new UtpTransportSettings { MaxConnectedTimeouts = 0 });
            AssertInvalidSetting (new UtpTransportSettings { MaxSynTimeouts = 0 });
            AssertInvalidSetting (new UtpTransportSettings { MaxReorderDistance = 31 });
            AssertInvalidSetting (new UtpTransportSettings { MaxReorderDistance = 2016 });
            AssertInvalidSetting (new UtpTransportSettings { MaxIncomingSynConnections = 0 });
            AssertInvalidSetting (new UtpTransportSettings { MaxReceiveBufferBytes = 0 });
            AssertInvalidSetting (new UtpTransportSettings { MaxSendQueuePackets = 0 });
            AssertInvalidSetting (new UtpTransportSettings { SocketReceiveBufferBytes = 0 });
            AssertInvalidSetting (new UtpTransportSettings { SocketSendBufferBytes = 0 });
            AssertInvalidSetting (new UtpTransportSettings { LinearIncreaseBytesPerRtt = 0 });
            AssertInvalidSetting (new UtpTransportSettings { KeepAliveInterval = TimeSpan.Zero });
            AssertInvalidSetting (new UtpTransportSettings { KeepAliveInterval = TimeSpan.FromTicks (-1) });
            AssertInvalidSetting (new UtpTransportSettings { ZeroWindowProbeInterval = TimeSpan.Zero });
            AssertInvalidSetting (new UtpTransportSettings { ZeroWindowProbeInterval = TimeSpan.FromTicks (-1) });
            AssertInvalidSetting (new UtpTransportSettings { DelayedAckDelay = TimeSpan.Zero });
            AssertInvalidSetting (new UtpTransportSettings { DelayedAckDelay = TimeSpan.FromTicks (-1) });
            AssertInvalidSetting (new UtpTransportSettings { CongestionControlTarget = TimeSpan.Zero });
            AssertInvalidSetting (new UtpTransportSettings { CongestionControlTarget = TimeSpan.FromTicks (-1) });
            AssertInvalidSetting (new UtpTransportSettings { MtuProbeInterval = TimeSpan.Zero });
            AssertInvalidSetting (new UtpTransportSettings { MtuProbeInterval = TimeSpan.FromTicks (-1) });
        }

        [Test]
        public void ListenerConfiguresSocketBufferSizes ()
        {
            using var socket = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var settings = UtpTransportSettings.Create (new UtpTransportSettings {
                SocketReceiveBufferBytes = 512 * 1024,
                SocketSendBufferBytes = 256 * 1024
            });

            UtpPeerConnectionListener.ConfigureSocketBuffers (socket, settings);

            Assert.GreaterOrEqual (socket.ReceiveBufferSize, settings.SocketReceiveBufferBytes);
            Assert.GreaterOrEqual (socket.SendBufferSize, settings.SocketSendBufferBytes);
        }

        [Test]
        public async Task LedbatDoesNotGrowWindowWhenNotWindowLimited ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber);
            clock.Microseconds = 50_000;
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await Task.Delay (50);

            var initialWindow = connection.MaxWindowForTests;

            await connection.SendAsync (new byte[1]).WithTimeout (10_000);
            data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            ack = CreateStatePacket (123, sequenceNumber: 10, ackNumber: data.packet.SequenceNumber);
            clock.Microseconds = 100_000;
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await Task.Delay (50);

            Assert.AreEqual (initialWindow, connection.MaxWindowForTests);
        }

        [Test]
        public async Task LedbatGrowsWindowWhenDelayIsBelowTargetAndWindowLimited ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            var initialWindow = connection.MaxWindowForTests;
            int payloadSize = UtpTransportSettings.DefaultInitialPacketSize;
            int packetCost = payloadSize;
            int packetsToFillWindow = (int) (initialWindow / (uint) packetCost);

            await connection.SendAsync (new byte[payloadSize * packetsToFillWindow]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            clock.Microseconds = 50_000;
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await Task.Delay (50);

            Assert.Greater (connection.MaxWindowForTests, initialWindow);
        }

        [Test]
        public async Task SlowStartContinuesBeyondInitialReceiveWindowWhenDelayIsBelowTarget ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                0,
                clock,
                transportSettings: new UtpTransportSettings {
                    MaxReceiveBufferBytes = 1024 * 1024,
                    InitialPacketSize = 1400,
                    EnablePathMtuDiscovery = false
                });

            async Task AckOneFullWindow (int i)
            {
                var bytesToSend = Math.Max (1, (int) connection.MaxWindowForTests);
                var sendTask = connection.SendAsync (new byte[bytesToSend]).AsTask ();

                UtpPacket last = default;
                var packetsToDrain = Math.Max (1, bytesToSend / 1400);
                for (int j = 0; j < packetsToDrain; j++) {
                    var sent = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
                    last = sent.packet;
                    sent.sendCompleted?.Invoke ();
                }

                await sendTask.WithTimeout (10_000);

                clock.Microseconds += 50_000;
                var ack = CreateStatePacket (123, sequenceNumber: (ushort) (9 + i), ackNumber: last.SequenceNumber);
                ack.TimestampDiff = 10_000;
                ack.WindowSize = 1024 * 1024;
                connection.Receive (ack);
                await Task.Delay (50);
            }

            int iteration = 0;
            while (connection.MaxWindowForTests < UtpPeerConnectionListener.INITIAL_WINDOW)
                await AckOneFullWindow (iteration++);

            var aboveOldThreshold = connection.MaxWindowForTests;
            await AckOneFullWindow (iteration);

            Assert.Greater (aboveOldThreshold, UtpPeerConnectionListener.INITIAL_WINDOW);
            Assert.Greater (connection.MaxWindowForTests, aboveOldThreshold);
        }

        [Test]
        public async Task LedbatIgnoresZeroDelaySample ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            var initialWindow = connection.MaxWindowForTests;

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 50_000;
            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber);
            ack.TimestampDiff = 0;
            connection.Receive (ack);
            await Task.Delay (50);

            Assert.AreEqual (initialWindow, connection.MaxWindowForTests);
            Assert.AreEqual (0, connection.RecentDelayMicrosecondsForTests);
        }

        [Test]
        public async Task LedbatClampsDelaySampleToAckedPacketRtt ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 50_000;
            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber);
            ack.TimestampDiff = 1_000_000_000;
            connection.Receive (ack);
            await Task.Delay (50);

            clock.Microseconds = 60_000;
            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 110_000;
            ack = CreateStatePacket (123, sequenceNumber: 10, ackNumber: data.packet.SequenceNumber);
            ack.TimestampDiff = 1_000_220_000;
            connection.Receive (ack);
            await Task.Delay (50);

            Assert.AreEqual (50_000, connection.RecentDelayMicrosecondsForTests);
        }

        [Test]
        public async Task LedbatUsesCurrentNormalizedDelay ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 50_000;
            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await Task.Delay (50);

            clock.Microseconds = 60_000;
            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 110_000;
            ack = CreateStatePacket (123, sequenceNumber: 10, ackNumber: second.packet.SequenceNumber);
            ack.TimestampDiff = 30_000;
            connection.Receive (ack);
            await Task.Delay (50);

            Assert.AreEqual (20_000, connection.RecentDelayMicrosecondsForTests);
        }

        [Test]
        public async Task LedbatYieldsWhenDelayRisesAboveTarget ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var lowDelayAck = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            clock.Microseconds = 50_000;
            lowDelayAck.TimestampDiff = 10_000;
            connection.Receive (lowDelayAck);
            await Task.Delay (50);

            var afterLowDelay = connection.MaxWindowForTests;

            clock.Microseconds = 60_000;
            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var highDelayAck = CreateStatePacket (123, sequenceNumber: 10, ackNumber: second.packet.SequenceNumber);
            clock.Microseconds = 280_000;
            highDelayAck.TimestampDiff = 220_000;
            connection.Receive (highDelayAck);
            await Task.Delay (50);

            Assert.Less (connection.MaxWindowForTests, afterLowDelay);
        }

        [Test]
        public async Task LedbatUsesConfiguredTargetDelay ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (
                sendQueue.Writer,
                new IPEndPoint (IPAddress.Loopback, 12345),
                124,
                123,
                0,
                clock,
                transportSettings: new UtpTransportSettings { CongestionControlTarget = TimeSpan.FromMilliseconds (20) });

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var lowDelayAck = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            clock.Microseconds = 50_000;
            lowDelayAck.TimestampDiff = 10_000;
            connection.Receive (lowDelayAck);
            await Task.Delay (50);

            var afterLowDelay = connection.MaxWindowForTests;

            clock.Microseconds = 60_000;
            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var highDelayAck = CreateStatePacket (123, sequenceNumber: 10, ackNumber: second.packet.SequenceNumber);
            clock.Microseconds = 150_000;
            highDelayAck.TimestampDiff = 90_000;
            connection.Receive (highDelayAck);
            await Task.Delay (50);

            Assert.Less (connection.MaxWindowForTests, afterLowDelay);
        }

        [Test]
        public async Task LedbatBaseDelayExpiresAfterTwoMinutes ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var lowDelayAck = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            clock.Microseconds = 50_000;
            lowDelayAck.TimestampDiff = 10_000;
            connection.Receive (lowDelayAck);
            await Task.Delay (50);

            var afterLowDelay = connection.MaxWindowForTests;

            clock.Microseconds = 120_000_001;

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var highDelayAck = CreateStatePacket (123, sequenceNumber: 10, ackNumber: second.packet.SequenceNumber);
            clock.Microseconds = 120_220_001;
            highDelayAck.TimestampDiff = 220_000;
            connection.Receive (highDelayAck);
            await Task.Delay (50);

            Assert.AreEqual (afterLowDelay, connection.MaxWindowForTests);
            Assert.AreEqual (0, connection.RecentDelayMicrosecondsForTests);
        }

        [Test]
        public async Task LedbatHalvesWindowOnPacketLoss ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            var initialWindow = connection.MaxWindowForTests;

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 4 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 5 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fourth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fifth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber, third.packet.SequenceNumber));
            connection.Receive (CreateStatePacket (123, sequenceNumber: 10, ackNumber: first.packet.SequenceNumber, fourth.packet.SequenceNumber));
            connection.Receive (CreateStatePacket (123, sequenceNumber: 11, ackNumber: first.packet.SequenceNumber, fifth.packet.SequenceNumber));

            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.Less (connection.MaxWindowForTests, initialWindow);
            Assert.GreaterOrEqual (connection.MaxWindowForTests, initialWindow / 2);
        }

        [Test]
        public async Task LedbatHalvesWindowOnceForBurstLossInSameRecoveryWindow ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            var initialWindow = connection.MaxWindowForTests;

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 2 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 3 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 4 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 5 }).WithTimeout (10_000);
            await connection.SendAsync (new byte[] { 6 }).WithTimeout (10_000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var third = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fourth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var fifth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var sixth = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber,
                fourth.packet.SequenceNumber,
                fifth.packet.SequenceNumber,
                sixth.packet.SequenceNumber));

            var firstRetransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var secondRetransmit = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (second.packet.SequenceNumber, firstRetransmit.packet.SequenceNumber);
            Assert.AreEqual (third.packet.SequenceNumber, secondRetransmit.packet.SequenceNumber);
            Assert.Less (connection.MaxWindowForTests, initialWindow);
            Assert.GreaterOrEqual (connection.MaxWindowForTests, initialWindow / 2);
        }

        [Test]
        public async Task LedbatRecoversWindowAfterTimeoutMinimum ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_000_000;
            await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (UtpTransportSettings.DefaultInitialPacketSize, connection.MaxWindowForTests);

            var ack = CreateStatePacket (123, sequenceNumber: 9, ackNumber: first.packet.SequenceNumber);
            connection.Receive (ack);
            await Task.Delay (50);

            await connection.SendAsync (new byte[UtpTransportSettings.DefaultInitialPacketSize]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            clock.Microseconds = 1_050_000;
            ack = CreateStatePacket (123, sequenceNumber: 10, ackNumber: second.packet.SequenceNumber);
            ack.TimestampDiff = 10_000;
            connection.Receive (ack);
            await Task.Delay (50);

            Assert.Greater (connection.MaxWindowForTests, UtpTransportSettings.DefaultInitialPacketSize);
        }

        [Test]
        public async Task PeerWindowCapsOutboundDataSeparatelyFromCongestionWindow ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0, clock);

            var tinyWindow = CreateStatePacket (123, sequenceNumber: 9, ackNumber: 0);
            tinyWindow.WindowSize = 0;
            connection.Receive (tinyWindow);
            await Task.Delay (50);

            var sendTask = connection.SendAsync (new byte[] { 1 }).AsTask ();
            await AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (100));
            Assert.IsFalse (sendTask.IsCompleted);

            var openWindow = CreateStatePacket (123, sequenceNumber: 10, ackNumber: 0);
            openWindow.WindowSize = 1;
            connection.Receive (openWindow);

            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (1, await sendTask.WithTimeout (5000));
            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (1, data.packet.Payload.Length);
        }

        [Test]
        public async Task AckAndPeerWindowGrowthDoNotAccumulateStaleSendWindowSignals ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[1]).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: data.packet.SequenceNumber));
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            var largerWindow = CreateStatePacket (123, sequenceNumber: 10, ackNumber: data.packet.SequenceNumber);
            largerWindow.WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW * 2;
            connection.Receive (largerWindow);
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            Assert.AreEqual (0, connection.SendWindowSignalCountForTests);
        }

        [Test]
        public async Task FutureAckDoesNotReopenPeerWindow ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[1]).WithTimeout (10_000);
            var data = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var forged = CreateStatePacket (123, sequenceNumber: 9, ackNumber: unchecked((ushort) (data.packet.SequenceNumber + 1)));
            forged.WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW * 2;
            connection.Receive (forged);
            await Task.Delay (50);

            Assert.AreEqual (UtpPeerConnectionListener.INITIAL_WINDOW, connection.DiagnosticSnapshot.PeerWindowBytes);
            Assert.AreEqual (UtpPacket.HeaderSize + 1, connection.BytesInFlightForTests);
        }

        [Test]
        public async Task StaleAckDoesNotReplaceNewerPeerWindow ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[1]).WithTimeout (10_000);
            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            await connection.SendAsync (new byte[1]).WithTimeout (10_000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var current = CreateStatePacket (123, sequenceNumber: 9, ackNumber: second.packet.SequenceNumber);
            current.WindowSize = 4096;
            connection.Receive (current);
            await Task.Delay (50);

            var stale = CreateStatePacket (123, sequenceNumber: 10, ackNumber: first.packet.SequenceNumber);
            stale.WindowSize = 0;
            connection.Receive (stale);
            await Task.Delay (50);

            Assert.AreEqual (4096, connection.DiagnosticSnapshot.PeerWindowBytes);
        }

        [Test]
        public async Task UnrelatedAckWhileIdleDoesNotChangePeerWindow ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            var unrelated = CreateStatePacket (123, sequenceNumber: 9, ackNumber: 1234);
            unrelated.WindowSize = 0;
            connection.Receive (unrelated);
            await Task.Delay (50);

            Assert.AreEqual (UtpPeerConnectionListener.INITIAL_WINDOW, connection.DiagnosticSnapshot.PeerWindowBytes);
        }

        [Test]
        public async Task PureStatePacketDoesNotConsumeSequenceNumber ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
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
            var synSequence = syn.packet.SequenceNumber;

            connection.Receive (CreateStatePacket (connection.ConnectionIdReceive, sequenceNumber: 9, ackNumber: synSequence));
            Assert.IsTrue (await connectTask.WithTimeout (5000));

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var data = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Data, data.packet.Type);
            Assert.AreEqual (connection.ConnectionIdSend, data.packet.ConnectionId);
            Assert.AreEqual (unchecked((ushort) (synSequence + 1)), data.packet.SequenceNumber);
            Assert.AreEqual (8, data.packet.AckNumber);
        }

        [Test]
        public async Task OutgoingConnectionAcceptsDataPacketAcknowledgingSyn ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            var connectTask = connection.ConnectAsync ().AsTask ();
            var syn = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            var data = CreateDataPacket (
                sequenceNumber: 9,
                payload: "a",
                connectionId: connection.ConnectionIdReceive,
                ackNumber: syn.packet.SequenceNumber);
            connection.Receive (data);

            Assert.IsTrue (await connectTask.WithTimeout (5000));

            var buffer = new byte[1];
            Assert.AreEqual (1, await connection.ReceiveAsync (buffer).WithTimeout (5000));
            Assert.AreEqual ((byte) 'a', buffer[0]);
        }

        [Test]
        public async Task IncomingSynAckUsesLibutpConnectionIdsAndSequenceNumber ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
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
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
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
        public async Task FinAckClosesCleanly ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendFinAsync ();
            var fin = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateStatePacket (123, sequenceNumber: 9, ackNumber: fin.packet.SequenceNumber));

            Assert.AreEqual (0, await connection.ReceiveAsync (new byte[1]).WithTimeout (5000));
            Assert.IsTrue (connection.IsClosedOrReset);
        }

        [Test]
        public async Task ResetDuringFinSentUnregistersConnection ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));

            await connection.SendFinAsync ();
            var fin = await listener.SendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateResetPacket (connection.ConnectionIdReceive, fin.packet.SequenceNumber));
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            Assert.IsFalse (listener.IsRegistered (connection));
            Assert.IsTrue (connection.IsClosedOrReset);
        }

        [Test]
        public async Task FutureAckResetIsIgnored ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 0);

            await connection.SendAsync (new byte[] { 1 }).WithTimeout (10_000);
            var sent = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            connection.Receive (CreateResetPacket (123, unchecked((ushort) (sent.packet.SequenceNumber + 1))));
            await Task.Delay (50);

            Assert.IsFalse (connection.IsClosedOrReset);

            connection.Receive (CreateResetPacket (123, sent.packet.SequenceNumber));
            await Task.Delay (50);

            Assert.IsTrue (connection.IsClosedOrReset);
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
        public async Task IncomingSynCapacityDropsNewConnectionsButAllowsDuplicateSyn ()
        {
            using var harness = new InMemoryUtpHarness (transportSettings: new UtpTransportSettings { MaxIncomingSynConnections = 1 });

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            var first = await harness.ReadOutbound ().WithTimeout (5000);
            Assert.AreEqual (PacketType.State, first.packet.Type);
            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);

            harness.Deliver (CreateSynPacket (connectionId: 124, sequenceNumber: 8));
            await AssertNoOutboundPacket (harness.Listener.SendQueue, TimeSpan.FromMilliseconds (100));
            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            var duplicate = await harness.ReadOutbound ().WithTimeout (5000);
            Assert.AreEqual (PacketType.State, duplicate.packet.Type);
            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);
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
        public async Task IncomingSynInitializesPeerWindowAndTimestamp ()
        {
            var clock = new ManualClock { Microseconds = 100_000 };
            using var harness = new InMemoryUtpHarness (clock);
            UtpPeerConnection? accepted = null;
            harness.Listener.ConnectionReceived += (o, e) => accepted = (UtpPeerConnection) e.Connection;
            var syn = CreateSynPacket (connectionId: 123, sequenceNumber: 7);
            syn.WindowSize = 777_777;
            syn.SetTimestamp (40_000);

            harness.Deliver (syn);
            await harness.ReadOutbound ().WithTimeout (5000);

            Assert.IsNotNull (accepted);
            Assert.AreEqual (777_777, accepted!.DiagnosticSnapshot.PeerWindowBytes);
            Assert.AreEqual (60_000, accepted.LastReceivedDelayMicrosecondsForTests);
        }

        [Test]
        public async Task SynWithSameEndpointAndConnectionIdButDifferentSequenceIsIgnored ()
        {
            using var harness = new InMemoryUtpHarness ();
            int received = 0;
            harness.Listener.ConnectionReceived += (o, e) => received++;

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            await harness.ReadOutbound ().WithTimeout (5000);

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 8));
            await AssertNoOutboundPacket (harness.Listener.SendQueue, TimeSpan.FromMilliseconds (100));

            Assert.AreEqual (1, received);
            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);
        }

        [Test]
        public async Task DuplicateSynAfterCloseCreatesFreshConnectionOnSameKey ()
        {
            using var harness = new InMemoryUtpHarness ();
            UtpPeerConnection? accepted = null;
            int received = 0;
            harness.Listener.ConnectionReceived += (o, e) => {
                accepted = (UtpPeerConnection) e.Connection;
                received++;
            };

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            var first = await harness.ReadOutbound ().WithTimeout (5000);
            accepted!.Dispose ();

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            var second = await harness.ReadOutbound ().WithTimeout (5000);

            Assert.AreEqual (2, received);
            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);
            Assert.AreEqual (PacketType.State, first.packet.Type);
            Assert.AreEqual (PacketType.State, second.packet.Type);
            Assert.AreEqual (7, second.packet.AckNumber);
        }

        [Test]
        public async Task StaleDuplicateDataAfterRemoteFinDoesNotSendReset ()
        {
            using var harness = new InMemoryUtpHarness ();
            UtpPeerConnection? accepted = null;
            harness.Listener.ConnectionReceived += (o, e) => accepted = (UtpPeerConnection) e.Connection;

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 1));
            await harness.ReadOutbound ().WithTimeout (5000);

            harness.Deliver (CreateDataPacket (2, "a", connectionId: 124));
            await harness.ReadOutbound ().WithTimeout (5000);
            harness.Deliver (CreateFinPacket (3, connectionId: 124));
            await harness.ReadOutbound ().WithTimeout (5000);

            var buffer = new byte[1];
            Assert.AreEqual (1, await accepted!.ReceiveAsync (buffer).WithTimeout (5000));
            Assert.AreEqual (0, await accepted.ReceiveAsync (buffer).WithTimeout (5000));

            harness.Deliver (CreateDataPacket (2, "a", connectionId: 124));
            await AssertNoOutboundPacket (harness.Listener.SendQueue, TimeSpan.FromMilliseconds (100));
            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);
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
        public async Task RepeatedUnknownDataPacketSendsOneReset ()
        {
            using var harness = new InMemoryUtpHarness ();
            var packet = CreateDataPacket (2, "x");

            harness.Deliver (packet);
            harness.Deliver (packet);

            var reset = await harness.ReadOutbound ().WithTimeout (5000);
            Assert.AreEqual (PacketType.Reset, reset.packet.Type);
            Assert.IsNull (reset.connection);
            await AssertNoOutboundPacket (harness.Listener.SendQueue, TimeSpan.FromMilliseconds (100));
        }

        [Test]
        public async Task UnknownDataResetThrottleExpires ()
        {
            var clock = new ManualClock ();
            using var harness = new InMemoryUtpHarness (clock);
            var packet = CreateDataPacket (2, "x");

            harness.Deliver (packet);
            var reset = await harness.ReadOutbound ().WithTimeout (5000);
            Assert.AreEqual (PacketType.Reset, reset.packet.Type);

            harness.Deliver (packet);
            await AssertNoOutboundPacket (harness.Listener.SendQueue, TimeSpan.FromMilliseconds (100));

            clock.Microseconds = 10_000_000;
            harness.Deliver (packet);
            reset = await harness.ReadOutbound ().WithTimeout (5000);
            Assert.AreEqual (PacketType.Reset, reset.packet.Type);
        }

        [Test]
        public async Task UnknownResetPacketDoesNotSendReset ()
        {
            using var harness = new InMemoryUtpHarness ();

            harness.Deliver (CreateResetPacket (123));

            await AssertNoOutboundPacket (harness.Listener.SendQueue, TimeSpan.FromMilliseconds (100));
        }

        [Test]
        public async Task UnknownPacketResetThrottleSuppressesWhenFull ()
        {
            using var harness = new InMemoryUtpHarness ();

            for (int i = 0; i < UtpPeerConnectionListener.RecentResetCapacityForTests + 1; i++)
                harness.Deliver (CreateDataPacket ((ushort) (2 + i), "x"));

            for (int i = 0; i < UtpPeerConnectionListener.RecentResetCapacityForTests; i++) {
                var reset = await harness.ReadOutbound ().WithTimeout (5000);
                Assert.AreEqual (PacketType.Reset, reset.packet.Type);
            }

            await AssertNoOutboundPacket (harness.Listener.SendQueue, TimeSpan.FromMilliseconds (100));
        }

        [Test]
        public async Task SynCollidingWithOutgoingConnectionIsIgnored ()
        {
            using var harness = new InMemoryUtpHarness ();
            using var outgoing = new UtpPeerConnection (harness.Listener, harness.Remote, 123);

            Assert.IsTrue (harness.Listener.TryRegisterOutgoing (outgoing));

            harness.Deliver (CreateSynPacket (connectionId: 122, sequenceNumber: 7));

            await AssertNoOutboundPacket (harness.Listener.SendQueue, TimeSpan.FromMilliseconds (100));
            Assert.IsTrue (harness.Listener.IsRegistered (outgoing));
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
        public async Task ProcessingDatagramDoesNotPruneStaleConnections ()
        {
            var clock = new ManualClock ();
            using var harness = new InMemoryUtpHarness (clock);

            harness.Deliver (CreateSynPacket (connectionId: 123, sequenceNumber: 7));
            await harness.ReadOutbound ().WithTimeout (5000);
            clock.Microseconds = 120_000_000;

            harness.Deliver (CreateDataPacket (2, "x", connectionId: 999));
            await harness.ReadOutbound ().WithTimeout (5000);

            Assert.AreEqual (1, harness.Listener.RegisteredConnectionCount);
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

            Assert.AreEqual (PacketType.State, ack1.packet.Type);
            Assert.AreEqual (3, ack1.packet.AckNumber);
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
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);
            var receiveTask = connection.ReceiveAsync (new byte[1]).AsTask ();

            connection.Dispose ();

            Assert.ThrowsAsync<OperationCanceledException> (async () => await receiveTask.WithTimeout (5000));
        }

        [Test]
        public void DisposeCancelsPendingSend ()
        {
            var sendQueue = Channel.CreateBounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> (1);
            Assert.IsTrue (sendQueue.Writer.TryWrite ((new UtpPacket (new byte[UtpPacket.HeaderSize]), null, new IPEndPoint (IPAddress.Loopback, 12345), null)));

            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);
            var sendTask = connection.SendAsync (new byte[] { 1 }).AsTask ();

            connection.Dispose ();

            Assert.ThrowsAsync<OperationCanceledException> (async () => await sendTask.WithTimeout (5000));
        }

        [Test]
        public void SendCompletesSynchronouslyWhenQueueHasCapacity ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            var send = connection.SendAsync (new byte[] { 1 });

            Assert.IsTrue (send.IsCompleted);
            Assert.AreEqual (1, send.GetAwaiter ().GetResult ());
            Assert.IsTrue (sendQueue.Reader.TryRead (out var queued));
            Assert.AreEqual (PacketType.Data, queued.packet.Type);
        }

        [Test]
        public async Task DisposeStopsReceiveProcessor ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ();
            var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);
            var processor = connection.ReceiveProcessorForTests;

            Assert.IsFalse (processor.IsCompleted);
            connection.Dispose ();

            await processor.WaitAsync (TimeSpan.FromSeconds (5));
            Assert.IsTrue (processor.IsCompletedSuccessfully);
        }

        [Test]
        public async Task ReceivedResetUnregistersConnection ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));

            connection.Receive (CreateResetPacket (connection.ConnectionIdReceive));
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            Assert.IsFalse (listener.IsRegistered (connection));
        }

        [Test]
        public async Task ReceivedResetWithSendConnectionIdUnregistersConnection ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));

            listener.ProcessDatagram ((IPEndPoint) connection.EndPoint, CreateResetPacket (connection.ConnectionIdSend).AsMemory ().ToArray ());
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            Assert.IsFalse (listener.IsRegistered (connection));
        }

        [Test]
        public async Task RoutedResetWithReceiveConnectionIdUnregistersConnection ()
        {
            var listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            using var connection = new UtpPeerConnection (listener, new IPEndPoint (IPAddress.Loopback, 12345), 123);

            Assert.IsTrue (listener.TryRegisterOutgoing (connection));

            listener.ProcessDatagram ((IPEndPoint) connection.EndPoint, CreateResetPacket (connection.ConnectionIdReceive).AsMemory ().ToArray ());
            await connection.FlushReceiveQueueForTests ().WaitAsync (TimeSpan.FromSeconds (5));

            Assert.IsFalse (listener.IsRegistered (connection));
        }

        static UtpPacket CreateDataPacket (ushort sequenceNumber, string payload, ushort connectionId = 123, ushort ackNumber = 1)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes (payload);
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize + bytes.Length]) {
                Type = PacketType.Data,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = connectionId,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = ackNumber
            };
            bytes.CopyTo (packet.Payload);
            packet.SetTimestamp ();
            return packet;
        }

        static UtpPacket CreateDataPacketWithExtensions (ushort sequenceNumber, string payload, params ExtensionBlock[] extensions)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes (payload);
            var extensionLength = extensions.Sum (t => 2 + t.Payload.Length);
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize + extensionLength + bytes.Length]) {
                Type = PacketType.Data,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                Extension = extensions.Length == 0 ? (byte) 0 : extensions[0].Type,
                ConnectionId = 123,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = 1
            };

            var span = packet.AsMemory ().Span;
            int offset = UtpPacket.HeaderSize;
            for (int i = 0; i < extensions.Length; i++) {
                span[offset] = i + 1 == extensions.Length ? (byte) 0 : extensions[i + 1].Type;
                span[offset + 1] = (byte) extensions[i].Payload.Length;
                extensions[i].Payload.CopyTo (span.Slice (offset + 2));
                offset += 2 + extensions[i].Payload.Length;
            }
            bytes.CopyTo (span.Slice (offset));
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

        static void AssertInvalidSetting (UtpTransportSettings settings)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException> (() => UtpTransportSettings.Create (settings));
            Assert.AreEqual ("settings", ex!.ParamName);
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

        static Task AssertNoOutboundPacket (Channel<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> sendQueue)
            => AssertNoOutboundPacket (sendQueue, TimeSpan.FromMilliseconds (100));

        static async Task AssertNoOutboundPacket (Channel<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> sendQueue, TimeSpan delay)
        {
            await Task.Delay (delay);
            Assert.IsFalse (sendQueue.Reader.TryRead (out _));
        }

        static byte SelectiveAckByte (UtpPacket packet)
            => packet.AsMemory ().Span[UtpPacket.HeaderSize + 2];

        readonly struct ExtensionBlock
        {
            public ExtensionBlock (byte type, byte[] payload)
            {
                Type = type;
                Payload = payload;
            }

            public byte Type { get; }
            public byte[] Payload { get; }
        }

        static UtpPacket CreateStatePacketWithExtensions (ushort connectionId, ushort sequenceNumber, ushort ackNumber, params ExtensionBlock[] extensions)
        {
            var extensionLength = extensions.Sum (t => 2 + t.Payload.Length);
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize + extensionLength]) {
                Type = PacketType.State,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                Extension = extensions.Length == 0 ? (byte) 0 : extensions[0].Type,
                ConnectionId = connectionId,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = ackNumber
            };

            var span = packet.AsMemory ().Span;
            int offset = UtpPacket.HeaderSize;
            for (int i = 0; i < extensions.Length; i++) {
                span[offset] = i + 1 == extensions.Length ? (byte) 0 : extensions[i + 1].Type;
                span[offset + 1] = (byte) extensions[i].Payload.Length;
                extensions[i].Payload.CopyTo (span.Slice (offset + 2));
                offset += 2 + extensions[i].Payload.Length;
            }
            packet.SetTimestamp ();
            return packet;
        }

        static UtpPacket CreateMalformedExtensionPacket (ushort connectionId, ushort sequenceNumber, ushort ackNumber)
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize + 3]) {
                Type = PacketType.State,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                Extension = 99,
                ConnectionId = connectionId,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = ackNumber
            };
            var span = packet.AsMemory ().Span;
            span[UtpPacket.HeaderSize] = 0;
            span[UtpPacket.HeaderSize + 1] = 8;
            span[UtpPacket.HeaderSize + 2] = 1;
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

        static UtpPacket CreateResetPacket (ushort connectionId, ushort ackNumber = 0)
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize]) {
                Type = PacketType.Reset,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = connectionId,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = 2,
                AckNumber = ackNumber
            };
            packet.SetTimestamp ();
            return packet;
        }

        static UtpPacket CreateFinPacket (ushort sequenceNumber, ushort connectionId = 123)
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize]) {
                Type = PacketType.Fin,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = connectionId,
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
                : this (new ManualClock (), null)
            {
            }

            public InMemoryUtpHarness (ManualClock? clock = null, UtpTransportSettings? transportSettings = null)
            {
                Clock = clock ?? new ManualClock ();
                Listener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0), Clock, transportSettings);
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

            public Task<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint, Action? sendCompleted)> ReadOutbound ()
                => Listener.SendQueue.Reader.ReadAsync ().AsTask ();

            public void Dispose ()
                => Listener.Stop ();
        }
    }
}
