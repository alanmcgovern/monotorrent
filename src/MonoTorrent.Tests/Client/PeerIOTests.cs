﻿//
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


using System.Threading.Tasks;

using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;

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
            pair = new ConnectionPair ().WithTimeout ();
        }

        [TearDown]
        public void Teardown ()
        {
            pair.Dispose ();
        }

        [Test]
        public async Task LargeMessageBodyLength ()
        {
            var buffer = new ByteBuffer (4);
            Message.Write (buffer.Data, 0, int.MaxValue);

            await NetworkIO.SendAsync (pair.Outgoing, buffer, 0, buffer.Data.Length);
            var receiveTask = PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance);

            Assert.ThrowsAsync<ProtocolException> (async () => await receiveTask, "#1");
            Assert.ThrowsAsync<ConnectionClosedException> (async () => await PeerIO.ReceiveMessageAsync (pair.Outgoing, PlainTextEncryption.Instance), "#2");
        }

        [Test]
        public async Task NegativeMessageBodyLength ()
        {
            var buffer = new ByteBuffer (4);
            Message.Write (buffer.Data, 0, -6);

            await NetworkIO.SendAsync (pair.Outgoing, buffer, 0, buffer.Data.Length);
            var receiveTask = PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance);

            Assert.ThrowsAsync<ProtocolException> (async () => await receiveTask, "#1");
            Assert.ThrowsAsync<ConnectionClosedException> (async () => await PeerIO.ReceiveMessageAsync (pair.Outgoing, PlainTextEncryption.Instance), "#2");
        }

        [Test]
        public async Task UnknownMessage ()
        {
            var data = new ByteBuffer (20);
            Message.Write (data.Data, 0, 16);
            for (int i = 4; i < 16; i++)
                data.Data[i] = byte.MaxValue;

            var task = PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance);
            await NetworkIO.SendAsync (pair.Outgoing, data, 0, 20);

            Assert.ThrowsAsync<ProtocolException> (async () => await task, "#1");
        }

        [Test]
        public async Task ZeroMessageBodyIsKeepAlive ()
        {
            var buffer = new ByteBuffer (4);

            Message.Write (buffer.Data, 0, 0);
            await NetworkIO.SendAsync (pair.Outgoing, buffer, 0, buffer.Data.Length);
            Assert.IsInstanceOf<KeepAliveMessage> (await PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance));

            new KeepAliveMessage ().Encode (buffer.Data, 0);
            await NetworkIO.SendAsync (pair.Outgoing, buffer, 0, buffer.Data.Length);
            Assert.IsInstanceOf<KeepAliveMessage> (await PeerIO.ReceiveMessageAsync (pair.Incoming, PlainTextEncryption.Instance));
        }

        [Test]
        public void IgnoreNullMonitors ()
        {
            var blockSize = Piece.BlockSize - 1234;
            var msg = new PieceMessage (0, 0, blockSize) {
                DataReleaser = new ByteBufferPool.Releaser (null, new ByteBuffer(blockSize))
            };

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
            var blockSize = Piece.BlockSize - 1234;
            var msg = new PieceMessage (0, 0, blockSize) {
                DataReleaser = new ByteBufferPool.Releaser (null, new ByteBuffer (blockSize))
            };

            var protocolSize = msg.ByteLength - blockSize;
            await Task.WhenAll (
                PeerIO.SendMessageAsync (pair.Incoming, PlainTextEncryption.Instance, msg, null, pair.Incoming.Monitor, pair.Incoming.ManagerMonitor).AsTask (),
                PeerIO.ReceiveMessageAsync (pair.Outgoing, PlainTextEncryption.Instance, null, pair.Outgoing.Monitor, pair.Outgoing.ManagerMonitor, null).AsTask ()
            );

            // incoming connection sends 1 message so should receive nothing.
            Assert.AreEqual (0, pair.Incoming.Monitor.DataBytesDownloaded);
            Assert.AreEqual (0, pair.Incoming.ManagerMonitor.DataBytesDownloaded);
            Assert.AreEqual (0, pair.Incoming.Monitor.ProtocolBytesDownloaded);
            Assert.AreEqual (0, pair.Incoming.ManagerMonitor.ProtocolBytesDownloaded);

            Assert.AreEqual (blockSize, pair.Incoming.Monitor.DataBytesUploaded);
            Assert.AreEqual (blockSize, pair.Incoming.ManagerMonitor.DataBytesUploaded);
            Assert.AreEqual (protocolSize, pair.Incoming.Monitor.ProtocolBytesUploaded);
            Assert.AreEqual (protocolSize, pair.Incoming.ManagerMonitor.ProtocolBytesUploaded);

            // outgoing connection receives 1 message, so should send nothing.
            Assert.AreEqual (0, pair.Outgoing.Monitor.DataBytesUploaded);
            Assert.AreEqual (0, pair.Outgoing.ManagerMonitor.DataBytesUploaded);
            Assert.AreEqual (0, pair.Outgoing.Monitor.ProtocolBytesUploaded);
            Assert.AreEqual (0, pair.Outgoing.ManagerMonitor.ProtocolBytesUploaded);

            Assert.AreEqual (blockSize, pair.Outgoing.Monitor.DataBytesDownloaded);
            Assert.AreEqual (blockSize, pair.Outgoing.ManagerMonitor.DataBytesDownloaded);
            Assert.AreEqual (protocolSize, pair.Outgoing.Monitor.ProtocolBytesDownloaded);
            Assert.AreEqual (protocolSize, pair.Outgoing.ManagerMonitor.ProtocolBytesDownloaded);
        }
    }
}

