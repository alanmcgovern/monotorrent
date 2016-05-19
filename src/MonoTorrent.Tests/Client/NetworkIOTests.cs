using System;
using System.Net;
using System.Threading;
using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Tests.Client
{
    public class NetworkIOTests : IDisposable
    {
        public NetworkIOTests()
        {
            if (data == null)
            {
                data = new byte[32768];
                new Random().NextBytes(data);
            }
            pair = new ConnectionPair(34567);
        }

        public void Dispose()
        {
            pair.Dispose();
        }

        private byte[] buffer;
        private readonly byte[] data;
        private readonly ConnectionPair pair;

        private CustomConnection Incoming
        {
            get { return pair.Incoming; }
        }

        private CustomConnection Outgoing
        {
            get { return pair.Outgoing; }
        }

        private void DoReceive(bool slowOutgoing, bool slowIncoming)
        {
            Incoming.SlowConnection = slowIncoming;
            Outgoing.SlowConnection = slowOutgoing;

            var sent = 0;
            buffer = new byte[data.Length];

            var handle = new ManualResetEvent(false);
            NetworkIO.EnqueueReceive(Outgoing, buffer, 0, buffer.Length, null, null, null, (s, t, o) =>
            {
                Assert.True(s, "#Receive successful");
                Assert.Equal(buffer.Length, t);
                handle.Set();
            }, null);

            while (sent != buffer.Length)
            {
                var r = Incoming.Send(data, sent, data.Length - sent);
                Assert.NotEqual(0, r);
                sent += r;
            }

            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(4)), "Data should be all received");
            for (var i = 0; i < buffer.Length; i++)
            {
                if (data[i] != buffer[i])
                    Assert.True(false, "Buffers differ at position " + i);
            }
        }

        public void DoSend(bool slowOutgoing, bool slowIncoming)
        {
            Incoming.SlowConnection = slowIncoming;
            Outgoing.SlowConnection = slowOutgoing;

            var handle = new ManualResetEvent(false);
            NetworkIO.EnqueueSend(Outgoing, data, 0, data.Length, null, null, null, delegate { handle.Set(); }, null);

            var received = 0;
            var buffer = new byte[data.Length];
            while (received != buffer.Length)
            {
                var r = Incoming.Receive(buffer, received, buffer.Length - received);
                Assert.NotEqual(0, r);
                received += r;
            }
            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(1)), "Data should be all sent");
            Assert.True(Toolbox.ByteMatch(buffer, data), "Data matches");
        }

        [Fact]
        public void InvalidMessage()
        {
            var success = true;
            var handle = new ManualResetEvent(false);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(16)), 0, data, 0, 4);
            for (var i = 4; i < 16; i++)
                data[i] = byte.MaxValue;
            PeerIO.EnqueueReceiveMessage(Incoming, new PlainTextEncryption(), null, null, null,
                (successful, count, state) =>
                {
                    success = successful;
                    handle.Set();
                }, null);

            NetworkIO.EnqueueSend(Outgoing, data, 0, 20, null, null, null, delegate { }, null);
            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(4)), "#Should have closed");
            ;
            Assert.False(success);
        }

        [Fact]
        public void ReceiveData()
        {
            DoReceive(false, false);
        }

        [Fact]
        public void ReceiveData_SlowIncoming()
        {
            DoReceive(false, true);
        }

        [Fact]
        public void ReceiveData_SlowIncoming_SlowOutgoing()
        {
            DoReceive(true, true);
        }

        [Fact]
        public void ReceiveData_SlowOutgoing()
        {
            DoReceive(true, false);
        }

        [Fact]
        public void ReceiveTwoKeepAlives()
        {
            var message = new KeepAliveMessage();
            var buffer = message.Encode();
            var handle = new AutoResetEvent(false);

            NetworkIO.EnqueueSend(Outgoing, buffer, 0, buffer.Length, null, null, null, delegate { }, null);
            NetworkIO.EnqueueSend(Outgoing, buffer, 0, buffer.Length, null, null, null, delegate { }, null);

            AsyncMessageReceivedCallback callback = (s, m, state) =>
            {
                if (s && m is KeepAliveMessage)
                    handle.Set();
            };

            PeerIO.EnqueueReceiveMessage(Incoming, new PlainTextEncryption(), null, null, null, callback, null);
            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(2)), "#Should receive first message");


            PeerIO.EnqueueReceiveMessage(Incoming, new PlainTextEncryption(), null, null, null, callback, null);
            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(2)), "#Should receive second message");
        }

        [Fact]
        public void SendData()
        {
            DoSend(false, false);
        }

        [Fact]
        public void SendData_SlowIncoming()
        {
            DoSend(false, true);
        }

        [Fact]
        public void SendData_SlowIncoming_SlowOutgoing()
        {
            DoSend(true, true);
        }

        [Fact]
        public void SendData_SlowOutgoing()
        {
            DoSend(true, false);
        }

        [Fact]
        public void ZeroReceivedClosesConnection()
        {
            var connectionOpen = true;
            var handle = new AutoResetEvent(false);
            Incoming.ManualBytesReceived = 0;
            NetworkIO.EnqueueReceive(Incoming, data, 0, 100, null, null, null, (successful, count, state) =>
            {
                connectionOpen = successful;
                handle.Set();
            }, null);

            NetworkIO.EnqueueSend(Outgoing, data, 0, 100, null, null, null, delegate { }, null);
            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(4)));
            Assert.False(connectionOpen);
        }

        [Fact]
        public void ZeroSentClosesConnection()
        {
            var connectionOpen = true;
            var handle = new AutoResetEvent(false);
            Incoming.ManualBytesSent = 0;
            NetworkIO.EnqueueSend(Incoming, data, 0, 100, null, null, null, (successful, count, state) =>
            {
                connectionOpen = successful;
                handle.Set();
            }, null);

            NetworkIO.EnqueueReceive(Outgoing, data, 0, 100, null, null, null, delegate { }, null);
            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(4)));
            Assert.False(connectionOpen);
        }
    }
}