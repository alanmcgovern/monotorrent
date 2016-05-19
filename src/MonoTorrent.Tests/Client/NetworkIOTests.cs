//
// NetworkIOTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2010 Alan McGovern
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

using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using System;
using System.Net;
using System.Threading;
using Xunit;

namespace MonoTorrent.Client
{
    public class NetworkIOTests : IDisposable
    {
        byte[] buffer;
        byte[] data;
        ConnectionPair pair;

        CustomConnection Incoming
        {
            get { return pair.Incoming; }
        }

        CustomConnection Outgoing
        {
            get { return pair.Outgoing; }
        }

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

        [Fact]
        public void ReceiveData_SlowIncoming_SlowOutgoing()
        {
            DoReceive(true, true);
        }

        [Fact]
        public void ReceiveData_SlowIncoming()
        {
            DoReceive(false, true);
        }

        [Fact]
        public void ReceiveData_SlowOutgoing()
        {
            DoReceive(true, false);
        }

        [Fact]
        public void ReceiveData()
        {
            DoReceive(false, false);
        }

        void DoReceive(bool slowOutgoing, bool slowIncoming)
        {
            Incoming.SlowConnection = slowIncoming;
            Outgoing.SlowConnection = slowOutgoing;

            int sent = 0;
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
                int r = Incoming.Send(data, sent, data.Length - sent);
                Assert.NotEqual(0, r);
                sent += r;
            }

            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(4)), "Data should be all received");
            for (int i = 0; i < buffer.Length; i++)
            {
                if (data[i] != buffer[i])
                    Assert.True(false, "Buffers differ at position " + i);
            }
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
        public void SendData_SlowIncoming()
        {
            DoSend(false, true);
        }

        [Fact]
        public void SendData()
        {
            DoSend(false, false);
        }

        public void DoSend(bool slowOutgoing, bool slowIncoming)
        {
            Incoming.SlowConnection = slowIncoming;
            Outgoing.SlowConnection = slowOutgoing;

            var handle = new ManualResetEvent(false);
            NetworkIO.EnqueueSend(Outgoing, data, 0, data.Length, null, null, null, delegate { handle.Set(); }, null);

            int received = 0;
            byte[] buffer = new byte[data.Length];
            while (received != buffer.Length)
            {
                int r = Incoming.Receive(buffer, received, buffer.Length - received);
                Assert.NotEqual(0, r);
                received += r;
            }
            Assert.True(handle.WaitOne(TimeSpan.FromSeconds(1)), "Data should be all sent");
            Assert.True(Toolbox.ByteMatch(buffer, data), "Data matches");
        }

        [Fact]
        public void InvalidMessage()
        {
            bool success = true;
            ManualResetEvent handle = new ManualResetEvent(false);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(16)), 0, data, 0, 4);
            for (int i = 4; i < 16; i++)
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
        public void ZeroReceivedClosesConnection()
        {
            bool connectionOpen = true;
            AutoResetEvent handle = new AutoResetEvent(false);
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
            bool connectionOpen = true;
            AutoResetEvent handle = new AutoResetEvent(false);
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