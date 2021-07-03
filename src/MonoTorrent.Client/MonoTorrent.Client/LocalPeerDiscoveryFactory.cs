using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    static class LocalPeerDiscoveryFactory
    {
        public static Func<int, ILocalPeerDiscovery> Creator = port => new LocalPeerDiscovery (port);

        public static ILocalPeerDiscovery Create (int port)
        {
            if (port == -1)
                return new NullLocalPeerDiscovery ();

            return Creator (port);
        }
    }
}
