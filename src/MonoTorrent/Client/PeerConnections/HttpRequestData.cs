using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Connections
{
    public partial class HttpConnection
    {
        private class HttpRequestData
        {
            public readonly RequestMessage Request;
            public readonly int TotalToReceive;
            public bool SentHeader;
            public bool SentLength;
            public int TotalReceived;

            public HttpRequestData(RequestMessage request)
            {
                Request = request;
                var m = new PieceMessage(request.PieceIndex, request.StartOffset, request.RequestLength);
                TotalToReceive = m.ByteLength;
            }

            public bool Complete
            {
                get { return TotalToReceive == TotalReceived; }
            }
        }
    }
}