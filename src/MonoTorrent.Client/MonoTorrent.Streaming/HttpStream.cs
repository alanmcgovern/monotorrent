//
// HttpStream.cs
//
// Authors:
//   Alan McGovern    alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Streaming
{
    class HttpStream : IHttpStream
    {
        CancellationTokenSource Cancellation { get; }

        CancellationTokenSource? CurrentContextCts { get; set; }

        HttpListener Listener { get; }

        SemaphoreSlim ReadLocker { get; }

        Stream Stream { get; }

        public string HttpPrefix { get; }

        public string FullUri { get; }

        public string RelativeUri { get; }

        internal HttpStream (string httpPrefix, Stream stream)
        {
            // Set up a HTTP listener for VLC/UWP to connect to.
            HttpPrefix = httpPrefix;
            RelativeUri = $"{Guid.NewGuid ()}/";
            FullUri = $"{HttpPrefix}{RelativeUri}";

            Listener = new HttpListener ();
            Listener.Prefixes.Add (FullUri);

            Listener.Start ();

            Cancellation = new CancellationTokenSource ();
            Cancellation.Token.Register (() => Listener.SafeDispose ());
            Cancellation.Token.Register (() => stream.SafeDispose ());

            ReadLocker = new SemaphoreSlim (1);
            Stream = stream;

            ListenForMediaPlayersAsync (Listener);
        }

        public void Dispose ()
        {
            Cancellation.Cancel ();
        }

        async void ListenForMediaPlayersAsync (HttpListener listener)
        {
            while (!Cancellation.IsCancellationRequested) {
                try {
                    var context = await listener.GetContextAsync ().ConfigureAwait (false);
                    Debug.WriteLine ("Received HTTP request");
                    CurrentContextCts?.Cancel ();
                    CurrentContextCts = new CancellationTokenSource ();
                    HandleMediaPlayerContext (context, CurrentContextCts.Token);
                } catch {
                    // FIXME: Be more discerning
                }
            }
        }

        async void HandleMediaPlayerContext (HttpListenerContext context, CancellationToken token)
        {
            try {
                using (token.Register (() => context.Response.Abort ()))
                using (context.Response)
                    await HandleMediaPlayerContextAsync (context, token).ConfigureAwait (false);
            } catch (Exception ex) {
                if (token.IsCancellationRequested)
                    Debug.WriteLine ("HTTP request cancelled by client.");
                else
                    Debug.WriteLine ("HTTP request closed ungracefully - {0}", ex.Message);
            }
        }

        async Task HandleMediaPlayerContextAsync (HttpListenerContext context, CancellationToken token)
        {
            long firstByte = 0;
            long lastByte = Stream.Length - 1;

            var range = context.Request.Headers["Range"];
            if (range != null && range.StartsWith ("bytes=")) {
                Debug.WriteLine (string.Format ("Client requested: {0}", range));
                var parts = range.Substring ("bytes=".Length).Split (new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) {
                    firstByte = long.Parse (parts[0]);
                } else if (parts.Length == 2) {
                    firstByte = long.Parse (parts[0]);
                    lastByte = long.Parse (parts[1]); // The range is inclusive, so we should add '1' to this offset so we send the last byte.
                }
            }

            // Clamp the last byte in case someone's player is over-reading. UWP likes to overread because it's terrible.
            if (lastByte > Stream.Length - 1)
                lastByte = Stream.Length - 1;

            // Some HTTP magic
            var buffer = new byte[64 * 1024];
            context.Response.SendChunked = true;
            context.Response.AppendHeader ("Content-Range", $"bytes {firstByte}-{lastByte}");

            //--- CORS Policy Declined goes away. A lot of SocketExceptions and MonoTorrent.Tracker Exceptions happen. When reverting to original Pre-release 132 Nupackage. No exceptions thrown.
            //--- HTML5 Player uses HLS.JS and does something, enables play button. When going to Url obtained from CreateHttpStreamAsync, Download Failed - No File. 
            //--- NodeJS Express Server CORS enabled is: 127.0.0.1:8888 
            context.Response.AppendHeader ("Access-Control-Allow-Origin", "*"); //-- should allow CORS from another domain.
            context.Response.AppendHeader ("Access-Control-Allow-Headers", "Content-Type,x-requested-with");
            context.Response.AppendHeader ("Access-Control-Allow-Methods", "GET,POST,PUT,HEAD,DELETE,OPTIONS");

            context.Response.StatusCode = (int) HttpStatusCode.PartialContent;
            context.Response.ContentLength64 = lastByte - firstByte + 1;
            Debug.WriteLine ($"Requested {firstByte} -> {lastByte}. Length: {context.Response.ContentLength64}");
            while (firstByte <= lastByte) {
                // Read the data and stream it back to the media player.
                try {
                    await ReadLocker.WaitAsync ();
                    token.ThrowIfCancellationRequested ();
                    if (Stream.Position != firstByte)
                        Stream.Seek (firstByte, SeekOrigin.Begin);
                    var read = await Stream.ReadAsync (buffer, 0, (int) Math.Min (lastByte - firstByte + 1, buffer.Length), token);
                    await context.Response.OutputStream.WriteAsync (buffer, 0, read, token);
                    await context.Response.OutputStream.FlushAsync (token);
                    firstByte += read;
                } finally {
                    ReadLocker.Release ();
                }
            }
            Debug.WriteLine ("Successfully transferred {0} -> {1}. Length: {2}", firstByte, lastByte, context.Response.ContentLength64);
        }
    }
}
