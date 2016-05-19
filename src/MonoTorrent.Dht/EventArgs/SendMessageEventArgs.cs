#if !DISABLE_DHT
using System.Net;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    internal class SendQueryEventArgs : TaskCompleteEventArgs
    {
        public SendQueryEventArgs(IPEndPoint endpoint, QueryMessage query, ResponseMessage response)
            : base(null)
        {
            EndPoint = endpoint;
            Query = query;
            Response = response;
        }

        public IPEndPoint EndPoint { get; }

        public QueryMessage Query { get; }

        public ResponseMessage Response { get; }

        public bool TimedOut
        {
            get { return Response == null; }
        }
    }
}

#endif