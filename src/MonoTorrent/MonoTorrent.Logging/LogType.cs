using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Logging
{
    public enum LogType : long
    {
        /// <summary>
        /// Log an event every time an incoming connection is received.
        /// </summary>
        IncomingConnectionEstablished,
        /// <summary>
        /// Log an event every time an outgoing connection is established.
        /// </summary>
        OutgoingConnectionEstablished,
        /// <summary>
        /// Log an event every time a connection is closed.
        /// </summary>
        ConnectionClosed,
        /// <summary>
        /// Log an event every time an incoming connection is blocked.
        /// </summary>
        ConnectionBlocked,
        /// <summary>
        /// Log an event every time an error occurs while forwarding a port.
        /// </summary>
        PortForwardingError,
        /// <summary>
        /// Log an event every time a piece fails the hashcheck after being received.
        /// </summary>
        PieceFailedHashCheck,
    }
}
