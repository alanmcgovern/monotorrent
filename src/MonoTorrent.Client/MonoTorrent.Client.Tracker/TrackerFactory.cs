//
// TrackerFactory.cs
//
// Authors:
//   Eric Butler eric@extremeboredom.net
//   Alan McGovern alan.mcgovern@gmail.com
// Copyright (C) 2007 Eric Butler
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

namespace MonoTorrent.Client.Tracker
{
    public static class TrackerFactory
    {
        static readonly Dictionary<string, Func<Uri, ITracker>> trackerTypes = new Dictionary<string, Func<Uri, ITracker>> {
            { "udp", uri => new UdpTracker (uri) },
            { "http", uri => new HTTPTracker (uri) },
            { "https", uri => new HTTPTracker (uri) },
        };

        public static void Register (string protocol, Type trackerType)
        {
            if (string.IsNullOrEmpty (protocol))
                throw new ArgumentException ("cannot be null or empty", protocol);
            if (trackerType == null)
                throw new ArgumentNullException (nameof (trackerType));

            Register (protocol, uri => (ITracker) Activator.CreateInstance (trackerType, uri));
        }

        public static void Register (string protocol, Func<Uri, ITracker> creator)
        {
            lock (trackerTypes)
                trackerTypes[protocol] = creator;
        }

        public static ITracker Create (Uri uri)
        {
            Check.Uri (uri);

            try {
                lock (trackerTypes) {
                    if (trackerTypes.TryGetValue (uri.Scheme, out Func<Uri, ITracker> creator))
                        return creator (uri);
                    return null;
                }
            } catch {
                return null; // Invalid tracker
            }
        }
    }
}
