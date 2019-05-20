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
        public QueryMessage Query { get; }
        public ResponseMessage Response { get; }
        public bool TimedOut => Response == null;

        public SendQueryEventArgs (IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
        {
            EndPoint = endpoint;
            Query = query;
            Response = response;
        }
    }
}
