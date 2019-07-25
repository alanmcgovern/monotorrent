using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;
using System.Net;

namespace MonoTorrent.Dht
{
    struct SendQueryEventArgs
    {
        public IPEndPoint EndPoint { get; }
        public ErrorMessage Error { get; }
        public Node Node { get; }
        public QueryMessage Query { get; }
        public ResponseMessage Response { get; }
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
