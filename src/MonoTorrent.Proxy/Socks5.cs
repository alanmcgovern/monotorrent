using System;
using System.Net;
using System.Net.Http;

using DotProxify;

using MonoTorrent.Client;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Connections.Proxy
{
    public class Socks5 : IDisposable
    {
        ISocketConnector SocketProxy { get; }

        HttpToSocksProxy HttpProxy { get; }

        public Factories Factories { get; }

        public Socks5 (Factories factories, IPEndPoint socksServerEndPoint)
            : this( factories, socksServerEndPoint, Array.Empty<byte> (), Array.Empty<byte> ())
        {

        }
        public Socks5 (Factories factories, IPEndPoint socksServerEndPoint, byte[] username, byte[] password)
        {
            if (factories is null)
                throw new ArgumentNullException (nameof (factories));
            if (socksServerEndPoint is null)
                throw new ArgumentNullException (nameof (socksServerEndPoint));

            var server = new Socks5Server (socksServerEndPoint, username, password);
            HttpProxy = new HttpToSocksProxy (server);
            SocketProxy = new Socks5SocketConnector (server);

            Factories = factories
                .WithHttpClientCreator (() => new HttpClient (new HttpClientHandler { Proxy = HttpProxy, UseProxy = true }))
                .WithSocketConnectorCreator (() => SocketProxy)
                .WithPeerConnectionCreator ("ipv4", uri => new SocketPeerConnection (uri, SocketProxy))
                .WithPeerConnectionCreator ("ipv6", uri => new SocketPeerConnection (uri, SocketProxy))
                .WithDhtListenerCreator (port => null)
                .WithLocalPeerDiscoveryCreator (() => null)
                .WithPeerConnectionListenerCreator (port => null);

            TrackerFactory.Register ("http", uri => new HTTPTracker (uri, Factories.CreateHttpClient));
            TrackerFactory.Register ("https", uri => new HTTPTracker (uri, Factories.CreateHttpClient));
        }

        public void Initialize ()
        {
            HttpProxy.Start ();
        }

        public void Dispose ()
        {
            HttpProxy.Stop ();
        }
    }
}
