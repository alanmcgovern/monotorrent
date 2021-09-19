using System;

using MonoTorrent.Dht.Listeners;

namespace MonoTorrent.Dht
{
    static class DhtEngineFactory
    {
        public static Func<IDhtListener, IDhtEngine> Creator = listener => new DhtEngine (listener);

        public static IDhtEngine Create (IDhtListener listener)
            => Creator (listener);
    }
}
