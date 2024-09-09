using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    interface IPeerExchangeSource
    {
        TorrentSettings Settings { get; }
    }
}
