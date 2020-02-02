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

namespace MonoTorrent.Client.Connections
{
    public static class ConnectionFactory
    {
        static readonly object locker = new object ();
        static readonly Dictionary<string, Func<Uri, IConnection>> trackerTypes = new Dictionary<string, Func<Uri, IConnection>> ();

        static ConnectionFactory ()
        {
            RegisterTypeForProtocol ("ipv4", uri => new IPV4Connection (uri));
            RegisterTypeForProtocol ("ipv6", uri => new IPV6Connection (uri));
            RegisterTypeForProtocol ("http", uri => new HttpConnection (uri));
            RegisterTypeForProtocol ("https", uri => new HttpConnection (uri));
        }

        public static void RegisterTypeForProtocol (string protocol, Type connectionType)
        {
            if (string.IsNullOrEmpty (protocol))
                throw new ArgumentException ("cannot be null or empty", nameof (protocol));
            if (connectionType == null)
                throw new ArgumentNullException (nameof (connectionType));

            RegisterTypeForProtocol (protocol, uri => (IConnection) Activator.CreateInstance (connectionType, uri));
        }

        static void RegisterTypeForProtocol (string protocol, Func<Uri, IConnection> creator)
        {
            lock (locker)
                trackerTypes[protocol] = creator;
        }


        public static IConnection Create (Uri connectionUri)
        {
            if (connectionUri == null)
                throw new ArgumentNullException (nameof (connectionUri));

            if (connectionUri.Scheme == "ipv4" && connectionUri.Port == -1)
                return null;

            Func<Uri, IConnection> creator;
            lock (locker)
                if (!trackerTypes.TryGetValue (connectionUri.Scheme, out creator))
                    return null;

            try {
                return creator (connectionUri);
            } catch {
                return null;
            }
        }
    }
}
