//
// GetPeersResponse.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2008 Alan McGovern
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
using System.Text;

using MonoTorrent.BEncoding;
using System.Net;

namespace MonoTorrent.Dht.Messages
{
    class GetPeersResponse : ResponseMessage
    {
        private static readonly BEncodedString TokenKey = "token";
        private static readonly BEncodedString ValuesKey = "values";

        public BEncodedString Token
        {
            get { return (BEncodedString)Parameters[TokenKey]; }
            set { Parameters[TokenKey] = value; }
        }

        public BEncodedList Values
        {
            get { return (BEncodedList)Parameters[ValuesKey]; }
        }

        public GetPeersResponse(NodeId id, BEncodedString token)
            :base (id)
        {
            Parameters.Add(TokenKey, token);
            Parameters.Add(ValuesKey, new BEncodedList());
        }

        public GetPeersResponse(BEncodedDictionary d)
            : base(d)
        {

        }

        public override bool Handle(DhtEngine engine, IPEndPoint source)
        {
            if (!base.Handle(engine, source))
                return false;

            // FIXME: Do the rest
            return true;
        }
    }
}
