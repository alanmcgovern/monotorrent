//
// HttpTrackerListener.cs
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
using System.Net;
using System.Threading;

using MonoTorrent.BEncoding;
using MonoTorrent.Logging;

namespace MonoTorrent.Connections.TrackerServer
{
    public class HttpTrackerListener : TrackerListener
    {
        static readonly Logger logger = Logger.Create (nameof (HttpTrackerListener));

        string Prefix { get; }

        public HttpTrackerListener (IPAddress address, int port)
            : this (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"http://[{address}]:{port}/announce/" : $"http://{address}:{port}/announce/")
        {

        }

        public HttpTrackerListener (IPEndPoint endpoint)
            : this (endpoint.Address, endpoint.Port)
        {

        }

        public HttpTrackerListener (string httpPrefix)
        {
            if (string.IsNullOrEmpty (httpPrefix))
                throw new ArgumentNullException (nameof (httpPrefix));

            Prefix = httpPrefix;
        }

        /// <summary>
        /// Starts listening for incoming connections
        /// </summary>
        protected override void StartCore (CancellationToken token)
        {
            var listener = new HttpListener ();
            token.Register (() => listener.Close ());

            listener.Prefixes.Add (Prefix);
            if (Prefix.EndsWith ("/announce/", StringComparison.OrdinalIgnoreCase))
                listener.Prefixes.Add (Prefix.Remove (Prefix.Length - "/announce/".Length) + "/scrape/");
            listener.Start ();
            GetContextAsync (listener, token);
            RaiseStatusChanged (ListenerStatus.Listening);
        }

        async void GetContextAsync (HttpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                HttpListenerContext context;
                try {
                    context = await listener.GetContextAsync ().ConfigureAwait (false);
                } catch (Exception ex) {
                    logger.Exception (ex, "Error accepting request from client");
                    continue;
                }
                ProcessContextAsync (context, token);
            }
        }

        protected virtual async void ProcessContextAsync (HttpListenerContext context, CancellationToken token)
        {
            int statusCode;
            byte[] response;
            bool isScrape = context.Request.Url?.LocalPath.EndsWith ("/scrape", StringComparison.OrdinalIgnoreCase) ?? false;

            using (context.Response) {
                try {
                    if (context.Request.RawUrl is null || context.Request.Url is null)
                        throw new ArgumentException ();
                    var responseData = Handle (context.Request.RawUrl, context.Request.RemoteEndPoint.Address, isScrape);
                    response = responseData.Encode ();
                    statusCode = (int) HttpStatusCode.OK;
                } catch (Exception ex) {
                    response = Array.Empty<byte> ();
                    statusCode = (int) HttpStatusCode.InternalServerError;
                    if (isScrape)
                        logger.Exception (ex, "Error processing scrape from peer");
                    else
                        logger.Exception (ex, "Error processing announce from peer");
                }

                try {
                    context.Response.StatusCode = statusCode;
                    context.Response.ContentLength64 = response.LongLength;
                    if (response.Length > 0)
                        context.Response.ContentType = "text/plain";
                    await context.Response.OutputStream.WriteAsync (response, 0, response.Length, token);
                } catch (Exception ex) {
                    logger.Exception (ex, "Error sending response back to the client");
                }
            }
        }
    }
}
