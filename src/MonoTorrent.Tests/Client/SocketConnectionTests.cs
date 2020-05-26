//
// SocketConnectionTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using MonoTorrent.Client.Connections;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class SocketConnectionTests
    {
        SocketConnection Incoming;
        SocketConnection Outgoing;

        [SetUp]
        public void Setup ()
        {
            var socketListener = new TcpListener (IPAddress.Loopback, 0);
            socketListener.Start ();

            var s1a = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect (socketListener.LocalEndpoint);

            var s1b = socketListener.AcceptSocket ();

            Incoming = new IPV4Connection (s1a, true);
            Outgoing = new IPV4Connection (s1b, false);
            socketListener.Stop ();

            SocketConnection.ClearStacktraces ();
        }

        [Test]
        public void ReceiveConcurrently ()
        {
            var tcs = new TaskCompletionSource<object> ();
            var receiveTask = Incoming.ReceiveAsync (new byte[100], 0, 100, tcs.Task);
            Assert.ThrowsAsync<ObjectDisposedException> (() => Incoming.ReceiveAsync (new byte[100], 0, 100).AsTask ());
            tcs.SetResult (null);
            Assert.ThrowsAsync<SocketException> (() => receiveTask.AsTask ());
            Assert.AreEqual (2, SocketConnection.DoubleReceiveStacktraces.Length);
        }

        [Test]
        public async Task SendConcurrently ()
        {
            var tcs = new TaskCompletionSource<object> ();
            var sendtask = Incoming.SendAsync (new byte[100], 0, 100, tcs.Task);
            Assert.ThrowsAsync<ObjectDisposedException> (() => Incoming.SendAsync (new byte[100], 0, 100).AsTask ());
            tcs.SetResult (null);
            await sendtask;
            Assert.AreEqual (2, SocketConnection.DoubleSendStacktraces.Length);
        }

        [Test]
        public async Task DisposeWhileReceiving ()
        {
            var task = Incoming.ReceiveAsync (new byte[100], 0, 100).AsTask ();
            Incoming.Dispose ();

            // All we care about is that the task is marked as 'Complete'.
            try {
                await task.WithTimeout (1000);
            } catch (SocketException) {
                // Socket exceptions are expected if we dispose while receiving
            }
            Assert.IsTrue (task.IsCompleted, "#1");
            GC.KeepAlive (task.Exception); // observe the exception (if any)
        }

        [Test]
        public async Task DisposeWhileSending ()
        {
            var task = Incoming.SendAsync (new byte[1000000], 0, 1000000).AsTask ();
            Incoming.Dispose ();

            // All we care about is that the task is marked as 'Complete'.
            try {
                await task.WithTimeout (1000);
            } catch (SocketException) {
                // Socket exceptions are expected if we dispose while sending
            }
            Assert.IsTrue (task.IsCompleted, "#1");
            GC.KeepAlive (task.Exception); // observe the exception (if any)
        }

        [TearDown]
        public void Teardown ()
        {
            Incoming?.Dispose ();
            Outgoing?.Dispose ();
        }
    }
}
