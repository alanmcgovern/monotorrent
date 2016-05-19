using System;
using System.Net;
using System.Net.Sockets;

namespace MonoTorrent.Tests.Client
{
    public class ConnectionPair : IDisposable
    {
        private readonly TcpListener socketListener;
        public CustomConnection Incoming;
        public CustomConnection Outgoing;

        public ConnectionPair(int port)
        {
            socketListener = new TcpListener(IPAddress.Loopback, port);
            socketListener.Start();

            var s1a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect(IPAddress.Loopback, port);
            var s1b = socketListener.AcceptSocket();

            Incoming = new CustomConnection(s1a, true);
            Outgoing = new CustomConnection(s1b, false);
            socketListener.Stop();
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            socketListener.Stop();
        }
    }
}