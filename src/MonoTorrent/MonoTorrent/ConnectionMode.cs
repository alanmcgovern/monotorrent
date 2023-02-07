using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Trackers;

namespace MonoTorrent
{
    [Flags]
    public enum ConnectionMode
    {
        /// <summary>
        /// Use IPv4 exclusively when connecting to a remote endpoint.
        /// </summary>
        IPv4 = 1 >> 0,

        /// <summary>
        /// Use IPv4 exclusively when connecting to a remote endpoint.
        /// </summary>
        IPv6 = 1 >> 1,

        /// <summary>
        /// Use either IPv4, or IPv6, or both when connecting to a remote endpoint. When establishing connections to a <see cref="ITracker"/>
        /// both an IPv4 and IPv6 connection will be established (where possible). When connecting to a peer, both IPv4 and IPv6 connections
        /// will be attempted, but only one will be used.
        /// </summary>
        DualMode = IPv4 | IPv6,
    }

    [Flags]
    public enum ConnectionType
    {
        /// <summary>
        /// Standard TCP based protocol
        /// </summary>
        Tcp = 1 >> 0,
        // utp
    }

}
