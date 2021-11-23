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

namespace MonoTorrent.Connections.TrackerServer
{
    public class HttpTrackerListener : TrackerListener
    {
        CancellationTokenSource Cancellation { get; set; }

        public bool IncompleteAnnounce { get; set; }

        public bool IncompleteScrape { get; set; }

        string Prefix { get; }

        public HttpTrackerListener (IPAddress address, int port)
            : this ($"http://{address}:{port}/announce/")
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
        public override void Start ()
        {
            Cancellation?.Cancel ();
            Cancellation = new CancellationTokenSource ();
            var token = Cancellation.Token;
            var listener = new HttpListener ();
            token.Register (() => listener.Close ());

            listener.Prefixes.Add (Prefix);
            if (Prefix.EndsWith ("/announce/", StringComparison.OrdinalIgnoreCase))
                listener.Prefixes.Add (Prefix.Remove (Prefix.Length - "/announce/".Length) + "/scrape/");
            listener.Start ();
            GetContextAsync (listener, token);
            RaiseStatusChanged (ListenerStatus.Listening);
        }

        public override void Stop ()
        {
            Cancellation?.Cancel ();
            RaiseStatusChanged (ListenerStatus.NotListening);
        }

        async void GetContextAsync (HttpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                try {
                    HttpListenerContext context = await listener.GetContextAsync ().ConfigureAwait (false);
                    ProcessContextAsync (context, token);
                } catch {
                }
            }
        }

        async void ProcessContextAsync (HttpListenerContext context, CancellationToken token)
        {
            using (context.Response) {
                bool isScrape = context.Request.RawUrl.StartsWith ("/scrape", StringComparison.OrdinalIgnoreCase);

                if (IncompleteAnnounce || IncompleteScrape) {
                    await context.Response.OutputStream.WriteAsync (new byte[1024], 0, 1024, token);
                    return;
                }

                BEncodedValue responseData = Handle (context.Request.RawUrl, context.Request.RemoteEndPoint.Address, isScrape);

                byte[] response = responseData.Encode ();
                context.Response.ContentType = "text/plain";
                context.Response.StatusCode = 200;
                context.Response.ContentLength64 = response.LongLength;
                await context.Response.OutputStream.WriteAsync (response, 0, response.Length, token);
            }
        }
    }
}
