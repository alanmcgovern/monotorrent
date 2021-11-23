//
// ListenerFactory.cs
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

namespace MonoTorrent.Connections.TrackerServer
{
    public static class TrackerListenerFactory
    {
        /// <summary>
        /// Creates a listener to receive incoming HTTP requests on IPAddress.Any and the given port.
        /// The resulting HTTP prefix will be similar to http://{address}:{port}/announce/ and will support Scrape requests.
        /// </summary>
        /// <param name="port">The local port to bind to.</param>
        /// <returns></returns>
        public static ITrackerListener CreateHttp (int port)
        {
            return new HttpTrackerListener (IPAddress.Any, port);
        }

        /// <summary>
        /// Creates a listener to receive incoming HTTP requests on the given local IP address and port.
        /// The resulting HTTP prefix will be similar to http://{address}:{port}/announce/ and will support Scrape requests.
        /// </summary>
        /// <param name="address">The local IP address to bind to.</param>
        /// <param name="port">The local port to bind to.</param>
        /// <returns></returns>
        public static ITrackerListener CreateHttp (IPAddress address, int port)
        {
            return new HttpTrackerListener (address, port);
        }

        /// <summary>
        /// Creates a listener to receive incoming HTTP requests on the given local endpoint.
        /// The resulting HTTP prefix will be similar to http://{address}:{port}/announce/ and will support Scrape requests.
        /// </summary>
        /// <param name="endpoint">The local endpoint to bind to.</param>
        /// <returns></returns>
        public static ITrackerListener CreateHttp (IPEndPoint endpoint)
        {
            return new HttpTrackerListener (endpoint);
        }

        /// <summary>
        /// Creates a listener to receive incoming HTTP requests on the given HTTP prefix. If
        /// the prefix ends in '/announce/' it will support Scrape requests, otherwise scraping will be disabled.
        /// The prefix should be in the form http://{address}:{port}/test/query/announce/
        /// </summary>
        /// <param name="httpPrefix">The HTTP prefix to bind to.</param>
        /// <returns></returns>
        public static ITrackerListener CreateHttp (string httpPrefix)
        {
            return new HttpTrackerListener (httpPrefix);
        }

        /// <summary>
        /// Creates a listener which binds to IPAddress.Any and listens for incoming UDP requests on the given local port.
        /// </summary>
        /// <param name="port">The local port to bind to.</param>
        /// <returns></returns>
        public static ITrackerListener CreateUdp (int port)
        {
            return new UdpTrackerListener (port);
        }

        /// <summary>
        /// Creates a listener which listens for incoming UDP requests on the given local IP address and port.
        /// </summary>
        /// <param name="address">The local IP address to bind to.</param>
        /// <param name="port">The local port to bind to.</param>
        /// <returns></returns>
        public static ITrackerListener CreateUdp (IPAddress address, int port)
        {
            return new UdpTrackerListener (new IPEndPoint (address, port));
        }

        /// <summary>
        /// Creates a listener which listens for incoming UDP requests on the given local IP address and port.
        /// </summary>
        /// <param name="endpoint">The local endpoint to bind to.</param>
        /// <returns></returns>
        public static ITrackerListener CreateUdp (IPEndPoint endpoint)
        {
            return new UdpTrackerListener (endpoint);
        }
    }
}
