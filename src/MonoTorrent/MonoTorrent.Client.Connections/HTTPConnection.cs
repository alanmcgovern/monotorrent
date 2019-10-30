//
// HTTPConnection.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using ReusableTasks;

namespace MonoTorrent.Client.Connections
{
    sealed class HttpConnection : IConnection2
    {
        static int webSeedId;

        internal static BEncodedString CreatePeerId ()
        {
            var peerId = "-WebSeed-";
            peerId += Interlocked.Increment (ref webSeedId).ToString().PadLeft(20 - peerId.Length, '0');
            return peerId;
        }

        class HttpRequestData
        {
            public RequestMessage Request;
            public bool SentLength;
            public bool SentHeader;
            public int TotalToReceive;
            public int TotalReceived;

            public bool Complete
            {
                get { return TotalToReceive == TotalReceived; }
            }

            public HttpRequestData(RequestMessage request)
            {
                Request = request;
                PieceMessage m = new PieceMessage(request.PieceIndex, request.StartOffset, request.RequestLength);
                TotalToReceive = m.ByteLength;
            }
        }

        #region Member Variables

        public byte[] AddressBytes => new byte[4];

        public bool CanReconnect => false;

        public bool Connected => true;

        internal TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

        HttpRequestData CurrentRequest { get; set; }

        Stream DataStream { get; set; }

        int DataStreamCount { get; set; }

        WebResponse DataStreamResponse { get; set; }

        private bool Disposed { get; set; }

        EndPoint IConnection.EndPoint => null;

        public bool IsIncoming => false;

        public TorrentManager Manager { get; set; }

        AutoResetEvent ReceiveWaiter { get; } = new AutoResetEvent(false);

        List<RequestMessage> RequestMessages { get; } = new List<RequestMessage>();

        TaskCompletionSource<object> SendResult { get; set; }

        public Uri Uri { get; }

        Queue<KeyValuePair<WebRequest, int>> WebRequests { get; } = new Queue<KeyValuePair<WebRequest, int>>();

        #endregion


        #region Constructors

        public HttpConnection(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) && !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Scheme is not http or https");

            Uri = uri;
         }

        #endregion Constructors

        Task IConnection.ConnectAsync ()
            => Task.CompletedTask;

        public ReusableTask ConnectAsync ()
        {
            return ReusableTask.CompletedTask;
        }

        async Task<int> IConnection.ReceiveAsync (byte[] buffer, int offset, int count)
            => await ReceiveAsync (buffer, offset, count);

        public async ReusableTask<int> ReceiveAsync (byte[] buffer, int offset, int count)
        {
            // This is a little tricky, so let's spell it out in comments...
            if (Disposed)
                throw new OperationCanceledException ();

            // If this is the first time ReceiveAsync is invoked, then we should get the first PieceMessage from the queue
            if (CurrentRequest == null)
            {
                // When we call 'SendAsync' with request piece messages, we add them to the list and then toggle the handle.
                // When this returns it means we have requests ready to go!
                await Task.Run(() => ReceiveWaiter.WaitOne());
                if (Disposed)
                    throw new OperationCanceledException ();

                // Grab the request. We know the 'SendAsync' call won't return until we process all the queued requests, so
                // this is threadsafe now.
                CurrentRequest = new HttpRequestData(RequestMessages[0]);
                RequestMessages.RemoveAt(0);
            }

            // If we have not sent the length header for this message, send it now
            if (!CurrentRequest.SentLength)
            {
                // The message length counts as the first four bytes
                CurrentRequest.SentLength = true;
                CurrentRequest.TotalReceived += 4;
                Message.Write(buffer, offset, CurrentRequest.TotalToReceive - CurrentRequest.TotalReceived);
                return 4;
            }

            // Once we've sent the length header, the next thing we need to send is the metadata for the 'Piece' message
            int written = 0;
            if (!CurrentRequest.SentHeader)
            {
                CurrentRequest.SentHeader = true;

                // We have *only* written the messageLength to the stream
                // Now we need to write the rest of the PieceMessage header
                written += Message.Write(buffer, offset + written, PieceMessage.MessageId);
                written += Message.Write(buffer, offset + written, CurrentRequest.Request.PieceIndex);
                written += Message.Write(buffer, offset + written, CurrentRequest.Request.StartOffset);
                count -= written;
                offset += written;
                CurrentRequest.TotalReceived += written;
            }

            // Once we have sent the message length, and the metadata, we now need to add the actual data from the HTTP server.
            // If we have already connected to the server then DataStream will be non-null and we can just read the next bunch
            // of data from it.
            if (DataStream != null) {
                var result = await DataStream.ReadAsync(buffer, offset, count);
                DataStreamCount -= result;
                // If result is zero it means we've read the last data from the stream.
                if (result == 0)
                {
                    using (DataStreamResponse)
                        DataStream.Dispose();

                    DataStreamResponse = null;
                    DataStream = null;

                    // If we requested more data (via the range header) than we were actually given, then it's a truncated
                    // stream and we can give up immediately.
                    if (DataStreamCount > 0)
                        throw new WebException("Unexpected end of stream");
                }
                else
                {
                    // Otherwise if we have received non-zero data we can accumulate that!
                    CurrentRequest.TotalReceived += result;
                    // If the request is complete we should dequeue the next RequestMessage so we can process
                    // that the next ReceiveAsync is invoked. Otherwise, if we have processed all the queued
                    // messages we can mark the 'SendAsync' as complete and we can wait for the piece picker
                    // to add more requests for us to process.
                    if (CurrentRequest.Complete)
                    {
                        if (RequestMessages.Count > 0)
                        {
                            CurrentRequest = new HttpRequestData(RequestMessages[0]);
                            RequestMessages.RemoveAt(0);
                        }
                        else
                        {
                            using (DataStreamResponse)
                                DataStream.Dispose();

                            DataStreamResponse = null;
                            DataStream = null;

                            CurrentRequest = null;

                            SendResult.TrySetResult(null);
                        }
                    }
                    return result + written;
                }
            }

            // Finally, if we have had no datastream what we need to do is execute the next web request in our list,
            // and then begin reading data from that stream.
            while (WebRequests.Count > 0)
            {
                var r = WebRequests.Dequeue();
                using (var cts = new CancellationTokenSource (ConnectionTimeout))
                using (cts.Token.Register (() => r.Key.Abort ())) {
                    DataStreamResponse = await r.Key.GetResponseAsync();
                    DataStream = DataStreamResponse.GetResponseStream();
                    DataStreamCount = r.Value;
                    return await ReceiveAsync(buffer, offset, count) + written;
                }
            }

            // If we reach this point it means that we processed all webrequests and still ended up receiving *less* data than we required,
            // and we did not throw an unexpected end of stream exception.
            throw new WebException ("Unable to download the required data from the server");
        }

