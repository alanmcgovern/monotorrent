using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace MonoTorrent.Client.Connections
{
    public class IPV6Connection : IConnection
    {
        private bool isIncoming;
        private Socket socket;
        private EndPoint endpoint;

        public IPV6Connection(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (uri.HostNameType != UriHostNameType.IPv6)
                throw new ArgumentException("Uri is not an IPV6 uri", "uri");

            socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            endpoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);
        }

        public IPV6Connection(Socket socket, bool isIncoming)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");

            if (socket.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException("Not an IPV6 socket", "socket");

            this.socket = socket;
            this.endpoint = socket.RemoteEndPoint;
            this.isIncoming = isIncoming;
        }

        public byte[] AddressBytes
        {
            // Fix this - Technically this is only useful for IPV4 Connections for the fast peer
            // extensions. I shouldn't force every inheritor ot need this
            get { return new byte[4]; }
        }

        public bool Connected
        {
            get { return socket.Connected; }
        }

        public virtual bool CanReconnect
        {
            get { return !isIncoming; }
        }

        public bool IsIncoming
        {
            get { return isIncoming; }
        }

        public EndPoint EndPoint
        {
            get { return endpoint; }
        }

        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            return socket.BeginConnect(endpoint, callback, state);
        }

        public void EndConnect(IAsyncResult result)
        {
            socket.EndConnect(result);
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            return socket.EndReceive(result);
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return socket.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            return socket.EndSend(result);
        }

        public void Dispose()
        {
            socket.Close();
        }
    }
}
