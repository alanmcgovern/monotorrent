//
// RequestParameters.cs
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

namespace MonoTorrent.Tracker
{
    public abstract class RequestParameters : EventArgs
    {
        protected internal static readonly string FailureKey = "failure reason";
        protected internal static readonly string WarningKey = "warning message";

        private IPAddress remoteAddress;
        private NameValueCollection parameters;
        private BEncodedDictionary response;

        public abstract bool IsValid { get; }
        
        public NameValueCollection Parameters
        {
            get { return parameters; }
        }

        public BEncodedDictionary Response
        {
            get { return response; }
        }

        public IPAddress RemoteAddress
        {
            get { return remoteAddress; }
            protected set { remoteAddress = value; }
        }

        protected RequestParameters(NameValueCollection parameters, IPAddress address)
        {
            this.parameters = parameters;
            remoteAddress = address;
            response = new BEncodedDictionary();
        }
    }
}
