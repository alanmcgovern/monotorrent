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
        public delegate ILocalPeerDiscovery LocalPeerDiscoveryCreator ();
        public delegate IPeerConnection PeerConnectionCreator (Uri uri);
        public delegate IPeerConnectionListener PeerConnectionListenerCreator (IPEndPoint endPoint);
        public delegate IPieceRequester PieceRequesterCreator ();
        public delegate IPieceWriter PieceWriterCreator (int maxOpenFiles);
        public delegate IPortForwarder PortForwarderCreator ();
        public delegate ISocketConnector SocketConnectorCreator ();
        public delegate IStreamingPieceRequester StreamingPieceRequesterCreator ();

        public delegate MD5 MD5Creator ();
        public delegate SHA1 SHA1Creator ();
    }

    public partial class Factories
    {
        public static Factories Default { get; } = new Factories ();

        BlockCacheCreator BlockCacheFunc { get; set; }
        DhtListenerCreator DhtListenerFunc { get; set; }
        HttpRequestCreator HttpRequestFunc { get; set; }
        LocalPeerDiscoveryCreator LocalPeerDiscoveryFunc { get; set; }
        ReadOnlyDictionary<string, PeerConnectionCreator> PeerConnectionFuncs { get; set; }
        PeerConnectionListenerCreator PeerConnectionListenerFunc { get; set; }
        PieceRequesterCreator PieceRequesterFunc { get; set; }
        PieceWriterCreator PieceWriterFunc { get; set; }
        PortForwarderCreator PortForwarderFunc { get; set; }
        SocketConnectorCreator SocketConnectorFunc { get; set; }
        StreamingPieceRequesterCreator StreamingPieceRequesterFunc { get; set; }

        MD5Creator MD5Func { get; set; }
        SHA1Creator SHA1Func { get; set; }

        public Factories ()
        {
            MD5Func = () => MD5.Create ();
            SHA1Func = () => SHA1.Create ();

            BlockCacheFunc = (writer, capacity, buffer) => new MemoryCache (buffer, capacity, writer);
            DhtListenerFunc = endpoint => new DhtListener (endpoint);
            HttpRequestFunc = () => new HttpRequest ();
            LocalPeerDiscoveryFunc = () => new LocalPeerDiscovery ();
            PeerConnectionFuncs = new ReadOnlyDictionary<string, PeerConnectionCreator> (
                new Dictionary<string, PeerConnectionCreator> {
                { "ipv4", uri => new SocketPeerConnection (uri, new SocketConnector ()) },
                { "ipv6", uri => new SocketPeerConnection (uri, new SocketConnector ()) },
                }
            );
            PeerConnectionListenerFunc = endPoint => new PeerConnectionListener (endPoint);
            PieceRequesterFunc = () => new StandardPieceRequester ();
            PieceWriterFunc = maxOpenFiles => new DiskWriter (maxOpenFiles);
            PortForwarderFunc = () => new MonoNatPortForwarder ();
            SocketConnectorFunc = () => new SocketConnector ();
            StreamingPieceRequesterFunc = () => new StreamingPieceRequester ();
        }

        public IBlockCache CreateBlockCache (IPieceWriter writer, long capacity, ByteBufferPool buffer)
            => BlockCacheFunc (writer, capacity, buffer);
        public Factories WithBlockCacheCreator (BlockCacheCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.BlockCacheFunc = creator ?? Default.BlockCacheFunc;
            return dupe;
        }

        public IDhtListener CreateDhtListener (IPEndPoint endPoint)
            => DhtListenerFunc (endPoint);
        public Factories WithDhtListenerCreator (DhtListenerCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.DhtListenerFunc = creator ?? Default.DhtListenerFunc;
            return dupe;
        }

        public IHttpRequest CreateHttpRequest ()
            => HttpRequestFunc ();
        public Factories WithHttpRequestCreator (HttpRequestCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.HttpRequestFunc = creator ?? Default.HttpRequestFunc;
            return dupe;
        }

        public ILocalPeerDiscovery CreateLocalPeerDiscovery ()
            => LocalPeerDiscoveryFunc ();
        public Factories WithLocalPeerDiscoveryCreator (LocalPeerDiscoveryCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.LocalPeerDiscoveryFunc = creator ?? Default.LocalPeerDiscoveryFunc;
            return dupe;
        }

        public MD5 CreateMD5 ()
            => MD5Func ();
        public Factories WithMD5Creator (MD5Creator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.MD5Func = creator ?? Default.MD5Func;
            return dupe;
        }

        public IPeerConnection CreatePeerConnection (Uri uri)
        {
            try {
                if (PeerConnectionFuncs.TryGetValue (uri.Scheme, out var creator))
                    return creator (uri);
            } catch {

            }
            return null;
        }
        public Factories WithPeerConnectionCreator (string scheme, PeerConnectionCreator creator)
        {
            var dict = new Dictionary<string, PeerConnectionCreator> (PeerConnectionFuncs);
            if (creator == null)
                dict.Remove (scheme);
            else
                dict[scheme] = creator;

            var dupe = MemberwiseClone ();
            dupe.PeerConnectionFuncs = new ReadOnlyDictionary<string, PeerConnectionCreator> (dict);
            return dupe;
        }

        public IPeerConnectionListener CreatePeerConnectionListener (IPEndPoint endPoint)
            => PeerConnectionListenerFunc (endPoint);
        public Factories WithPeerConnectionListenerCreator(PeerConnectionListenerCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.PeerConnectionListenerFunc = creator ?? Default.PeerConnectionListenerFunc;
            return dupe;
        }

        public IPieceRequester CreatePieceRequester ()
            => PieceRequesterFunc ();
        public Factories WithPieceRequesterCreator (PieceRequesterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.PieceRequesterFunc = creator ?? Default.PieceRequesterFunc;
            return dupe;
        }

        public IPieceWriter CreatePieceWriter (int maxOpenFiles)
            => PieceWriterFunc (maxOpenFiles);
        public Factories WithPieceWriterCreator (PieceWriterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.PieceWriterFunc = creator ?? Default.PieceWriterFunc;
            return dupe;
        }

        public IPortForwarder CreatePortForwarder ()
            => PortForwarderFunc ();
        public Factories WithPortForwarderCreator (PortForwarderCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.PortForwarderFunc = creator ?? Default.PortForwarderFunc;
            return dupe;
        }

        public SHA1 CreateSHA1 ()
            => SHA1Func ();
        public Factories WithSHA1Creator (SHA1Creator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.SHA1Func = creator ?? Default.SHA1Func;
            return dupe;
        }

        public ISocketConnector CreateSocketConnector ()
            => SocketConnectorFunc ();
        public Factories WithSocketConnectorCreator(SocketConnectorCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.SocketConnectorFunc = creator ?? Default.SocketConnectorFunc;
            return dupe;
        }

        public IStreamingPieceRequester CreateStreamingPieceRequester ()
            => StreamingPieceRequesterFunc ();
        public Factories WithStreamingPieceRequesterCreator (StreamingPieceRequesterCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.StreamingPieceRequesterFunc = creator ?? Default.StreamingPieceRequesterFunc;
            return dupe;
        }
        new Factories MemberwiseClone ()
            => (Factories) base.MemberwiseClone ();
    }
}
