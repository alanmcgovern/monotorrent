//
// InternalHttpServer.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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
using System.IO;
using System.Net;
using System.Web;
using System.Text;
using System.Collections.Specialized;
using System.Diagnostics;

using MonoTorrent.Common;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Tracker
{
    public class HttpListener : ListenerBase
    {
        #region Fields

        private IPEndPoint endpoint;
        private System.Net.HttpListener listener;

        #endregion Fields


        #region Properties

        /// <summary>
        /// True if the listener is waiting for incoming connections
        /// </summary>
        public override bool Running
        {
            get { return listener.IsListening; }
        }

        #endregion Properties


        #region Constructors

        public HttpListener(IPAddress address, int port)
            : this(new IPEndPoint(address, port))
        {

        }

        public HttpListener(IPEndPoint endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            listener = new System.Net.HttpListener();
            this.endpoint = endpoint;
        }

        #endregion Constructors


        #region Methods

        /// <summary>
        /// Starts listening for incoming connections
        /// </summary>
        public override void Start()
        {
            listener.Prefixes.Add(string.Format("http://{0}:{1}/", endpoint.Address.ToString(), endpoint.Port));
            listener.Start();
            listener.BeginGetContext(EndGetRequest, null);

        }

        /// <summary>
        /// Stops listening for incoming connections
        /// </summary>
        public override void Stop()
        {
            listener.Stop();
        }

        private void EndGetRequest(IAsyncResult result)
        {
            HttpListenerContext context;
            context = listener.EndGetContext(result);
            HandleRequest(context);
            context.Response.Close();
            listener.BeginGetContext(EndGetRequest, null);
        }

        private void HandleRequest(HttpListenerContext context)
        {
            bool isScrape = context.Request.RawUrl.StartsWith("/scrape", StringComparison.OrdinalIgnoreCase);

            BEncodedValue responseData = Handle(context.Request.RawUrl, context.Request.RemoteEndPoint.Address, isScrape);

            byte[] response = responseData.Encode();
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 200;
            context.Response.OutputStream.Write(response, 0, response.Length);
        }

        #endregion Methods
    }
}
