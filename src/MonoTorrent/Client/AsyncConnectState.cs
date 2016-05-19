using MonoTorrent.Client.Connections;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal static partial class NetworkIO
    {
        private class AsyncConnectState : ICacheable
        {
            public IConnection Connection { get; private set; }

            public AsyncIOCallback Callback { get; private set; }

            public object State { get; private set; }

            public void Initialise()
            {
                Initialise(null, null, null);
            }

            public AsyncConnectState Initialise(IConnection connection, AsyncIOCallback callback, object state)
            {
                Connection = connection;
                Callback = callback;
                State = state;
                return this;
            }
        }
    }
}