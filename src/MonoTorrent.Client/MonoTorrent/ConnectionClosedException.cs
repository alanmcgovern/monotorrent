using System;

namespace MonoTorrent
{
    class ConnectionClosedException : Exception
    {
        public ConnectionClosedException (string message)
            : base (message)
        {
        }
    }
}
