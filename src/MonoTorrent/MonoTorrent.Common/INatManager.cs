using System;
using System.Net.Sockets;

namespace MonoTorrent.Common
{
    public interface INatManager
    {
        void Open(ProtocolType protocol, int port);
        void Close();
    }
}
