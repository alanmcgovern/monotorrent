using System;
using System.Collections.Generic;
using System.Text;

using MonoTorrent.Client;

namespace MonoTorrent.Logging
{
    public enum LogType : long
    {
        /// <summary>
        /// Log an event every time an incoming connection is received. The parameters for this log event are: <see cref="Uri"/>
        /// </summary>
        IncomingConnectionEstablished,
        /// <summary>
        /// Log an event every time an outgoing connection is established. The parameters for this log event are: <see cref="Uri"/>
        /// </summary>
        OutgoingConnectionEstablished,
        /// <summary>
        /// Log an event every time a connection is closed. The parameters for this log event are: <see cref="Uri"/> 
        /// </summary>
        ConnectionClosed,
        /// <summary>
        /// Log an event every time an incoming connection is blocked. The parameters for this log event are: <see cref="Uri"/>
        /// </summary>
        ConnectionBlocked,
        /// <summary>
        /// Log an event every time an error occurs while forwarding a port. The parameters for this log event are: <see cref="TorrentManager"/>, <see cref="int"/>
        /// </summary>
        PortForwardingError,
        /// <summary>
        /// Log an event every time a piece fails the hashcheck after being received. The parameters for this log event are: <see cref="ClientEngine"/>, <see cref="Exception"/>
        /// </summary>
        PieceFailedHashCheck,
    }
}
