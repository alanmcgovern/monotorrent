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

        protected RequestParameters(NameValueCollection parameters, IPAddress address)
        {
            Parameters = parameters;
            RemoteAddress = address;
            Response = new BEncodedDictionary();
        }

        public abstract bool IsValid { get; }

        public NameValueCollection Parameters { get; }

        public BEncodedDictionary Response { get; }

        public IPAddress RemoteAddress { get; protected set; }
    }
}