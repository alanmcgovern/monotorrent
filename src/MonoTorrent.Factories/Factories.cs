using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Text;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Connections;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.PiecePicking;
using MonoTorrent.PieceWriter;

namespace MonoTorrent.Client
{
    public partial class Factories
    {
        public delegate IBlockCache BlockCacheCreator (IPieceWriter writer, long capacity, ByteBufferPool buffer);
        public delegate IDhtListener DhtListenerCreator (IPEndPoint endpoint);
        public delegate IHttpRequest HttpRequestCreator ();
        public delegate ILocalPeerDiscovery LocalPeerDiscoveryCreator (int port);
        public delegate IPeerConnection PeerConnectionCreator (Uri uri);
        public delegate IPeerConnectionListener PeerConnectionListenerCreator (IPEndPoint endPoint);
        public delegate IPieceRequester PieceRequesterCreator (ITorrentData torrentData);
        public delegate IPieceWriter PieceWriterCreator (int maxOpenFiles);
        public delegate ISocketConnector SocketConnectorCreator ();
        public delegate IStreamingPieceRequester StreamingPieceRequesterCreator (ITorrentData torrentData);
    }

    public partial class Factories
    {
        public static Factories Default { get; }

        static readonly BlockCacheCreator DefaultBlockCacheCreator;
        static readonly DhtListenerCreator DefaultDhtListenerCreator;
        static readonly HttpRequestCreator DefaultHttpRequestCreator;
        static readonly LocalPeerDiscoveryCreator DefaultLocalPeerDiscoveryCreator;
        static readonly ReadOnlyDictionary<string, PeerConnectionCreator> DefaultPeerConnectionCreators;
        static readonly PeerConnectionListenerCreator DefaultPeerConnectionListenerCreator;
        static readonly PieceRequesterCreator DefaultPieceRequesterCreator;
        static readonly PieceWriterCreator DefaultPieceWriterCreator;
        static readonly SocketConnectorCreator DefaultSocketConnectorCreator;
        static readonly StreamingPieceRequesterCreator DefaultStreamingPieceRequesterCreator;

        static Factories ()
        {
            DefaultBlockCacheCreator = (writer, capacity, buffer) => new MemoryCache (buffer, capacity, writer);
            DefaultDhtListenerCreator = endpoint => new DhtListener (endpoint);
            DefaultHttpRequestCreator = () => new HttpRequest ();
            DefaultLocalPeerDiscoveryCreator = port => new LocalPeerDiscovery (port);
            DefaultPeerConnectionCreators = new ReadOnlyDictionary<string, PeerConnectionCreator> (
                new Dictionary<string, PeerConnectionCreator> {
                { "ipv4", uri => new SocketPeerConnection (uri, new SocketConnector ()) },
                { "ipv6", uri => new SocketPeerConnection (uri, new SocketConnector ()) },
                }
            );
            DefaultPeerConnectionListenerCreator = endPoint => new PeerConnectionListener (endPoint);
            DefaultPieceRequesterCreator = torrentData => new StandardPieceRequester ();
            DefaultPieceWriterCreator = maxOpenFiles => new DiskWriter (maxOpenFiles);
            DefaultSocketConnectorCreator = () => new SocketConnector ();
            DefaultStreamingPieceRequesterCreator = torrentData => new StreamingPieceRequester ();

            Default = new Factories ();
        }

        public BlockCacheCreator CreateBlockCache { get; private set; }
        public DhtListenerCreator CreateDhtListener { get; private set; }
        public HttpRequestCreator CreateHttpRequest { get; private set; }
        public LocalPeerDiscoveryCreator CreateLocalPeerDiscovery { get; private set; }
        public ReadOnlyDictionary<string, PeerConnectionCreator> PeerConnectionCreators { get; private set; }
        public PeerConnectionListenerCreator CreatePeerConnectionListener { get; private set; }
        public PieceRequesterCreator CreatePieceRequester { get; private set; }
        public PieceWriterCreator CreatePieceWriter { get; private set; }
        public SocketConnectorCreator CreateSocketConnector { get; private set; }
        public StreamingPieceRequesterCreator CreateStreamingPieceRequester { get; private set; }

        public Factories ()
        {
            CreateBlockCache = DefaultBlockCacheCreator;
            CreateDhtListener = DefaultDhtListenerCreator;
            CreateHttpRequest = DefaultHttpRequestCreator;
            CreateLocalPeerDiscovery = DefaultLocalPeerDiscoveryCreator;
            PeerConnectionCreators = DefaultPeerConnectionCreators;
            CreatePeerConnectionListener = DefaultPeerConnectionListenerCreator;
            CreatePieceRequester = DefaultPieceRequesterCreator;
            CreatePieceWriter = DefaultPieceWriterCreator;
            CreateSocketConnector = DefaultSocketConnectorCreator;
            CreateStreamingPieceRequester = DefaultStreamingPieceRequesterCreator;
        }

        public Factories WithBlockCacheCreator (BlockCacheCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateBlockCache = creator ?? DefaultBlockCacheCreator;
            return dupe;
        }

        public Factories WithDhtListenerCreator (DhtListenerCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateDhtListener = creator ?? DefaultDhtListenerCreator;
            return dupe;
        }

        public Factories WithHttpRequestCreator (HttpRequestCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateHttpRequest = creator ?? DefaultHttpRequestCreator;
            return dupe;
        }

        public Factories WithLocalPeerDiscoveryCreator (LocalPeerDiscoveryCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateLocalPeerDiscovery = creator ?? DefaultLocalPeerDiscoveryCreator;
            return dupe;
        }

        public Factories WithPeerConnectionCreator (string scheme, PeerConnectionCreator creator)
        {
            var dict = new Dictionary<string, PeerConnectionCreator> (PeerConnectionCreators);
            if (creator == null)
                dict.Remove (scheme);
            else
                dict[scheme] = creator;

            var dupe = MemberwiseClone ();
            dupe.PeerConnectionCreators = new ReadOnlyDictionary<string, PeerConnectionCreator> (dict);
            return dupe;
        }

        public Factories WithPeerConnectionListenerCreator(PeerConnectionListenerCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreatePeerConnectionListener = creator ?? DefaultPeerConnectionListenerCreator;
            return dupe;
        }

        public Factories WithPieceRequesterCreator (PieceRequesterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreatePieceRequester = creator ?? DefaultPieceRequesterCreator;
            return dupe;
        }

        public Factories WithPieceWriterCreator (PieceWriterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreatePieceWriter = creator ?? DefaultPieceWriterCreator;
            return dupe;
        }

        public Factories WithSocketConnectionCreator(SocketConnectorCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateSocketConnector = creator ?? DefaultSocketConnectorCreator;
            return dupe;
        }

        public Factories WithStreamingPieceRequesterCreator (StreamingPieceRequesterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateStreamingPieceRequester = creator ?? DefaultStreamingPieceRequesterCreator;
            return dupe;
        }
        new Factories MemberwiseClone ()
            => (Factories) base.MemberwiseClone ();
    }
}
