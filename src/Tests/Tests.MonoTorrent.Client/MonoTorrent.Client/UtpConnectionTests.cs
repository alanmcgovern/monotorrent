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

using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Utp;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class UtpConnectionTests
    {
        UtpPeerConnection Incoming;
        UtpPeerConnectionListener IncomingListener;
        UtpPeerConnection Outgoing;
        UtpPeerConnectionListener OutgoingListener;

        [SetUp]
        public async Task Setup ()
        {
            var tcs = new TaskCompletionSource<UtpPeerConnection> ();
            IncomingListener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            IncomingListener.Start ();

            OutgoingListener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            OutgoingListener.Start ();

            Outgoing = new UtpPeerConnection (OutgoingListener, OutgoingListener.SendQueue, IncomingListener.LocalEndPoint, 123);
            IncomingListener.ConnectionReceived += (o, e) => {
                tcs.SetResult ((UtpPeerConnection) e.Connection);
            };

            await Outgoing.ConnectAsync ();
            Incoming = await tcs.Task;
        }

        [Test]
        public async Task SendRandomBytes ([Values (1, 1399, 1401, 3000, 60_000, 100_000, 1024_000)] int size)
        {
            var sendBuffer = new byte[size];
            var receiveBuffer = new byte[size];
            Random.Shared.NextBytes (sendBuffer);

            await Incoming.SendAsync (sendBuffer).WithTimeout (5000);

            int received = 0;
            while (received != size) {
                received += await Outgoing.ReceiveAsync (receiveBuffer.AsMemory (received)).WithTimeout (5000);
            }
            Assert.IsTrue (sendBuffer.AsSpan ().SequenceEqual (receiveBuffer));
        }

        [TearDown]
        public void Teardown ()
        {
            Incoming?.Dispose ();
            IncomingListener?.Stop ();
            Outgoing?.Dispose ();
            OutgoingListener?.Stop ();
        }
    }
}
