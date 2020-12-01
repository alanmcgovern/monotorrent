//
// PeerListenerFactory.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2019 Alan McGovern
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


using System.Net;

namespace MonoTorrent.Client.Listeners
{
    public static class PeerListenerFactory
    {
        /// <summary>
        /// Creates a listener which binds to IPAddress.Any and listens for incoming TCP requests on the given local port.
        /// </summary>
        /// <param name="port">The local port to bind to.</param>
        /// <returns></returns>
        public static IPeerListener CreateTcp (int port)
        {
            return CreateTcp (IPAddress.Any, port);
        }

        /// <summary>
        /// Creates a listener which listens for incoming TCP requests on the given local IP address and port.
        /// </summary>
        /// <param name="address">The local IP address to bind to.</param>
        /// <param name="port">The local port to bind to.</param>
        /// <returns></returns>
        public static IPeerListener CreateTcp (IPAddress address, int port)
        {
            return CreateTcp (new IPEndPoint (address, port));
        }

        /// <summary>
        /// Creates a listener which listens for incoming TCP requests on the given local IP address and port.
        /// </summary>
        /// <param name="endpoint">The local endpoint to bind to.</param>
        /// <returns></returns>
        public static IPeerListener CreateTcp (IPEndPoint endpoint)
        {
            return new PeerListener (endpoint);
        }
    }
}
