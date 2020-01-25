using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Streaming
{
    class HttpStream : IUriStream
    {
        CancellationTokenSource Cancellation { get; }

        CancellationTokenSource CurrentContextCts { get; set; }

        HttpListener Listener { get; }

        SemaphoreSlim ReadLocker { get; }

        Stream Stream { get; }

        public Uri Uri { get; }

        public HttpStream (Stream stream)
        {
            // Set up a HTTP listener for VLC/UWP to connect to.
            Uri = new Uri ($"http://127.0.0.1:5555/{Guid.NewGuid ()}/");

            Listener = new HttpListener ();
            Listener.Prefixes.Add (Uri.ToString ());
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
                    var context = await listener.GetContextAsync ();
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
                using (context.Response)
                    await HandleMediaPlayerContextAsync (context, token);
            } catch (Exception ex) {
                Console.WriteLine ("Connection closed ungracefully - {0}", ex.Message);
            }
        }

        async Task HandleMediaPlayerContextAsync (HttpListenerContext context, CancellationToken token)
        {
            var path = context.Request.Url.PathAndQuery;

            long firstByte = 0;
            long lastByte = Stream.Length - 1;

            var range = context.Request.Headers["Range"];
            if (range == null) {
                Console.WriteLine ("No range specified");
            } else if (range.StartsWith ("bytes=")) {
                Console.WriteLine ("Client requested: {0}", range);
                var parts = range.Substring ("bytes=".Length).Split (new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1) {
                    firstByte = long.Parse (parts[0]);
                } else if (parts.Length == 2) {
                    firstByte = long.Parse (parts[0]);
                    lastByte = long.Parse (parts[1]); // The range is inclusive, so we should add '1' to this offset so we send the last byte.
                }
                Console.WriteLine ("Range is {0}-{1}", firstByte, lastByte);
            }

            // Some HTTP magic
            var buffer = new byte[64 * 1024];
            context.Response.SendChunked = true;
            context.Response.AppendHeader ("Content-Range", $"bytes {firstByte}-{lastByte}");
            context.Response.StatusCode = (int) HttpStatusCode.PartialContent;
            context.Response.ContentLength64 = lastByte - firstByte + 1;
            Console.WriteLine ("Length is: {0}", context.Response.ContentLength64);
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
        }
    }
}
