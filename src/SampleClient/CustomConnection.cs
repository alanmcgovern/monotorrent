using System;
using System.Net;
using System.Net.Sockets;
using MonoTorrent.Client.Connections;

namespace SampleClient
{
    public class CustomConnection : IConnection
    {
        private readonly string name;
        private readonly Socket s;

        public CustomConnection(Socket s, bool incoming, string name)
        {
            this.name = name;
            this.s = s;
            IsIncoming = incoming;
        }

        public byte[] AddressBytes
        {
            get { return ((IPEndPoint) s.RemoteEndPoint).Address.GetAddressBytes(); }
        }

        public bool Connected
        {
            get { return s.Connected; }
        }

        public bool CanReconnect
        {
            get { return false; }
        }

        public bool IsIncoming { get; }

        public EndPoint EndPoint
        {
            get { return s.RemoteEndPoint; }
        }

        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            throw new InvalidOperationException();
        }

        public void EndConnect(IAsyncResult result)
        {
            throw new InvalidOperationException();
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return s.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            Console.WriteLine("{0} - {1}", name, "received");
            return s.EndReceive(result);
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return s.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            Console.WriteLine("{0} - {1}", name, "sent");
            return s.EndSend(result);
        }

        public void Dispose()
        {
            s.Close();
        }

        public Uri Uri
        {
            get { return null; }
        }

        public override string ToString()
        {
            return name;
        }
    }
}