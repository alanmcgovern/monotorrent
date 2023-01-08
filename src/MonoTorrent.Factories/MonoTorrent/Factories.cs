//
// Factories.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Connections;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Tracker;
using MonoTorrent.Dht;
using MonoTorrent.PiecePicking;
using MonoTorrent.PieceWriter;
using MonoTorrent.PortForwarding;
using MonoTorrent.Trackers;

namespace MonoTorrent
{
    public partial class Factories
    {
        public delegate IBlockCache BlockCacheCreator (IPieceWriter writer, long capacity, CachePolicy policy, MemoryPool buffer);
        public delegate IDhtEngine DhtCreator ();
        public delegate IDhtListener DhtListenerCreator (IPEndPoint endpoint);
        public delegate HttpClient HttpClientCreator (AddressFamily family);
        public delegate ILocalPeerDiscovery LocalPeerDiscoveryCreator ();
        public delegate IPeerConnection PeerConnectionCreator (Uri uri);
        public delegate IPeerConnectionListener PeerConnectionListenerCreator (IPEndPoint endPoint);
        public delegate IPieceRequester PieceRequesterCreator (PieceRequesterSettings settings);
        public delegate IPieceWriter PieceWriterCreator (int maxOpenFiles);
        public delegate IPortForwarder PortForwarderCreator ();
        public delegate ISocketConnector SocketConnectorCreator ();
        public delegate IStreamingPieceRequester StreamingPieceRequesterCreator ();
        public delegate ITracker TrackerCreator (Uri uri);
    }

    public partial class Factories
    {
        public static Factories Default { get; } = new Factories ();

        BlockCacheCreator BlockCacheFunc { get; set; }
        DhtCreator DhtFunc { get; set; }
        DhtListenerCreator DhtListenerFunc { get; set; }
        LocalPeerDiscoveryCreator LocalPeerDiscoveryFunc { get; set; }
        HttpClientCreator HttpClientFunc { get; set; }
        ReadOnlyDictionary<string, PeerConnectionCreator> PeerConnectionFuncs { get; set; }
        PeerConnectionListenerCreator PeerConnectionListenerFunc { get; set; }
        PieceRequesterCreator PieceRequesterFunc { get; set; }
        PieceWriterCreator PieceWriterFunc { get; set; }
        PortForwarderCreator PortForwarderFunc { get; set; }
        SocketConnectorCreator SocketConnectorFunc { get; set; }
        StreamingPieceRequesterCreator StreamingPieceRequesterFunc { get; set; }

        ReadOnlyDictionary<string, TrackerCreator> TrackerFuncs { get; set; }

        public Factories ()
        {
            BlockCacheFunc = (writer, capacity, policy, buffer) => new MemoryCache (buffer, capacity, policy, writer);
            DhtFunc = () => new DhtEngine ();
            DhtListenerFunc = endpoint => new DhtListener (endpoint);

            HttpClientFunc = HttpRequestFactory.CreateHttpClient;

            LocalPeerDiscoveryFunc = () => new LocalPeerDiscovery ();
            PeerConnectionFuncs = new ReadOnlyDictionary<string, PeerConnectionCreator> (
                new Dictionary<string, PeerConnectionCreator> {
                    { "ipv4", uri => new SocketPeerConnection (uri, new SocketConnector ()) },
                    { "ipv6", uri => new SocketPeerConnection (uri, new SocketConnector ()) },
                }
            );
            PeerConnectionListenerFunc = endPoint => new PeerConnectionListener (endPoint);
            PieceRequesterFunc = settings => new StandardPieceRequester (settings);
            PieceWriterFunc = maxOpenFiles => new DiskWriter (maxOpenFiles);
            PortForwarderFunc = () => new MonoNatPortForwarder ();
            SocketConnectorFunc = () => new SocketConnector ();
            StreamingPieceRequesterFunc = () => new StreamingPieceRequester ();

            // 'Convert' the bespoke delegate to a standard 'Func' delegate
            Func<AddressFamily, HttpClient> httpCreator = t => HttpClientFunc (t);
            TrackerFuncs = new ReadOnlyDictionary<string, TrackerCreator> (
                new Dictionary<string, TrackerCreator> {
                    { "http", uri => new Tracker (new HttpTrackerConnection(uri, httpCreator, AddressFamily.InterNetwork), new HttpTrackerConnection(uri, httpCreator, AddressFamily.InterNetworkV6)) },
                    { "https", uri => new Tracker (new HttpTrackerConnection(uri, httpCreator, AddressFamily.InterNetwork), new HttpTrackerConnection(uri, httpCreator, AddressFamily.InterNetworkV6)) },
                    { "udp", uri => new Tracker (new UdpTrackerConnection (uri, AddressFamily.InterNetwork), new UdpTrackerConnection (uri, AddressFamily.InterNetworkV6)) },
                }
            );
        }

        public IBlockCache CreateBlockCache (IPieceWriter writer, long capacity, CachePolicy policy, MemoryPool buffer)
            => BlockCacheFunc (writer, capacity, policy, buffer);
        public Factories WithBlockCacheCreator (BlockCacheCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.BlockCacheFunc = creator ?? Default.BlockCacheFunc;
            return dupe;
        }

        public IDhtEngine CreateDht ()
            => DhtFunc ();
        public Factories WithDhtCreator (DhtCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.DhtFunc = creator ?? Default.DhtFunc;
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

        public HttpClient CreateHttpClient ()
            => CreateHttpClient (AddressFamily.Unspecified);
        public HttpClient CreateHttpClient (AddressFamily addressFamily)
            => HttpClientFunc (addressFamily);
        public Factories WithHttpClientCreator (HttpClientCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.HttpClientFunc = creator ?? Default.HttpClientFunc;
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

        public IPeerConnection? CreatePeerConnection (Uri uri)
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
            if (creator == null && Default.PeerConnectionFuncs.ContainsKey (scheme))
                creator = Default.PeerConnectionFuncs[scheme];

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
        public Factories WithPeerConnectionListenerCreator (PeerConnectionListenerCreator creator)
        {
            var dupe = MemberwiseClone ();
            dupe.PeerConnectionListenerFunc = creator ?? Default.PeerConnectionListenerFunc;
            return dupe;
        }

        public IPieceRequester CreatePieceRequester ()
            => CreatePieceRequester (PieceRequesterSettings.Default);
        public IPieceRequester CreatePieceRequester (PieceRequesterSettings settings)
            => PieceRequesterFunc (settings);
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

        public ISocketConnector CreateSocketConnector ()
            => SocketConnectorFunc ();
        public Factories WithSocketConnectorCreator (SocketConnectorCreator creator)
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

        public ITracker? CreateTracker (Uri uri)
        {
            try {
                if (TrackerFuncs.TryGetValue (uri.Scheme, out var creator))
                    return creator (uri);
            } catch {

            }
            return null;
        }
        public Factories WithTrackerCreator (string scheme, TrackerCreator creator)
        {
            var dict = new Dictionary<string, TrackerCreator> (TrackerFuncs);
            if (creator == null && Default.TrackerFuncs.ContainsKey (scheme))
                creator = Default.TrackerFuncs[scheme];

            if (creator == null)
                dict.Remove (scheme);
            else
                dict[scheme] = creator;

            var dupe = MemberwiseClone ();
            dupe.TrackerFuncs = new ReadOnlyDictionary<string, TrackerCreator> (dict);
            return dupe;
        }
        new Factories MemberwiseClone ()
            => (Factories) base.MemberwiseClone ();
    }
}
