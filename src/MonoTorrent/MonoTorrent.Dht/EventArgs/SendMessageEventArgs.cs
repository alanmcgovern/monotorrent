using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;
using System.Net;

namespace MonoTorrent.Dht
{
    class SendQueryEventArgs : TaskCompleteEventArgs
    {
        private IPEndPoint endpoint;
        private QueryMessage query;
        private ResponseMessage response;

        public IPEndPoint EndPoint
        {
            get { return endpoint; }
        }

        public QueryMessage Query
        {
            get { return query; }
        }

        public ResponseMessage Response
        {
            get { return response; }
        }

        public bool TimedOut
        {
            get { return response == null; }
        }

        public SendQueryEventArgs(IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
            : base(null)
        {
            this.endpoint = endpoint;
            this.query = query;
            this.response = response;
        }
    }
}
