using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
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

            try {
                Assert.IsTrue (task.Wait (1000), "#1");
            } catch (AggregateException ex) {
                Assert.IsInstanceOf<SocketException> (ex.InnerException, "#3");
            }
        }

        [Test]
        public void DisposeWhileSending ()
        {
            var task = Incoming.SendAsync (new byte[1000000], 0, 1000000);
            Incoming.Dispose ();

            try {
                Assert.IsTrue (task.Wait (1000), "#1");
            } catch (AggregateException ex) {
                Assert.IsInstanceOf<SocketException> (ex.InnerException, "#3");
            }
        }


        [TearDown]
        public void Teardown ()
        {
            Incoming?.Dispose ();
            Outgoing?.Dispose ();
        }
    }
}
