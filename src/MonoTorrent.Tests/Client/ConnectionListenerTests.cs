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

        SocketListener listener;

        [SetUp]
        public void Setup()
        {
            listener = new SocketListener(endpoint);
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
                    (await task).Connection.Dispose ();
                }
            }
        }

        [Test]
        public async Task ChangePortTen()
        {
            for (int i = 0; i < 10; i++) {
                endpoint.Port++;
                listener.ChangeEndpoint(endpoint);
                await AcceptTen();
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
