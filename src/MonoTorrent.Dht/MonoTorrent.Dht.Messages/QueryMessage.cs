//
// QueryMessage.cs
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


using System.Net.Sockets;
using System.Runtime.CompilerServices;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Messages
{
    abstract class QueryMessage : DhtMessage
    {
        static readonly BEncodedString QueryArgumentsKey = new BEncodedString ("a");
        static readonly BEncodedString QueryNameKey = new BEncodedString ("q");
        internal static readonly BEncodedString QueryType = new BEncodedString ("q");

        internal override NodeId Id => new NodeId ((BEncodedString) Parameters[IdKey]);

        protected BEncodedDictionary Parameters => (BEncodedDictionary) properties[QueryArgumentsKey];

        protected QueryMessage (AddressFamily addressFamily, NodeId id, BEncodedString queryName)
            : this (addressFamily, id, queryName, new BEncodedDictionary ())
        {

        }

        protected QueryMessage (AddressFamily addressFamily, NodeId id, BEncodedString queryName, BEncodedDictionary queryArguments)
            : base (addressFamily, QueryType)
        {
            properties.Add (QueryNameKey, queryName);
            properties.Add (QueryArgumentsKey, queryArguments);

            Parameters.Add (IdKey, BEncodedString.FromMemory (id.AsMemory ()));
        }

        protected QueryMessage (AddressFamily addressFamily, BEncodedDictionary d)
            : base (addressFamily, d)
        {
        }

        public abstract ResponseMessage CreateResponse (BEncodedDictionary parameters);
    }
}
