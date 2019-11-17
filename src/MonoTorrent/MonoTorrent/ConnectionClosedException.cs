using System;
using System.Collections.Generic;
using System.Text;

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
