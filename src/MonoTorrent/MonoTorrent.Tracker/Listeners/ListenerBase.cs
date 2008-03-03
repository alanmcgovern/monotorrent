using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.BEncoding;
using System.Collections.Specialized;
using System.Net;
using System.Threading;

namespace MonoTorrent.Tracker.Listeners
{
    public abstract class ListenerBase
    {
        #region Events

        public event EventHandler<ScrapeParameters> ScrapeReceived;
        public event EventHandler<AnnounceParameters> AnnounceReceived;

        #endregion Events

        #region Member Variables
        
        private Tracker tracker;

        #endregion Member Variables

        #region Properties

        public abstract bool Running { get; }

        internal Tracker Tracker
        {
            get { return tracker; }
            set { tracker = value; }
        }

        #endregion Properties


        #region Methods

        public virtual BEncodedValue Handle(string queryString, IPAddress remoteAddress, bool isScrape)
        {
            if (queryString == null)
                throw new ArgumentNullException("queryString");

            return Handle(ParseQuery(queryString), remoteAddress, isScrape);
        }

        public virtual BEncodedValue Handle(NameValueCollection collection, IPAddress remoteAddress, bool isScrape)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            if (remoteAddress == null)
                throw new ArgumentNullException("remoteAddress");

            RequestParameters parameters;
            if (isScrape)
                parameters = new ScrapeParameters(collection, remoteAddress);
            else
                parameters = new AnnounceParameters(collection, remoteAddress);

            // If the parameters are invalid, the failure reason will be added to the response dictionary
            if (!parameters.IsValid)
                return parameters.Response;

            // Fire the necessary event so the request will be handled and response filled in
            if (isScrape)
                RaiseScrapeReceived((ScrapeParameters)parameters);
            else
                RaiseAnnounceReceived((AnnounceParameters)parameters);

            // Return the response now that the connection has been handled correctly.
            return parameters.Response;
        }

        private NameValueCollection ParseQuery(string url)
        {
            // The '?' symbol will be there if we received the entire URL as opposed to
            // just the query string - we accept both therfore trim out the excess if we have the entire URL
            if (url.IndexOf('?') != -1)
                url = url.Substring(url.IndexOf('?') + 1);

            string[] parts = url.Split('&', '=');
            NameValueCollection c = new NameValueCollection(1 + parts.Length / 2);
            for (int i = 0; i < parts.Length; i += 2)
                if (parts.Length > i + 1)
                    c.Add(parts[i], parts[i + 1]);

            return c;
        }

        private void RaiseAnnounceReceived(AnnounceParameters e)
        {
            EventHandler<AnnounceParameters> h = AnnounceReceived;
            if (h != null)
                h(this, e);
        }

        private void RaiseScrapeReceived(ScrapeParameters e)
        {
            EventHandler<ScrapeParameters> h = ScrapeReceived;
            if (h != null)
                h(this, e);
        }

        public abstract void Start();

        public abstract void Stop();

        #endregion Methods

    }
}
