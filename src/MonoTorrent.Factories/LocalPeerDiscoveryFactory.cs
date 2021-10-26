using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public static class LocalPeerDiscoveryFactory
    {
        static Func<int, ILocalPeerDiscovery> Creator = port => new LocalPeerDiscovery (port);

        public static void Register (Func<int, ILocalPeerDiscovery> creator)
            => Creator = creator ?? throw new ArgumentNullException (nameof (creator));

        public static ILocalPeerDiscovery Create (int port)
        {
            if (port == -1)
                return null;

            return Creator (port);
        }
    }
}
