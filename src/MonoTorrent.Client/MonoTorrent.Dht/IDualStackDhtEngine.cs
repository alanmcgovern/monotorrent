using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;

namespace MonoTorrent.Client
{
    public interface IDualStackDhtEngine
    {
        IDht IPv4Dht { get; }
        IDht IPv6Dht { get; }
    }
}
