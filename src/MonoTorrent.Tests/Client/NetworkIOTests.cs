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
            pair = new ConnectionPair ().WithTimeout ();
        }

        [TearDown]
        public void Teardown ()
        {
            pair.Dispose ();
        }

        [Test]
        public async Task ReceiveData_RateLimited ()
        {
            // Allow 1 megabyte worth of data
            var oneMegabyte = 1 * 1024 * 1024;
            var limiter = new RateLimiter ();
            limiter.UpdateChunks (oneMegabyte, oneMegabyte);

            await Outgoing.SendAsync (new byte [oneMegabyte], 0, oneMegabyte);
            await NetworkIO.ReceiveAsync (Incoming, new byte[oneMegabyte], 0, oneMegabyte, limiter, null, null);

            var expectedChunks = (int)Math.Ceiling (oneMegabyte / (double)NetworkIO.ChunkLength);
            Assert.AreEqual (expectedChunks, Incoming.Receives.Count, "#1");
        }

        [Test]
        public async Task ReceiveData_Unlimited ()
        {
            var oneMegabyte = 1 * 1024 * 1024;
            var limiter = new RateLimiterGroup ();

            await Outgoing.SendAsync (new byte [oneMegabyte], 0, oneMegabyte);
            await NetworkIO.ReceiveAsync (Incoming, new byte[oneMegabyte], 0, oneMegabyte, limiter, null, null);

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

            var data = new byte [16384];
            new Random ().NextBytes (data);

            int sent = 0;
            var buffer = new byte [data.Length];

            var task = NetworkIO.ReceiveAsync(Outgoing, buffer, 0, buffer.Length, null, null, null);

            while (sent != buffer.Length) {
                int r = await Incoming.SendAsync (data, sent, data.Length - sent);
                Assert.AreNotEqual (0, r, "#Received data");
                sent += r;
            }

            Assert.DoesNotThrowAsync (() => task.WithTimeout (TimeSpan.FromSeconds (10)), "Data should be all received");
            for (int i = 0; i < buffer.Length; i++) {
                if (data[i] != buffer[i])
                    Assert.Fail ("Buffers differ at position " + i);
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
        public async Task SendData_RateLimited ()
        {
            // Allow 1 megabyte worth of data
            var oneMegabyte = 1 * 1024 * 1024;
            var limiter = new RateLimiter ();
            limiter.UpdateChunks (oneMegabyte, oneMegabyte);

            await NetworkIO.SendAsync (Incoming, new byte [oneMegabyte], 0, oneMegabyte, limiter, null, null);

            var expectedChunks = (int)Math.Ceiling (oneMegabyte / (double)NetworkIO.ChunkLength);
            Assert.AreEqual (expectedChunks, Incoming.Sends.Count, "#1");
        }

        [Test]
        public async Task SendData_Unlimited ()
        {
            var oneMegabyte = 1 * 1024 * 1024;
            var limiter = new RateLimiterGroup ();

            await NetworkIO.SendAsync (Incoming, new byte [oneMegabyte], 0, oneMegabyte, limiter, null, null);

            Assert.AreEqual (1, Incoming.Sends.Count, "#1");
        }

        async Task DoSend (bool slowOutgoing, bool slowIncoming)
        {
            Incoming.SlowConnection = slowIncoming;
            Outgoing.SlowConnection = slowOutgoing;

            var data = new byte [16384];
            new Random ().NextBytes (data);
            var task = NetworkIO.SendAsync(Outgoing, data, 0, data.Length, null, null, null);

            int received = 0;
            byte[] buffer = new byte [data.Length];
            while (received != buffer.Length) {
                int r = await Incoming.ReceiveAsync (buffer, received, buffer.Length - received);
                Assert.AreNotEqual (0, r, "#Received data");
                received += r;
            }
            Assert.DoesNotThrowAsync (() => task.WithTimeout (TimeSpan.FromSeconds (1)), "Data should be all sent");
            Assert.IsTrue (Toolbox.ByteMatch (buffer, data), "Data matches");
        }

        [Test]
        public async Task ZeroReceivedClosesConnection ()
        {
            var data = new byte[100];
            Incoming.ManualBytesReceived = 0;
            var receiveTask = NetworkIO.ReceiveAsync(Incoming, data, 0, data.Length, null, null, null);

            var sendTask = NetworkIO.SendAsync (Outgoing, data, 0, data.Length, null, null, null);
            Assert.ThrowsAsync<ConnectionClosedException> (async () => await receiveTask);
            await sendTask;
        }

        [Test]
        public void ZeroSentClosesConnection ()
        {
            var data = new byte[100];
            Incoming.ManualBytesSent = 0;
            var task = NetworkIO.SendAsync (Incoming, data, 0, data.Length, null, null, null);

            _ = NetworkIO.ReceiveAsync (Outgoing, data, 0, data.Length, null, null, null);
            Assert.ThrowsAsync<ConnectionClosedException> (async () => await task);
        }
    }
}

