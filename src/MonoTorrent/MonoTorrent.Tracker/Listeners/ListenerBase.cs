//
// ListenerBase.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Specialized;
using System.Net;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Tracker.Listeners
{
    abstract class ListenerBase : IListener
    {
        #region Events

        public event EventHandler<ScrapeParameters> ScrapeReceived;
        public event EventHandler<AnnounceParameters> AnnounceReceived;

        #endregion Events

        #region Properties

        public abstract bool Running { get; }

        #endregion Properties


        #region Methods

        public virtual BEncodedDictionary Handle(string queryString, IPAddress remoteAddress, bool isScrape)
        {
            if (queryString == null)
                throw new ArgumentNullException("queryString");

            return Handle(ParseQuery(queryString), remoteAddress, isScrape);
        }

        public virtual BEncodedDictionary Handle(NameValueCollection collection, IPAddress remoteAddress, bool isScrape)
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
