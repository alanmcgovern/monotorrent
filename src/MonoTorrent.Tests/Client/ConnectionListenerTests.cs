using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MonoTorrent.Client;
using Xunit;

namespace MonoTorrent.Tests.Client
{
    public class ConnectionListenerTests : IDisposable
    {
        public ConnectionListenerTests()
        {
            endpoint = new IPEndPoint(IPAddress.Loopback, 55652);
            listener = new SocketListener(endpoint);
            listener.Start();
            Thread.Sleep(100);
        }

        public void Dispose()
        {
            listener.Stop();
        }

        //static void Main(string[] args)
        //{
        //    ConnectionListenerTests t = new ConnectionListenerTests();
        //    t.Setup();
        //    t.AcceptThree();
        //    t.Teardown();
        //}
        private readonly SocketListener listener;
        private readonly IPEndPoint endpoint;

        [Fact]
        public void AcceptThree()
        {
            using (var c = new TcpClient(AddressFamily.InterNetwork))
                c.Connect(endpoint);
            using (var c = new TcpClient(AddressFamily.InterNetwork))
                c.Connect(endpoint);
            using (var c = new TcpClient(AddressFamily.InterNetwork))
                c.Connect(endpoint);
        }

        [Fact]
        public void ChangePortThree()
        {
            endpoint.Port++;
            listener.ChangeEndpoint(endpoint);
            AcceptThree();

            endpoint.Port++;
            listener.ChangeEndpoint(endpoint);
            AcceptThree();

            endpoint.Port++;
            listener.ChangeEndpoint(endpoint);
            AcceptThree();
        }
    }
}