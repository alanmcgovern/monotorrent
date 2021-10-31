using System;

using MonoTorrent.Client;
using MonoTorrent.Dht.Listeners;

namespace MonoTorrent.Dht
{
    static class DhtEngineFactory
    {
        public static Func<IDhtListener, Factories, IDhtEngine> Creator = (listener, factories) => new DhtEngine (listener, factories);

        public static IDhtEngine Create (IDhtListener listener, Factories factories)
            => Creator (listener, factories);
    }
}
