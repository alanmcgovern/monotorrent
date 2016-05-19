using System;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace MonoTorrent.Client
{
    public class ConnectionListenerTests : IDisposable
    {
        //static void Main(string[] args)
        //{
        //    ConnectionListenerTests t = new ConnectionListenerTests();
        //    t.Setup();
        //    t.AcceptThree();
        //    t.Teardown();
        //}
        private SocketListener listener;
        private IPEndPoint endpoint;

        public ConnectionListenerTests()
        {
            endpoint = new IPEndPoint(IPAddress.Loopback, 55652);
            listener = new SocketListener(endpoint);
            listener.Start();
            System.Threading.Thread.Sleep(100);
        }

        public void Dispose()
        {
            listener.Stop();
        }

        [Fact]
        public void AcceptThree()
        {
            using (TcpClient c = new TcpClient(AddressFamily.InterNetwork))
                c.Connect(endpoint);
            using (TcpClient c = new TcpClient(AddressFamily.InterNetwork))
                c.Connect(endpoint);
            using (TcpClient c = new TcpClient(AddressFamily.InterNetwork))
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