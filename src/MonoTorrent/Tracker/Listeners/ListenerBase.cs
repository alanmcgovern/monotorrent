using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.BEncoding;
using System.Collections.Specialized;
using System.Net;
using System.Threading;

namespace MonoTorrent.Tracker
{
    internal class RequestAsyncResult : IAsyncResult
    {
        private object asyncState;
        private bool isComplete;
        private AsyncCallback callback;
        private ManualResetEvent waitHandle;
        private BEncodedDictionary response;

        public RequestAsyncResult(AsyncCallback callback, object asyncState)
        {
            this.asyncState = asyncState;
            this.callback = callback;
        }

        public object AsyncState
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { return waitHandle; }
        }

        public bool CompletedSynchronously
        {
            get { return false; }
        }

        public bool IsCompleted
        {
            get { return isComplete; }
            internal set { isComplete = value; }
        }

        public BEncodedDictionary Response
        {
            get { return response; }
        }
    }

    public abstract class ListenerBase
    {
        private delegate void HandleDelegate(NameValueCollection collection, IPAddress remoteAddress);

        public event EventHandler<ScrapeParameters> ScrapeReceived;
        public event EventHandler<AnnounceParameters> AnnounceReceived;

        /// <summary>
        /// True if the listener is actively listening for incoming connections
        /// </summary>
        public abstract bool Running { get; }

        /// <summary>
        /// Starts listening for incoming connections
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Stops listening for incoming connections
        /// </summary>
        public abstract void Stop();

        /*
        public virtual IAsyncResult BeginHandle(string queryString, IPAddress remoteAddress, AsyncCallback callback, object asyncData)
        {
            return BeginHandle(ParseQuery(queryString), remoteAddress, callback, asyncData);
        }

        public virtual IAsyncResult BeginHandle(NameValueCollection collection, IPAddress remoteAddres, AsyncCallback callback, object asyncData)
        {

        }

        public virtual BEncodedDictionary EndHandle(IAsyncResult result)
        {
            
        }
        */
        protected virtual BEncodedValue Handle(string queryString, IPAddress remoteAddress, bool isScrape)
        {
            return Handle(ParseQuery(queryString), remoteAddress, isScrape);
        }

        protected virtual BEncodedValue Handle(NameValueCollection collection, IPAddress remoteAddress, bool isScrape)
        {
            RequestParameters parameters;
            if (isScrape)
                parameters = new ScrapeParameters(collection, remoteAddress);
            else
                parameters = new AnnounceParameters(collection, remoteAddress);

            // If the parameters are invalid, the failure reason will be added to the response dictionary
            if (!parameters.IsValid)
            {
                return parameters.Response;
            }
            else
            {
                // Otherwise, fire the necessary event and the request will be handled and the response filled in
                if (isScrape)
                    RaiseScrapeReceived((ScrapeParameters)parameters);
                else
                    RaiseAnnounceReceived((AnnounceParameters)parameters);
            }
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
    }
}
