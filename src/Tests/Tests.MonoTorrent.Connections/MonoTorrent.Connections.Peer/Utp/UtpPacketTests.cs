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
    public class UtpPacketTests
    {
        [Test]
        public void HeaderEncoding ()
        {
            var buffer = new byte[UtpPacket.HeaderSize + 3];
            var packet = new UtpPacket (buffer) {
                Type = PacketType.State,
                Version = 1,
                Extension = 2,
                ConnectionId = 0x1234,
                TimestampDiff = 0x55667788,
                WindowSize = 0x99AABBCC,
                SequenceNumber = 0xDDEE,
                AckNumber = 0xFF00
            };
            packet.SetTimestamp (0x11223344);

            CollectionAssert.AreEqual (new byte[] {
                0x21, 0x02,
                0x12, 0x34,
                0x11, 0x22, 0x33, 0x44,
                0x55, 0x66, 0x77, 0x88,
                0x99, 0xAA, 0xBB, 0xCC,
                0xDD, 0xEE,
                0xFF, 0x00
            }, buffer.AsSpan (0, UtpPacket.HeaderSize).ToArray ());
        }

        [Test]
        public void TypeAndVersionShareFirstByteNibbles ()
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize]);

            packet.Type = PacketType.Fin;
            packet.Version = 1;

            Assert.AreEqual (0x11, packet.AsMemory ().Span[0]);

            packet.Version = 0x0F;
            Assert.AreEqual (0x1F, packet.AsMemory ().Span[0]);

            packet.Type = PacketType.Reset;
            Assert.AreEqual (0x3F, packet.AsMemory ().Span[0]);
        }

        [Test]
        public void PayloadSlicesAfterHeader ()
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize + 4]);

            new byte[] { 1, 2, 3, 4 }.CopyTo (packet.Payload);

            CollectionAssert.AreEqual (new byte[] { 1, 2, 3, 4 }, packet.AsMemory ().Slice (UtpPacket.HeaderSize).ToArray ());
        }

        [TestCase ((ushort) 2, (ushort) 1, true)]
        [TestCase ((ushort) 0, ushort.MaxValue, true)]
        [TestCase (ushort.MaxValue, (ushort) 0, false)]
        [TestCase ((ushort) 1, (ushort) 1, false)]
        public void SequenceGreaterThanHandlesWraparound (ushort left, ushort right, bool expected)
        {
            Assert.AreEqual (expected, UtpPeerConnection.SequenceGreaterThan (left, right));
        }

        [TestCase ((ushort) 1, (ushort) 2, true)]
        [TestCase (ushort.MaxValue, (ushort) 0, true)]
        [TestCase ((ushort) 0, ushort.MaxValue, false)]
        [TestCase ((ushort) 1, (ushort) 1, true)]
        public void SequenceLessThanOrEqualHandlesWraparound (ushort left, ushort right, bool expected)
        {
            Assert.AreEqual (expected, UtpPeerConnection.SequenceLessThanOrEqual (left, right));
        }

        [Test]
        public async Task OutgoingPacketsUseLatestReceivedDelaySample ()
        {
            var clock = new ManualClock ();
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection? connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1, clock);

            clock.Microseconds = 1_000;
            connection.Receive (CreatePacket (sequenceNumber: 2, timestamp: 900));
            await sendQueue.Reader.ReadAsync ();

            await connection.SendAsync (new byte[] { 1 });
            var outgoing = await sendQueue.Reader.ReadAsync ();

            clock.Microseconds = 2_000;
            connection.Receive (CreatePacket (sequenceNumber: 3, timestamp: 1_750));
            await sendQueue.Reader.ReadAsync ();

            outgoing.connection!.PrepareForSend (ref outgoing.packet);

            Assert.AreEqual (2_000, outgoing.packet.Timestamp);
            Assert.AreEqual (250, outgoing.packet.TimestampDiff);
        }

        static UtpPacket CreatePacket (ushort sequenceNumber, uint timestamp)
        {
            var packet = new UtpPacket (new byte[UtpPacket.HeaderSize]) {
                Type = PacketType.Data,
                Version = UtpPeerConnectionListener.UTP_VERSION,
                ConnectionId = 124,
                WindowSize = UtpPeerConnectionListener.INITIAL_WINDOW,
                SequenceNumber = sequenceNumber,
                AckNumber = 1
            };
            packet.SetTimestamp (timestamp);
            return packet;
        }

        sealed class ManualClock : IUtpClock
        {
            public uint Microseconds { get; set; }
        }
    }
}
