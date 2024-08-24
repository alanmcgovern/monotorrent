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


using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using MonoTorrent.Client.RateLimiters;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class NetworkIOTests
    {
        ConnectionPair pair;

        CustomConnection Incoming => pair.Incoming;

        CustomConnection Outgoing => pair.Outgoing;

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
        public async Task ConnectAsync ()
        {
            var listener = new TcpListener (IPAddress.Loopback, 0);
            listener.Start ();
            try {
                using var c = Factories.Default.CreatePeerConnection (new Uri ($"ipv4://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"));
                var connectTask = NetworkIO.ConnectAsync (c);

                var receivingSocket = await listener.AcceptSocketAsync ().WithTimeout ();
                await connectTask.WithTimeout ();
            } finally {
                listener.Stop ();
            }
        }

        [Test]
        public void DisposeBeforeConnect ()
        {
            using var c = Factories.Default.CreatePeerConnection (new Uri ($"ipv4://127.0.0.1:12345"));
            c.Dispose ();
            Assert.ThrowsAsync<ObjectDisposedException> (async () => await NetworkIO.ConnectAsync (c));
        }

        [Test]
        public async Task DisposeBeforeReceive ()
        {
            var listener = new TcpListener (IPAddress.Loopback, 0);
            listener.Start ();
            try {
                using var c = Factories.Default.CreatePeerConnection (new Uri ($"ipv4://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"));
                var connectTask = NetworkIO.ConnectAsync (c);

                var receivingSocket = await listener.AcceptSocketAsync ().WithTimeout ();
                await connectTask.WithTimeout ();

                c.Dispose ();
                using var releaser = MemoryPool.Default.Rent (123, out Memory<byte> buffer);
                Assert.AreEqual (0, await c.ReceiveAsync (buffer).WithTimeout ());
            } finally {
                listener.Stop ();
            }
        }

        [Test]
        public async Task DisposeBeforeSend ()
        {
            var listener = new TcpListener (IPAddress.Loopback, 0);
            listener.Start ();
            try {
                using var c = Factories.Default.CreatePeerConnection (new Uri ($"ipv4://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"));
                var connectTask = NetworkIO.ConnectAsync (c);

                var receivingSocket = await listener.AcceptSocketAsync ().WithTimeout ();
                await connectTask.WithTimeout ();

                c.Dispose ();
                using var releaser = MemoryPool.Default.Rent (123, out Memory<byte> buffer);
                Assert.AreEqual (0, await c.SendAsync (buffer).WithTimeout ());
            } finally {
                listener.Stop ();
            }
        }

        [Test]
        public async Task ReceiveData_Unlimited ()
        {
            var oneMegabyte = 1 * 1024 * 1024;
            var limiter = new RateLimiterGroup ();

            using var r1 = MemoryPool.Default.Rent (oneMegabyte, out Memory<byte> sendBuffer);
            using var r2 = MemoryPool.Default.Rent (oneMegabyte, out Memory<byte> receiveBuffer);

            await Outgoing.SendAsync (sendBuffer);
            await NetworkIO.ReceiveAsync (Incoming, receiveBuffer, limiter, null, null);

            Assert.AreEqual (1, Incoming.Receives.Count, "#1");
        }

        [Test]
        public async Task ReceiveData_SlowIncoming_SlowOutgoing ()
        {
            await DoReceive (true, true);
        }

        [Test]
        public async Task ReceiveData_SlowIncoming ()
        {
            await DoReceive (false, true);
        }

        [Test]
        public async Task ReceiveData_SlowOutgoing ()
        {
            await DoReceive (true, false);
        }

        [Test]
        public async Task ReceiveData ()
        {
            await DoReceive (false, false);
        }

        async Task DoReceive (bool slowOutgoing, bool slowIncoming)
        {
            Incoming.SlowConnection = slowIncoming;
            Outgoing.SlowConnection = slowOutgoing;

            using var r1 = MemoryPool.Default.Rent (16384, out Memory<byte> data);
            using var r2 = MemoryPool.Default.Rent (16384, out Memory<byte> buffer);
            new Random ().NextBytes (data.Span);

            int sent = 0;
            var task = NetworkIO.ReceiveAsync (Outgoing, buffer, null, null, null);

            while (sent != buffer.Length) {
                int r = await Incoming.SendAsync (data.Slice (sent));
                Assert.AreNotEqual (0, r, "#Received data");
                sent += r;
            }

            await task.WithTimeout (TimeSpan.FromSeconds (10));
            for (int i = 0; i < buffer.Length; i++) {
                if (!data.Span.SequenceEqual (buffer.Span))
                    Assert.Fail ($"Buffers differ at position {i}");
            }
        }

        [Test]
        public async Task SendData_SlowIncoming_SlowOutgoing ()
        {
            await DoSend (true, true);
        }

        [Test]
        public async Task SendData_SlowOutgoing ()
        {
            await DoSend (true, false);
        }

        [Test]
        public async Task SendData_SlowIncoming ()
        {
            await DoSend (false, true);
        }

        [Test]
        public async Task SendData ()
        {
            await DoSend (false, false);
        }

        [Test]
        public async Task SendData_Unlimited ()
        {
            var oneMegabyte = 1 * 1024 * 1024;
            var limiter = new RateLimiterGroup ();

            using var releaser = MemoryPool.Default.Rent (oneMegabyte, out Memory<byte> buffer);
            await NetworkIO.SendAsync (Incoming, buffer, limiter, null, null);

            Assert.AreEqual (1, Incoming.Sends.Count, "#1");
        }

        async Task DoSend (bool slowOutgoing, bool slowIncoming)
        {
            Incoming.SlowConnection = slowIncoming;
            Outgoing.SlowConnection = slowOutgoing;

            using var r1 = MemoryPool.Default.Rent (16384, out Memory<byte> sendBuffer);
            using var r2 = MemoryPool.Default.Rent (16384, out Memory<byte> receiveBuffer);
            new Random ().NextBytes (sendBuffer.Span);
            var task = NetworkIO.SendAsync (Outgoing, sendBuffer, null, null, null);

            int received = 0;
            while (received != receiveBuffer.Length) {
                int r = await Incoming.ReceiveAsync (receiveBuffer.Slice (received));
                Assert.AreNotEqual (0, r, "#Received data");
                received += r;
            }
            await task.WithTimeout (TimeSpan.FromSeconds (10));
            Assert.IsTrue (receiveBuffer.Span.SequenceEqual (sendBuffer.Span), "Data matches");
        }

        [Test]
        public async Task ZeroReceivedClosesConnection ()
        {
            using var releaser = MemoryPool.Default.Rent (100, out Memory<byte> buffer);
            Incoming.ManualBytesReceived = 0;
            var receiveTask = NetworkIO.ReceiveAsync (Incoming, buffer, null, null, null);

            var sendTask = NetworkIO.SendAsync (Outgoing, buffer, null, null, null);
            Assert.ThrowsAsync<ConnectionClosedException> (async () => await receiveTask);
            await sendTask;
        }

        [Test]
        public void ZeroSentClosesConnection ()
        {
            using var releaser = MemoryPool.Default.Rent (100, out Memory<byte> buffer);
            Incoming.ManualBytesSent = 0;
            var task = NetworkIO.SendAsync (Incoming, buffer, null, null, null);

            _ = NetworkIO.ReceiveAsync (Outgoing, buffer, null, null, null);
            Assert.ThrowsAsync<ConnectionClosedException> (async () => await task);
        }
    }
}

