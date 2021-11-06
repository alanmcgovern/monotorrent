using System;

using MonoTorrent.Client;

namespace MonoTorrent.Dht
{
    static class DhtEngineFactory
    {
        public static Func<Factories, IDhtEngine> Creator = (factories) => new DhtEngine (factories);

        public static IDhtEngine Create (Factories factories)
            => Creator (factories);
    }
}
