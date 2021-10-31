using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Cryptography;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Connections;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.PiecePicking;
using MonoTorrent.PieceWriter;
using MonoTorrent.PortForwarding;

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
        public delegate IPortForwarder PortForwarderCreator ();
        public delegate ISocketConnector SocketConnectorCreator ();
        public delegate IStreamingPieceRequester StreamingPieceRequesterCreator (ITorrentData torrentData);

        public delegate MD5 MD5Creator ();
        public delegate SHA1 SHA1Creator ();
    }

    public partial class Factories
    {
        public static Factories Default { get; } = new Factories ();

        public BlockCacheCreator CreateBlockCache { get; private set; }
        public DhtListenerCreator CreateDhtListener { get; private set; }
        public HttpRequestCreator CreateHttpRequest { get; private set; }
        public LocalPeerDiscoveryCreator CreateLocalPeerDiscovery { get; private set; }
        public ReadOnlyDictionary<string, PeerConnectionCreator> PeerConnectionCreators { get; private set; }
        public PeerConnectionListenerCreator CreatePeerConnectionListener { get; private set; }
        public PieceRequesterCreator CreatePieceRequester { get; private set; }
        public PieceWriterCreator CreatePieceWriter { get; private set; }
        public PortForwarderCreator CreatePortForwarder { get; private set; }
        public SocketConnectorCreator CreateSocketConnector { get; private set; }
        public StreamingPieceRequesterCreator CreateStreamingPieceRequester { get; private set; }

        public MD5Creator CreateMD5 { get; private set; }
        public SHA1Creator CreateSHA1 { get; private set; }

        public Factories ()
        {
            CreateMD5 = () => MD5.Create ();
            CreateSHA1 = () => SHA1.Create ();

            CreateBlockCache = (writer, capacity, buffer) => new MemoryCache (buffer, capacity, writer);
            CreateDhtListener = endpoint => new DhtListener (endpoint);
            CreateHttpRequest = () => new HttpRequest ();
            CreateLocalPeerDiscovery = port => new LocalPeerDiscovery (port);
            PeerConnectionCreators = new ReadOnlyDictionary<string, PeerConnectionCreator> (
                new Dictionary<string, PeerConnectionCreator> {
                { "ipv4", uri => new SocketPeerConnection (uri, new SocketConnector ()) },
                { "ipv6", uri => new SocketPeerConnection (uri, new SocketConnector ()) },
                }
            );
            CreatePeerConnectionListener = endPoint => new PeerConnectionListener (endPoint);
            CreatePieceRequester = torrentData => new StandardPieceRequester ();
            CreatePieceWriter = maxOpenFiles => new DiskWriter (maxOpenFiles);
            CreatePortForwarder = () => new MonoNatPortForwarder ();
            CreateSocketConnector = () => new SocketConnector ();
            CreateStreamingPieceRequester = torrentData => new StreamingPieceRequester ();
        }

        public Factories WithBlockCacheCreator (BlockCacheCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateBlockCache = creator ?? Default.CreateBlockCache;
            return dupe;
        }

        public Factories WithDhtListenerCreator (DhtListenerCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateDhtListener = creator ?? Default.CreateDhtListener;
            return dupe;
        }

        public Factories WithHttpRequestCreator (HttpRequestCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateHttpRequest = creator ?? Default.CreateHttpRequest;
            return dupe;
        }

        public Factories WithLocalPeerDiscoveryCreator (LocalPeerDiscoveryCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateLocalPeerDiscovery = creator ?? Default.CreateLocalPeerDiscovery;
            return dupe;
        }

        public Factories WithMD5Creator (MD5Creator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateMD5 = creator ?? Default.CreateMD5;
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
            dupe.CreatePeerConnectionListener = creator ?? Default.CreatePeerConnectionListener;
            return dupe;
        }

        public Factories WithPieceRequesterCreator (PieceRequesterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreatePieceRequester = creator ?? Default.CreatePieceRequester;
            return dupe;
        }

        public Factories WithPieceWriterCreator (PieceWriterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreatePieceWriter = creator ?? Default.CreatePieceWriter;
            return dupe;
        }
        public Factories WithPortForwarderCreator (PortForwarderCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreatePortForwarder = creator ?? Default.CreatePortForwarder;
            return dupe;
        }

        public Factories WithSHA1Creator (SHA1Creator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateSHA1 = creator ?? Default.CreateSHA1;
            return dupe;
        }

        public Factories WithSocketConnectionCreator(SocketConnectorCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateSocketConnector = creator ?? Default.CreateSocketConnector;
            return dupe;
        }

        public Factories WithStreamingPieceRequesterCreator (StreamingPieceRequesterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.CreateStreamingPieceRequester = creator ?? Default.CreateStreamingPieceRequester;
            return dupe;
        }
        new Factories MemberwiseClone ()
            => (Factories) base.MemberwiseClone ();
    }
}
