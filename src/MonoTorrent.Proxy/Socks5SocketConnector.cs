using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using DotProxify;
using ReusableTasks;

namespace MonoTorrent.Connections.Proxy
{
    class Socks5SocketConnector : ISocketConnector
    {
        Socks5Server SocksServer { get; }

        public Socks5SocketConnector (Socks5Server socksServer)
            => SocksServer = socksServer ?? throw new ArgumentNullException (nameof (SocksServer));

        public ReusableTask<Socket> ConnectAsync (Uri uri, CancellationToken token)
            => SocksServer.ConnectTcp (new IPEndPoint (IPAddress.Parse (uri.Host), uri.Port));
    }
}
