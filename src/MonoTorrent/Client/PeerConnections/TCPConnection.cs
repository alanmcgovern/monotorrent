using System;
using System.Net;
using System.Net.Sockets;

namespace MonoTorrent.Client.Connections
{
    public class IPV4Connection : IConnection
    {
        private readonly Socket socket;

        #region Member Variables

        public bool CanReconnect
        {
            get { return !IsIncoming; }
        }

        public bool Connected
        {
            get { return socket.Connected; }
        }

        EndPoint IConnection.EndPoint
        {
            get { return EndPoint; }
        }

        public IPEndPoint EndPoint { get; }

        public bool IsIncoming { get; }

        public Uri Uri { get; }

        #endregion

        #region Constructors

        public IPV4Connection(Uri uri)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
                new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port),
                false)
        {
            Uri = uri;
        }

        public IPV4Connection(IPEndPoint endPoint)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), endPoint, false)
        {
        }

        public IPV4Connection(Socket socket, bool isIncoming)
            : this(socket, (IPEndPoint) socket.RemoteEndPoint, isIncoming)
        {
        }


        private IPV4Connection(Socket socket, IPEndPoint endpoint, bool isIncoming)
        {
            this.socket = socket;
            EndPoint = endpoint;
            IsIncoming = isIncoming;
        }

        #endregion

        #region Async Methods

        public byte[] AddressBytes
        {
            get { return EndPoint.Address.GetAddressBytes(); }
        }

        public IAsyncResult BeginConnect(AsyncCallback peerEndCreateConnection, object state)
        {
            return socket.BeginConnect(EndPoint, peerEndCreateConnection, state);
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback asyncCallback, object state)
        {
            return socket.BeginReceive(buffer, offset, count, SocketFlags.None, asyncCallback, state);
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback asyncCallback, object state)
        {
            return socket.BeginSend(buffer, offset, count, SocketFlags.None, asyncCallback, state);
        }

        public void Dispose()
        {
            ((IDisposable) socket).Dispose();
        }

        public void EndConnect(IAsyncResult result)
        {
            socket.EndConnect(result);
        }

        public int EndSend(IAsyncResult result)
        {
            return socket.EndSend(result);
        }

        public int EndReceive(IAsyncResult result)
        {
            return socket.EndReceive(result);
        }

        #endregion
    }
}