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
        public const string FailureKey = "failure reason";
        public const string WarningKey = "warning"; //FIXME: Check this, i know it's wrong!

        private IPAddress remoteAddress;
        private bool handled;
        private NameValueCollection parameters;
        private BEncodedDictionary response;

        private bool Handled
        {
            get { return handled; }
            set { handled = value; }
        }

        /// <summary>
        /// True if the request is properly formed and valid
        /// </summary>
        public abstract bool IsValid { get; }
        
        /// <summary>
        /// The parameters from the original query
        /// </summary>
        public NameValueCollection Parameters
        {
            get { return parameters; }
        }

        /// <summary>
        /// The response which will be returned to the peer
        /// </summary>
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
