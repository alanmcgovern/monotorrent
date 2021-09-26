using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.Connections
{
    public static class SocketConnectorFactory
    {
        static ISocketConnector DefaultInstance = new SocketConnector ();
        static Func<ISocketConnector> Creator = () => DefaultInstance;

        public static void Register (Func<ISocketConnector> creator)
            => Creator = creator ?? throw new ArgumentNullException (nameof (creator));

        public static ISocketConnector Create ()
            => Creator ();
    }
}
