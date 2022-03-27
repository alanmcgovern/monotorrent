//
// TrackerRequest.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
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
using System.Collections.Specialized;
using System.Net;

using MonoTorrent.BEncoding;

namespace MonoTorrent.TrackerServer
{
    public abstract class TrackerRequest : EventArgs
    {
        public static readonly BEncodedString FailureKey = new BEncodedString ("failure reason");
        public static readonly BEncodedString WarningKey = new BEncodedString ("warning message");

        public abstract bool IsValid { get; }

        /// <summary>
        /// The raw (url-encoded) key/value pairs from the original query string
        /// </summary>
        public NameValueCollection Parameters { get; }

        /// <summary>
        /// The BEncodedDictionary which will be sent back to the client who initiated this request
        /// </summary>
        public BEncodedDictionary Response { get; }

        /// <summary>
        /// The IPAddress for the remote client who initiated this request.
        /// </summary>
        public IPAddress RemoteAddress { get; protected set; }

        protected TrackerRequest (NameValueCollection parameters, IPAddress remoteAddress)
        {
            Parameters = parameters;
            RemoteAddress = remoteAddress;
            Response = new BEncodedDictionary ();
        }
    }
}
