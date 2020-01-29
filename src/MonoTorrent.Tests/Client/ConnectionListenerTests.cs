//
// ConnectionListenerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Threading.Tasks;

using MonoTorrent.Client.Listeners;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class ConnectionListenerTests
    {
        readonly IPEndPoint endpoint = new IPEndPoint (IPAddress.Loopback, 55652);

        IPeerListener listener;

        [SetUp]
        public void Setup ()
        {
            listener = new PeerListener (endpoint);
            listener.Start ();
        }

        [TearDown]
        public void Teardown ()
        {
            listener.Stop ();
        }

        [Test]
        public async Task AcceptTen ()
        {
            for (int i = 0; i < 10; i++) {
                using TcpClient c = new TcpClient (AddressFamily.InterNetwork);
                var task = AcceptSocket ();
                c.Connect (endpoint);
                if (await Task.WhenAny (Task.Delay (1000), task) != task)
                    Assert.Fail ("Failed to establish a connection");
                (await task).Connection.Dispose ();
            }
        }

        [Test]
        public void PortNotFree ()
        {
            var tcs = new TaskCompletionSource<object> ();
            var otherListener = new PeerListener (endpoint);
            otherListener.StatusChanged += (o, e) => tcs.SetResult (null);
            otherListener.Start ();
            Assert.AreEqual (ListenerStatus.PortNotFree, otherListener.Status);
            Assert.IsTrue (tcs.Task.Wait (1000));
        }

        Task<NewConnectionEventArgs> AcceptSocket ()
        {
            var tcs = new TaskCompletionSource<NewConnectionEventArgs> ();
            listener.ConnectionReceived += (o, e) => tcs.TrySetResult (e);
            return tcs.Task;
        }
    }
}
