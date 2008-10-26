using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Specialized;
using MonoTorrent.BEncoding;
using System.Net;

namespace MonoTorrent.Tracker
{
    public abstract class RequestParameters : EventArgs
    {
        protected internal static readonly string FailureKey = "failure reason";
        protected internal static readonly string WarningKey = "warning"; //FIXME: Check this, i know it's wrong!

        private IPAddress remoteAddress;
        private NameValueCollection parameters;
        private BEncodedDictionary response;

        public abstract bool IsValid { get; }
        
        public NameValueCollection Parameters
        {
            get { return parameters; }
        }

        public BEncodedDictionary Response
        {
            get { return response; }
        }

        public IPAddress RemoteAddress
        {
            get { return remoteAddress; }
            protected set { remoteAddress = value; }
        }

        protected RequestParameters(NameValueCollection parameters, IPAddress address)
        {
            this.parameters = parameters;
            remoteAddress = address;
            response = new BEncodedDictionary();
        }
    }
}
