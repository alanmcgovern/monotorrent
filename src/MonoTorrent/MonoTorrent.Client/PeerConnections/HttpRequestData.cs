using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Connections
{
    public partial class HttpConnection
    {
        private class HttpRequestData
        {
            public RequestMessage Request;
            public bool SentLength;
            public bool SentHeader;
            public int TotalToReceive;
            public int TotalReceived;

            public bool Complete
            {
                get { return TotalToReceive == TotalReceived; }
            }

            public HttpRequestData(RequestMessage request)
            {
                Request = request;
                PieceMessage m = new PieceMessage(null, request.PieceIndex, request.StartOffset, request.RequestLength);
                TotalToReceive = m.ByteLength;
            }
        }
    }
}
