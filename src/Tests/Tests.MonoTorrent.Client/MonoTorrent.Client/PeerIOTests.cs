//
// PeerIOTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
using System.Threading.Tasks;

using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PeerIOTests
    {
        ConnectionPair pair;

        [SetUp]
        public void Setup ()
        {
            pair = new ConnectionPair ().DisposeAfterTimeout ();
        }

        [TearDown]
        public void Teardown ()
        {
            pair.Dispose ();
        }

        [Test]
        public async Task ValidVeryLargeMessageBodyLength ()
        {
            var torrentData = TestTorrentManagerInfo.Create (
                pieceLength: Constants.BlockSize, // 16kB pieces.
                size: 1024L * 1024 * 1024 * 100 // 100GB
            );

            var bf = new BitField (torrentData.PieceCount).Set (1, true);
            var message = new BitfieldMessage (bf);

            using var releaser = MemoryPool.Default.Rent (message.ByteLength, out Memory<byte> buffer);
            message.Encode (buffer.Span);

            await NetworkIO.SendAsync (pair.Outgoing, buffer);
            var receivedMessage = (BitfieldMessage) await PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance, null, null, null, torrentData);
            Assert.IsTrue (message.BitField.SequenceEqual(receivedMessage.BitField));
        }

        [Test]
        public async Task InvalidLargeMessageBodyLength ()
        {
            using var releaser = MemoryPool.Default.Rent (4, out Memory<byte> buffer);
            Message.Write (buffer.Span, int.MaxValue);

            await NetworkIO.SendAsync (pair.Outgoing, buffer);
            var receiveTask = PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance);

            Assert.ThrowsAsync<ProtocolException> (async () => await receiveTask, "#1");
        }

        [Test]
        public async Task NegativeMessageBodyLength ()
        {
            using var releaser = MemoryPool.Default.Rent (20, out Memory<byte> buffer);
            Message.Write (buffer.Span, -6);

            await NetworkIO.SendAsync (pair.Outgoing, buffer);
            var receiveTask = PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance);

            Assert.ThrowsAsync<ProtocolException> (async () => await receiveTask, "#1");
            Assert.ThrowsAsync<ConnectionClosedException> (async () => await PeerIO.ReceiveMessageAsync (pair.Outgoing, PlainTextEncryption.Instance), "#2");
        }

        [Test]
        public async Task UnknownMessage ()
        {
            using var releaser = MemoryPool.Default.Rent (20, out Memory<byte> data);
            Message.Write (data.Span, 16);
            for (int i = 4; i < 16; i++)
                data.Span [i] = byte.MaxValue;

            var task = PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance);
            await NetworkIO.SendAsync (pair.Outgoing, data);

            Assert.ThrowsAsync<MessageException> (async () => await task, "#1");
        }

        [Test]
        public async Task ZeroMessageBodyIsKeepAlive ()
        {
            using var releaser = MemoryPool.Default.Rent (4, out Memory<byte> buffer);

            Message.Write (buffer.Span, 0);
            await NetworkIO.SendAsync (pair.Outgoing, buffer);
            Assert.IsInstanceOf<KeepAliveMessage> (await PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance));

            new KeepAliveMessage ().Encode (buffer.Span);
            await NetworkIO.SendAsync (pair.Outgoing, buffer);
            Assert.IsInstanceOf<KeepAliveMessage> (await PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance));
        }

        [Test]
        public void IgnoreNullMonitors ()
        {
            var blockSize = Constants.BlockSize - 1234;
            var msg = new PieceMessage (0, 0, blockSize);
            var releaser = new MemoryPool ().Rent (blockSize, out Memory<byte> buffer);
            msg.SetData ((releaser, buffer));

            Assert.DoesNotThrowAsync (() => {
                return Task.WhenAll (
                    PeerIO.SendMessageAsync (pair.Incoming, PlainTextEncryption.Instance, msg).AsTask (),
                    PeerIO.ReceiveMessageAsync (pair.Outgoing, PlainTextEncryption.Instance).AsTask ()
                );
            });
        }

        [Test]
        public async Task CountPieceMessageBlockLengthAsData ()
        {
            var blockSize = Constants.BlockSize - 1234;
            var msg = new PieceMessage (0, 0, blockSize);
            var releaser = new MemoryPool ().Rent (blockSize, out Memory<byte> buffer);
            msg.SetData ((releaser, buffer));

            var protocolSize = msg.ByteLength - blockSize;
            await Task.WhenAll (
                PeerIO.SendMessageAsync (pair.Incoming, PlainTextEncryption.Instance, msg, null, pair.Incoming.Monitor, pair.Incoming.ManagerMonitor).AsTask (),
                PeerIO.ReceiveMessageAsync (pair.Outgoing, PlainTextEncryption.Instance, null, pair.Outgoing.Monitor, pair.Outgoing.ManagerMonitor, null).AsTask ()
            );

            // incoming connection sends 1 message so should receive nothing.
            Assert.AreEqual (0, pair.Incoming.Monitor.DataBytesReceived);
            Assert.AreEqual (0, pair.Incoming.ManagerMonitor.DataBytesReceived);
            Assert.AreEqual (0, pair.Incoming.Monitor.ProtocolBytesReceived);
            Assert.AreEqual (0, pair.Incoming.ManagerMonitor.ProtocolBytesReceived);

            Assert.AreEqual (blockSize, pair.Incoming.Monitor.DataBytesSent);
            Assert.AreEqual (blockSize, pair.Incoming.ManagerMonitor.DataBytesSent);
            Assert.AreEqual (protocolSize, pair.Incoming.Monitor.ProtocolBytesSent);
            Assert.AreEqual (protocolSize, pair.Incoming.ManagerMonitor.ProtocolBytesSent);

            // outgoing connection receives 1 message, so should send nothing.
            Assert.AreEqual (0, pair.Outgoing.Monitor.DataBytesSent);
            Assert.AreEqual (0, pair.Outgoing.ManagerMonitor.DataBytesSent);
            Assert.AreEqual (0, pair.Outgoing.Monitor.ProtocolBytesSent);
            Assert.AreEqual (0, pair.Outgoing.ManagerMonitor.ProtocolBytesSent);

            Assert.AreEqual (blockSize, pair.Outgoing.Monitor.DataBytesReceived);
            Assert.AreEqual (blockSize, pair.Outgoing.ManagerMonitor.DataBytesReceived);
            Assert.AreEqual (protocolSize, pair.Outgoing.Monitor.ProtocolBytesReceived);
            Assert.AreEqual (protocolSize, pair.Outgoing.ManagerMonitor.ProtocolBytesReceived);
        }
    }
}

