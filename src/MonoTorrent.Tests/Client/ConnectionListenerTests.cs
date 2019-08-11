using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class ConnectionListenerTests
    {
        readonly IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, 55652);

        IPeerListener listener;

        [SetUp]
        public void Setup()
        {
            listener = new PeerListener(endpoint);
            listener.Start();
        }

        [TearDown]
        public void Teardown()
        {
            listener.Stop();
        }

        [Test]
        public async Task AcceptTen()
        {
            for (int i = 0; i < 10; i ++) {
                using (TcpClient c = new TcpClient(AddressFamily.InterNetwork)) {
                    var task = AcceptSocket ();
                    c.Connect(endpoint);
                    if (await Task.WhenAny (Task.Delay (1000), task) != task)
                        Assert.Fail ("Failed to establish a connection");
                    (await task).Connection.Dispose ();
                }
            }
        }

        Task<NewConnectionEventArgs> AcceptSocket ()
        {
            var tcs = new TaskCompletionSource<NewConnectionEventArgs>();
            listener.ConnectionReceived += (o, e) => tcs.TrySetResult (e);
            return tcs.Task;
        }
    }
}
