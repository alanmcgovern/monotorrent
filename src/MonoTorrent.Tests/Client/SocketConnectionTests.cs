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


using System.Net;
using System.Net.Sockets;

using MonoTorrent.Client.Connections;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    public class SocketConnectionTests
    {
        SocketConnection Incoming;
        SocketConnection Outgoing;

        [SetUp]
        public void Setup ()
        {
            var socketListener = new TcpListener(IPAddress.Loopback, 0);
            socketListener.Start();

            var s1a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect(socketListener.LocalEndpoint);

            var s1b = socketListener.AcceptSocket();

            Incoming = new IPV4Connection (s1a, true);
            Outgoing = new IPV4Connection (s1b, false);
            socketListener.Stop();
        }

        [Test]
        public void DisposeWhileReceiving ()
        {
            var task = Incoming.ReceiveAsync (new byte[100], 0, 100);
            Incoming.Dispose ();

            Assert.ThrowsAsync <SocketException> (() => task.WithTimeout (), "#1");
        }

        [Test]
        public void DisposeWhileSending ()
        {
            var task = Incoming.SendAsync (new byte[1000000], 0, 1000000);
            Incoming.Dispose ();

            Assert.ThrowsAsync <SocketException> (() => task.WithTimeout (), "#1");
        }

        [TearDown]
        public void Teardown ()
        {
            Incoming?.Dispose ();
            Outgoing?.Dispose ();
        }
    }
}
