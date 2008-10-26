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

namespace MonoTorrent.Tracker.Listeners
{
    public class HttpListener : ListenerBase
    {
        #region Fields

        private string prefix;
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
            : this(string.Format("http://{0}:{1}/", address, port))
        {

        }

        public HttpListener(IPEndPoint endpoint)
            : this(endpoint.Address, endpoint.Port)
        {

        }

        public HttpListener(string httpPrefix)
        {
            if (string.IsNullOrEmpty(httpPrefix))
                throw new ArgumentNullException("httpPrefix");

            this.prefix = httpPrefix;
        }

        #endregion Constructors


        #region Methods

        /// <summary>
        /// Starts listening for incoming connections
        /// </summary>
        public override void Start()
        {
			if (listener != null)
				return;
			
			listener = new System.Net.HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            listener.BeginGetContext(EndGetRequest, listener);
        }

        /// <summary>
        /// Stops listening for incoming connections
        /// </summary>
        public override void Stop()
        {
			if (listener == null)
				return;
			
            listener.Stop();
			listener = null;
        }

        private void EndGetRequest(IAsyncResult result)
        {
			HttpListenerContext context = null;
			System.Net.HttpListener listener = (System.Net.HttpListener) result.AsyncState;
            
            try
            {
                context = listener.EndGetContext(result);
                HandleRequest(context);
            }
            catch(Exception ex)
            {
                Console.Write("Exception in listener: {0}{1}", Environment.NewLine, ex);
            }
            finally
            {
                if (context != null)
                    context.Response.Close();

                if (listener.IsListening)
                    listener.BeginGetContext(EndGetRequest, null);
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            bool isScrape = context.Request.RawUrl.StartsWith("/scrape", StringComparison.OrdinalIgnoreCase);

            BEncodedValue responseData = Handle(context.Request.RawUrl, context.Request.RemoteEndPoint.Address, isScrape);

            byte[] response = responseData.Encode();
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = response.LongLength;
            context.Response.OutputStream.Write(response, 0, response.Length);
        }

        #endregion Methods
    }
}
