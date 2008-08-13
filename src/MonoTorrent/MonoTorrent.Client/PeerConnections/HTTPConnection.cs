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
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.IO;

using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using System.Text.RegularExpressions;

namespace MonoTorrent.Client.Connections
{
    public partial class HttpConnection : IConnection
    {
        private class HttpResult : AsyncResult
        {
            public byte[] Buffer;
            public int Offset;
            public int Count;
            public int BytesTransferred;

            public HttpResult(AsyncCallback callback, object state, byte[] buffer, int offset, int count)
                : base(callback, state)
            {
                Buffer = buffer;
                Offset = offset;
                Count = count;
            }

            public void Complete(int bytes)
            {
                this.BytesTransferred = bytes;
                base.Complete();
            }
        }

        Regex rangeMatcher = new Regex(@"(\d{1,10})-(\d{1,10})");

        #region Member Variables

        private HttpRequestData currentRequest;
        private Stream dataStream;
        private AsyncCallback getResponseCallback;
        private int length;
        private TorrentManager manager;
        private HttpResult receiveResult;
        private List<RequestMessage> requestMessages;
        private HttpResult sendResult;
        private int totalExpected;
        private Uri uri;

        public byte[] AddressBytes
        {
            get { return new byte[4]; }
        }

        public bool CanReconnect
        {
            get { return false; }
        }

        public bool Connected
        {
            get { return true; }
        }

        private HttpRequestData CurrentRequest
        {
            get { return currentRequest; }
        }

        EndPoint IConnection.EndPoint
        {
            get { return null; }
        }

        public bool IsIncoming
        {
            get { return false; }
        }

        public TorrentManager Manager
        {
            get { return manager; }
            set { manager = value; }
        }

        public Uri Uri
        {
            get { return uri; }
        }

        private bool WriteHeader
        {
            get
            {
                if (receiveResult.BytesTransferred != 0 || receiveResult.Offset == 4)
                    return false;
                int total = 0;
                foreach (RequestMessage m in requestMessages)
                    total += m.RequestLength;
                return totalExpected == total;
            }
        }

        #endregion


        #region Constructors

        public HttpConnection(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Scheme is not http");

            this.uri = uri;
            getResponseCallback = GotResponse;
            requestMessages = new List<RequestMessage>();
        }

        #endregion Constructors


        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            AsyncResult result = new AsyncResult(callback, state);
            result.Complete();
            return result;
        }

        public void EndConnect(IAsyncResult result)
        {
            // Do nothing
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Console.WriteLine("BeginReceive");
            if (receiveResult != null)
                throw new InvalidOperationException("Cannot call BeginReceive twice");

            receiveResult = new HttpResult(callback, state, buffer, offset, count);
            try
            {
                if (currentRequest != null && currentRequest.Complete)
                    throw new MessageException("Should be impossible - current request is complete");

                if (currentRequest == null && requestMessages.Count > 0)
                {
                    currentRequest = new HttpRequestData(requestMessages[0]);
                    requestMessages.RemoveAt(0);
                }

                if (!currentRequest.SentLength)
                {
                    if (count != 4)
                        throw new MessageException("More than 4 bytes requested yet message length has not been written");

                    // The message length counts as the first four bytes
                    currentRequest.SentLength = true;
                    currentRequest.TotalReceived += 4;
                    Message.Write(receiveResult.Buffer, receiveResult.Offset, currentRequest.TotalToReceive - currentRequest.TotalReceived);
                    receiveResult.Complete(receiveResult.Count);
                    return receiveResult;
                }
                else if (!currentRequest.SentHeader)
                {
                    currentRequest.SentHeader = true;

                    // We have *only* written the messageLength to the stream
                    // Now we need to write the rest of the PieceMessage header
                    int written = 0;
                    written += Message.Write(buffer, offset + written, PieceMessage.MessageId);
                    written += Message.Write(buffer, offset + written, CurrentRequest.Request.PieceIndex);
                    written += Message.Write(buffer, offset + written, CurrentRequest.Request.StartOffset);
                    count -= written;
                    offset += written;
                    receiveResult.BytesTransferred += written;
                    currentRequest.TotalReceived += written;
                }

                dataStream.BeginRead(buffer, offset, count, ReceivedChunk, null);
            }
            catch (Exception ex)
            {
                if (sendResult != null)
                    sendResult.Complete(ex);

                if (receiveResult != null)
                    receiveResult.Complete(ex);
            }

            return receiveResult;
        }

