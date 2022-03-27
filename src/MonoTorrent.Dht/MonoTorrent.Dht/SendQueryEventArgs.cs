//
// SendQueryEventArgs.cs
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

using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    struct SendQueryEventArgs
    {
        public IPEndPoint EndPoint { get; }
        public ErrorMessage? Error { get; }
        public Node Node { get; }
        public QueryMessage Query { get; }
        public ResponseMessage? Response { get; }
        public bool TimedOut => Response == null && Error == null;

        public SendQueryEventArgs (Node node, IPEndPoint endpoint, QueryMessage query)
        {
            EndPoint = endpoint;
            Error = null;
            Node = node;
            Query = query;
            Response = null;
        }

        public SendQueryEventArgs (Node node, IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
        {
            EndPoint = endpoint;
            Error = null;
            Node = node;
            Query = query;
            Response = response;
        }

        public SendQueryEventArgs (Node node, IPEndPoint endpoint, QueryMessage query, ErrorMessage error)
        {
            EndPoint = endpoint;
            Error = error;
            Node = node;
            Query = query;
            Response = null;
        }
    }
}