        async Task<int> IConnection.SendAsync (byte[] buffer, int offset, int count)
            => await SendAsync (buffer, offset, count);

        public async ReusableTask<int> SendAsync (byte[] buffer, int offset, int count)
        {
            SendResult = new TaskCompletionSource<object>();

            List<RequestMessage> bundle = DecodeMessages(buffer, offset, count);
            if (bundle.Count > 0)
            {
                RequestMessages.AddRange(bundle);
                // The RequestMessages are always sequential
                RequestMessage start = (RequestMessage)bundle[0];
                RequestMessage end = (RequestMessage)bundle[bundle.Count - 1];
                CreateWebRequests(start, end);
            }
            else
            {
                return count;
            }

            ReceiveWaiter.Set();
            await SendResult.Task;
            return count;
        }

        static List<RequestMessage> DecodeMessages(byte[] buffer, int offset, int count)
        {
            var messages = new List<RequestMessage>();
            for (int i = offset; i < offset + count;)
            {
                PeerMessage message = PeerMessage.DecodeMessage(buffer, i, count + offset - i, null);
                if (message is RequestMessage msg)
                    messages.Add(msg);
                i += message.ByteLength;
            }
            return messages;
        }


        private void CreateWebRequests(RequestMessage start, RequestMessage end)
        {
            // Properly handle the case where we have multiple files
            // This is only implemented for single file torrents
            Uri uri = Uri;

            if (Uri.OriginalString.EndsWith("/"))
                uri = new Uri(uri, Manager.Torrent.Name + "/");

            // startOffset and endOffset are *inclusive*. I need to subtract '1' from the end index so that i
            // stop at the correct byte when requesting the byte ranges from the server
            long startOffset = (long)start.PieceIndex * Manager.Torrent.PieceLength + start.StartOffset;
            long endOffset = (long)end.PieceIndex * Manager.Torrent.PieceLength + end.StartOffset + end.RequestLength;

            foreach (TorrentFile file in Manager.Torrent.Files)
            {
                Uri u = uri;
                if (Manager.Torrent.Files.Length > 1)
                    u = new Uri(u, file.Path);
                if (endOffset == 0)
                    break;

                // We want data from a later file
                if (startOffset >= file.Length)
                {
                    startOffset -= file.Length;
                    endOffset -= file.Length;
                }
                // We want data from the end of the current file and from the next few files
                else if (endOffset >= file.Length)
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(u);
                    request.AddRange(startOffset, file.Length - 1);
                    WebRequests.Enqueue(new KeyValuePair<WebRequest, int>(request, (int)(file.Length - startOffset)));
                    startOffset = 0;
                    endOffset -= file.Length;
                }
                // All the data we want is from within this file
                else
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(u);
                    request.AddRange (startOffset, endOffset - 1);
                    WebRequests.Enqueue(new KeyValuePair<WebRequest,int>(request, (int)(endOffset - startOffset)));
                    endOffset = 0;
                }
            }
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;

            SendResult?.TrySetCanceled ();
            DataStreamResponse?.Dispose();
            DataStream?.Dispose();
            ReceiveWaiter.Set ();

            DataStreamResponse = null;
            DataStream = null;
        }
    }
}
