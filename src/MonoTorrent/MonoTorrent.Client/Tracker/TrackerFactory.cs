//
// TrackerFactory.cs
//
// Authors:
//   Eric Butler eric@extremeboredom.net
//
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
using System.Net;
using System.Threading;
using MonoTorrent.Common;
using System.Collections.Generic;

namespace MonoTorrent.Client.Tracker
{
    public static class TrackerFactory
    {
        static Dictionary<string, Type> trackerTypes = new Dictionary<string, Type>();

        public static void Register(string protocol, Type trackerType)
        {
            if (string.IsNullOrEmpty(protocol))
                throw new ArgumentException("cannot be null or empty", protocol);

            if (trackerType == null)
                throw new ArgumentNullException("trackerType");

            lock (trackerTypes)
                trackerTypes.Add(protocol, trackerType);
        }

        public static Tracker Create(string protocol, Uri announceUrl)
        {
            if (string.IsNullOrEmpty(protocol))
                throw new ArgumentException("cannot be null or empty", "protocol");

            if (announceUrl == null)
                throw new ArgumentNullException("announceUrl");

            if (!trackerTypes.ContainsKey(protocol))
                return null;

            return (Tracker)Activator.CreateInstance(trackerTypes[protocol], announceUrl);
        }
    }
}
