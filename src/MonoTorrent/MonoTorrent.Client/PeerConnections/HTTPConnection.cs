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

namespace MonoTorrent.Client.Connections
{
    public class HttpConnection : IConnection
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

        #region Member Variables

        private int totalExpected;
        private int length;
        private bool writeHeader;
        private Stream dataStream;
        private AsyncCallback getResponseCallback;
        private TorrentManager manager;
        private HttpResult receiveResult;
        private RequestMessage requestMessage;
        private HttpResult sendResult;
        private Uri uri;

        public bool CanReconnect
        {
            get { return false; }
        }

        public bool Connected
        {
            get { return true; }
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
        }

        #endregion Constructors


        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            AsyncResult result = new AsyncResult(callback, state);
            result.Complete();
            return result;
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Console.WriteLine("BeginReceive");
            if (receiveResult != null)
                throw new InvalidOperationException("Cannot call BeginReceive twice");

            receiveResult = new HttpResult(callback, state, buffer, offset, count);
            try
            {
                if (SendLength())
                    return receiveResult;

                if (dataStream != null && requestMessage != null)
                {
                    // We have *only* written the messageLength to the stream
                    // Now we need to write the rest of the PieceMessage header
                    if (writeHeader)
                    {
                        writeHeader = false;
                        int written = 0;
                        written += Message.Write(buffer, offset + written, PieceMessage.MessageId);
                        written += Message.Write(buffer, offset + written, requestMessage.PieceIndex);
                        written += Message.Write(buffer, offset + written, requestMessage.StartOffset);
                        count -= written;
                        offset += written;
                        //totalExpected -= written;
                        receiveResult.BytesTransferred += written;
                    }

                    dataStream.BeginRead(buffer, offset, count, ReceivedChunk, null);
                }
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

        private void ReceivedChunk(IAsyncResult result)
        {
            try
            {
                int received = dataStream.EndRead(result);
                receiveResult.BytesTransferred += received;
                totalExpected -= received;
                receiveResult.Complete();
            }
            catch (Exception ex)
            {
                receiveResult.Complete(ex);
            }
            finally
            {
                if (totalExpected == 0)
                    RequestCompleted();
            }
        }

        private void RequestCompleted()
        {
            dataStream.Close();
            dataStream = null;
            requestMessage = null;

            // Let MonoTorrent know we've finished requesting that piece
            sendResult.Complete(sendResult.Count);
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Console.WriteLine("BeginSend");
            if (sendResult != null)
                throw new InvalidOperationException("Cannot call BeginReceive twice");
            sendResult = new HttpResult(callback, state, buffer, offset, count);

            try
            {
                PeerMessage message = PeerMessage.DecodeMessage(buffer, offset + 4, count - 4, null);
                if (message is RequestMessage)
                {
                    requestMessage = (RequestMessage)message;
                    WebRequest r = CreateWebRequest(requestMessage);
                    r.BeginGetResponse(getResponseCallback, r);
                }
                else
                {
                    // Pretend we sent all the data
                    sendResult.Complete(count);
                }
            }
            catch(Exception ex)
            {
                sendResult.Complete(ex);
            }

            return sendResult;
        }

        public void EndConnect(IAsyncResult result)
        {
            // Do nothing
        }

        public int EndReceive(IAsyncResult result)
        {
            int r = CompleteTransfer(result, receiveResult);
            receiveResult = null;
            Console.WriteLine("EndReceive");
            return r;
        }

        public int EndSend(IAsyncResult result)
        {
            int r = CompleteTransfer(result, sendResult);
            sendResult = null;
            Console.WriteLine("EndSend");
            return r;
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

        public void Dispose()
        {
            //do nothing
        }

        public byte[] AddressBytes
        {
            get { return new byte[4]; }
        }

        private void GotResponse(IAsyncResult result)
        {
            WebRequest r = (WebRequest)result.AsyncState;
            try
            {
                WebResponse response = r.EndGetResponse(result);
                dataStream = response.GetResponseStream();
                PieceMessage m = new PieceMessage(Manager, requestMessage.PieceIndex, requestMessage.StartOffset, requestMessage.RequestLength);
                length = m.ByteLength;
                // Warning receive and send calls asynchronous. Need better signalling!
                sendLength = true;
                SendLength();
            }
            catch (Exception ex)
            {
                if (sendResult != null)
                    sendResult.Complete(ex);

                if (receiveResult != null)
                    receiveResult.Complete(ex);
            }
        }
        private bool sendLength;

        private bool SendLength()
        {
            lock (this)
            {
                if (sendLength && receiveResult != null && requestMessage != null && receiveResult.Count == 4 && receiveResult.BytesTransferred == 0)
                {
                    sendLength = false;
                    writeHeader = true;
                    Message.Write(receiveResult.Buffer, receiveResult.Offset, length - 4);
                    receiveResult.Complete(receiveResult.Count);
                    return true;
                }
            }

            return false;
        }

        private WebRequest CreateWebRequest(RequestMessage requestMessage)
        {
            // Properly handle the case where we have multiple files
            // This is only implemented for single file torrents
            Uri u = uri;

            if (uri.OriginalString.EndsWith("/"))
                u = new Uri(uri, Manager.Torrent.Name);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(u);
            request.AddRange(requestMessage.StartOffset, requestMessage.StartOffset + requestMessage.RequestLength);
            totalExpected = requestMessage.RequestLength;
            return request;
        }
    }
}
