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
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;

using MonoTorrent.Connections.Peer;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    public class SocketConnectionTests
    {
        IPeerConnection Incoming;
        IPeerConnection Outgoing;

        [SetUp]
        public void Setup ()
        {
            var socketListener = new TcpListener (IPAddress.Loopback, 0);
            socketListener.Start ();

            var s1a = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect (socketListener.LocalEndpoint);

            var s1b = socketListener.AcceptSocket ();

            Incoming = new SocketPeerConnection (s1a, true);
            Outgoing = new SocketPeerConnection (s1b, false);

            Assert.AreEqual (AddressFamily.InterNetwork, s1a.AddressFamily);
            Assert.AreEqual (AddressFamily.InterNetwork, s1b.AddressFamily);

            Assert.AreEqual ("ipv4", Incoming.Uri.Scheme);
            Assert.AreEqual ("ipv4", Outgoing.Uri.Scheme);

            socketListener.Stop ();
        }

        [Test]
        public void TestIPV6Connection ()
        {
            Assume.That (Socket.OSSupportsIPv6);

            var socketListener = new TcpListener (IPAddress.IPv6Loopback, 0);
            socketListener.Start ();

            using var s1a = new Socket (AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect (socketListener.LocalEndpoint);

            using var s1b = socketListener.AcceptSocket ();

            using var incoming = new SocketPeerConnection (s1a, true);
            using var outgoing = new SocketPeerConnection (s1b, false);

            Assert.AreEqual (AddressFamily.InterNetworkV6, s1a.AddressFamily);
            Assert.AreEqual (AddressFamily.InterNetworkV6, s1b.AddressFamily);

            Assert.AreEqual ("ipv6", incoming.Uri.Scheme);
            Assert.AreEqual ("ipv6", outgoing.Uri.Scheme);

            socketListener.Stop ();
        }

        [Test]
        public async Task DisposeWhileReceiving ()
        {
            using var releaser = MemoryPool.Default.Rent (100, out Memory<byte> buffer);
            var task = Incoming.ReceiveAsync (buffer).AsTask ();
            Incoming.Dispose ();

            // All we care about is that the task is marked as 'Complete'.
            _ = await Task.WhenAny (task).WithTimeout (1000);
            Assert.IsTrue (task.IsCompleted, "#1");
            GC.KeepAlive (task.Exception); // observe the exception (if any)
        }

        [Test]
        public async Task DisposeWhileSending ()
        {
            using var releaser = MemoryPool.Default.Rent (1000000, out Memory<byte> buffer);
            var task = Incoming.SendAsync (buffer).AsTask ();
            Incoming.Dispose ();

            // All we care about is that the task is marked as 'Complete'.
            _ = await Task.WhenAny (task).WithTimeout (1000);
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