        public int EndReceive(IAsyncResult result)
        {
            int r = CompleteTransfer(result, receiveResult);
            receiveResult = null;
            Console.WriteLine("EndReceive");
            return r;
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Console.WriteLine("BeginSend");
            if (sendResult != null)
                throw new InvalidOperationException("Cannot call BeginReceive twice");
            sendResult = new HttpResult(callback, state, buffer, offset, count);

            try
            {
                List<PeerMessage> bundle = new List<PeerMessage>();
                for (int i = offset; i < offset + count; )
                {
                    PeerMessage message = PeerMessage.DecodeMessage(buffer, i, count + offset - i, null);
                    bundle.Add(message);
                    i += message.ByteLength;
                }

                if (bundle.TrueForAll(delegate(PeerMessage m) { return m is RequestMessage; }))
                {
                    requestMessages.AddRange(bundle.ConvertAll<RequestMessage>(delegate(PeerMessage m) { return (RequestMessage)m; }));
                    // The RequestMessages are always sequential
                    RequestMessage start = (RequestMessage)bundle[0];
                    RequestMessage end = (RequestMessage)bundle[bundle.Count - 1];
                    WebRequest r = CreateWebRequest(start, end);
                    r.BeginGetResponse(getResponseCallback, r);
                }
                else
                {
                    sendResult.Complete(count);
                }
            }
            catch (Exception ex)
            {
                sendResult.Complete(ex);
            }

            return sendResult;
        }

        public int EndSend(IAsyncResult result)
        {
            int r = CompleteTransfer(result, sendResult);
            sendResult = null;
            Console.WriteLine("EndSend");
            return r;
        }




        private void ReceivedChunk(IAsyncResult result)
        {
            try
            {
                int received = dataStream.EndRead(result);
                receiveResult.BytesTransferred += received;
                currentRequest.TotalReceived += received;

                // We've received everything for this piece, so null it out
                if (currentRequest.Complete)
                    currentRequest = null;

                totalExpected -= received;
                receiveResult.Complete();
            }
            catch (Exception ex)
            {
                receiveResult.Complete(ex);
            }
            finally
            {
                // If there are no more requests pending, complete the Send call
                if (currentRequest == null && requestMessages.Count == 0)
                    RequestCompleted();
            }
        }

        private void RequestCompleted()
        {
            dataStream.Close();
            dataStream = null;

            // Let MonoTorrent know we've finished requesting everything it asked for
            sendResult.Complete(sendResult.Count);
        }

        private int CompleteTransfer(IAsyncResult supplied, HttpResult expected)
        {
            if (supplied == null)
                throw new ArgumentNullException("result");

            if (supplied != expected)
                throw new ArgumentException("Invalid IAsyncResult supplied");

            if (!expected.IsCompleted)
                expected.AsyncWaitHandle.WaitOne();

            if (expected.SavedException != null)
                throw expected.SavedException;

            return expected.BytesTransferred;
        }

        private WebRequest CreateWebRequest(RequestMessage start, RequestMessage end)
        {
            // Properly handle the case where we have multiple files
            // This is only implemented for single file torrents
            Uri u = uri;

            if (uri.OriginalString.EndsWith("/"))
                u = new Uri(uri, Manager.Torrent.Name);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(u);
            int startOffset = start.PieceIndex * manager.Torrent.PieceLength + start.StartOffset;
            int endOffset = end.PieceIndex * manager.Torrent.PieceLength + end.StartOffset + end.RequestLength;
            request.AddRange(startOffset, endOffset);
            totalExpected = endOffset - startOffset;
            return request;
        }

        public void Dispose()
        {
            //do nothing
        }

        private void GotResponse(IAsyncResult result)
        {
            WebRequest r = (WebRequest)result.AsyncState;
            try
            {
                WebResponse response = r.EndGetResponse(result);
                dataStream = response.GetResponseStream();
            }
            catch (Exception ex)
            {
                if (sendResult != null)
                    sendResult.Complete(ex);

                if (receiveResult != null)
                    receiveResult.Complete(ex);
            }
        }
    }
}
