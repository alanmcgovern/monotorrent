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
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection connection, IPEndPoint remoteEndPoint)> ();
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
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            connection.Receive (CreateDataPacket (4, "d"));

            var ack = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            Assert.AreEqual (PacketType.State, ack.packet.Type);
            Assert.AreEqual (1, ack.packet.AckNumber);
            Assert.AreEqual (1, ack.packet.Extension);
            Assert.AreEqual (0b_0000_0010, ack.packet.AsMemory ().Span[UtpPacket.HeaderSize + 2]);
        }

        [Test]
        public async Task UnackedDataPacketIsRetransmitted ()
        {
            var sendQueue = Channel.CreateUnbounded<(UtpPacket packet, UtpPeerConnection connection, IPEndPoint remoteEndPoint)> ();
            using var connection = new UtpPeerConnection (sendQueue.Writer, new IPEndPoint (IPAddress.Loopback, 12345), 124, 123, 1);

            await connection.SendAsync (new byte[] { 1, 2, 3 }).WithTimeout (5000);

            var first = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);
            var second = await sendQueue.Reader.ReadAsync ().AsTask ().WithTimeout (5000);

            Assert.AreEqual (PacketType.Data, first.packet.Type);
            Assert.AreEqual (first.packet.SequenceNumber, second.packet.SequenceNumber);
            Assert.IsTrue (first.packet.Payload.SequenceEqual (second.packet.Payload));
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
    }
}
