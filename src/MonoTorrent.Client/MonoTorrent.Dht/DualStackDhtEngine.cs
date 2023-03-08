using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;

namespace MonoTorrent.Client
{
    class DualStackDhtEngine : IDualStackDhtEngine
    {
        public IDhtEngine IPv4Dht { get; private set; }
        IDht IDualStackDhtEngine.IPv4Dht => IPv4Dht;

        public IDhtEngine IPv6Dht { get; private set; }
        IDht IDualStackDhtEngine.IPv6Dht => IPv6Dht;

        public TimeSpan MinimumAnnounceInterval => IPv4Dht.MinimumAnnounceInterval;

        public DualStackDhtEngine (Factories factories)
        {
            IPv4Dht = factories.CreateDht (AddressFamily.InterNetwork);
            IPv6Dht = factories.CreateDht (AddressFamily.InterNetworkV6);
        }

        public void Add (IEnumerable<ReadOnlyMemory<byte>> nodes)
        {
            var array = nodes.ToArray ();
            IPv4Dht.Add (array);
            IPv6Dht.Add (array);
        }

        public void Announce (InfoHash infoHash, int port)
        {
            IPv4Dht.Announce (infoHash, port);
            IPv6Dht.Announce (infoHash, port);
        }

        public void Dispose ()
        {
            IPv4Dht.Dispose ();
            IPv6Dht.Dispose ();
        }

        public void GetPeers (InfoHash infoHash)
        {
            IPv4Dht.GetPeers (infoHash);
            IPv6Dht.GetPeers (infoHash);
        }

        public async Task SetListenersAsync (IEnumerable<IDhtListener> listeners)
        {
            var array = listeners.ToArray ();
            var listenersIPv4 = array.Where (t => t.PreferredLocalEndPoint.AddressFamily == AddressFamily.InterNetwork).ToArray ();
            var listenersIPv6 = array.Where (t => t.PreferredLocalEndPoint.AddressFamily == AddressFamily.InterNetworkV6).ToArray ();

            if (listenersIPv4.Length > 1)
                throw new NotSupportedException ($"This array should contain at most one IPv4 based listener. It actually contained {listenersIPv4.Length} IPv4 listeners.");

            if (listenersIPv6.Length > 1)
                throw new NotSupportedException ($"This array should contain at most one IPv6 based listener. It actually contained {listenersIPv6.Length} IPv6 listeners.");

            await IPv4Dht.SetListenerAsync (listenersIPv4.FirstOrDefault () ?? NullDhtListener.IPv4);
            await IPv6Dht.SetListenerAsync (listenersIPv6.FirstOrDefault () ?? NullDhtListener.IPv6);
        }

        public async Task StartAsync (ReadOnlyMemory<byte> initialNodesIPv4, ReadOnlyMemory<byte> initialNodesIPv6)
        {
            await IPv4Dht.StartAsync (initialNodesIPv4);
            await IPv6Dht.StartAsync (initialNodesIPv6);
        }

        public async Task StopAsync ()
        {
            await IPv4Dht.StopAsync ();
            await IPv6Dht.StopAsync ();
        }
    }
}
