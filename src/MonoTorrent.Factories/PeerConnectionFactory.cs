//
// ConnectionFactory.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Net.Sockets;

using MonoTorrent.Connections;

namespace MonoTorrent.Client.Connections
{
    public static class PeerConnectionFactory
    {
        static readonly Dictionary<string, Func<Uri, IPeerConnection>> connectionCreator = new Dictionary<string, Func<Uri, IPeerConnection>> {
            { "ipv4", uri => new SocketPeerConnection (uri, SocketConnectorFactory.Create ()) },
            { "ipv6", uri => new SocketPeerConnection (uri, SocketConnectorFactory.Create ()) }
        };

        /// <summary>
        /// Registers a function to create an IConnection for a particular peer connection scheme.
        /// Typically this is one of the constants defined in <see cref="ConnectionType"/>, representing standard
        /// ipv4, ipv6 or HTTP(s) connections. 
        /// </summary>
        /// <param name="scheme">The scheme associated with the connection</param>
        /// <param name="creator">The delegate which will be invoked to create the connection</param>
        public static void Register (string scheme, Func<Uri, IPeerConnection> creator)
        {
            lock (connectionCreator)
                connectionCreator[scheme] = creator;
        }

        public static IPeerConnection Create (Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException (nameof (uri));

            if (uri.Port == 0)
                return null;

            Func<Uri, IPeerConnection> creator;
            lock (connectionCreator)
                if (!connectionCreator.TryGetValue (uri.Scheme, out creator))
                    return null;

            try {
                return creator (uri);
            } catch {
                return null;
            }
        }
    }
}
