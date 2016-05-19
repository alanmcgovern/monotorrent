using System;
using System.Net;
using System.Net.Sockets;
using MonoTorrent.Client.Connections;

namespace SampleClient
{
    public class ConnectionPair : IDisposable
    {
        private readonly TcpListener socketListener;
        public IConnection Incoming;
        public IConnection Outgoing;

        public ConnectionPair(int port)
        {
            socketListener = new TcpListener(port);
            socketListener.Start();

            var s1a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect(IPAddress.Loopback, port);
            var s1b = socketListener.AcceptSocket();

            Incoming = new CustomConnection(s1a, true, "1A");
            Outgoing = new CustomConnection(s1b, false, "1B");
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            socketListener.Stop();
        }
    }
}