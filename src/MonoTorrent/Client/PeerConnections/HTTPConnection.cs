using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Connections
{
    public partial class HttpConnection : IConnection
    {
        private static readonly MethodInfo method = typeof(WebHeaderCollection).GetMethod
            ("AddWithoutValidate", BindingFlags.Instance | BindingFlags.NonPublic);

        #region Constructors

        public HttpConnection(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Scheme is not http");

            Uri = uri;

            ConnectionTimeout = TimeSpan.FromSeconds(10);
            getResponseCallback = ClientEngine.MainLoop.Wrap(GotResponse);
            receivedChunkCallback = ClientEngine.MainLoop.Wrap(ReceivedChunk);
            requestMessages = new List<RequestMessage>();
            webRequests = new Queue<KeyValuePair<WebRequest, int>>();
        }

        #endregion Constructors

        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            var result = new AsyncResult(callback, state);
            result.Complete();
            return result;
        }

        public void EndConnect(IAsyncResult result)
        {
            // Do nothing
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (receiveResult != null)
                throw new InvalidOperationException("Cannot call BeginReceive twice");

            receiveResult = new HttpResult(callback, state, buffer, offset, count);
            try
            {
                // BeginReceive has been called *before* we have sent a piece request.
                // Wait for a piece request to be sent before allowing this to complete.
                if (dataStream == null)
                    return receiveResult;

                DoReceive();
                return receiveResult;
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
            var r = CompleteTransfer(result, receiveResult);
            receiveResult = null;
            return r;
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (sendResult != null)
                throw new InvalidOperationException("Cannot call BeginSend twice");
            sendResult = new HttpResult(callback, state, buffer, offset, count);

            try
            {
                var bundle = DecodeMessages(buffer, offset, count);
                if (bundle == null)
                {
                    sendResult.Complete(count);
                }
                else if (bundle.TrueForAll(delegate(PeerMessage m) { return m is RequestMessage; }))
                {
                    requestMessages.AddRange(
                        bundle.ConvertAll(delegate(PeerMessage m) { return (RequestMessage) m; }));
                    // The RequestMessages are always sequential
                    var start = (RequestMessage) bundle[0];
                    var end = (RequestMessage) bundle[bundle.Count - 1];
                    CreateWebRequests(start, end);

                    var r = webRequests.Dequeue();
                    totalExpected = r.Value;
                    BeginGetResponse(r.Key, getResponseCallback, r.Key);
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
            var r = CompleteTransfer(result, sendResult);
            sendResult = null;
            return r;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (dataStream != null)
                dataStream.Dispose();
            dataStream = null;
        }

        private List<PeerMessage> DecodeMessages(byte[] buffer, int offset, int count)
        {
            var off = offset;
            var c = count;

            try
            {
                if (sendBuffer != BufferManager.EmptyBuffer)
                {
                    Buffer.BlockCopy(buffer, offset, sendBuffer, sendBufferCount, count);
                    sendBufferCount += count;

                    off = 0;
                    c = sendBufferCount;
                }
                var messages = new List<PeerMessage>();
                for (var i = off; i < off + c;)
                {
                    var message = PeerMessage.DecodeMessage(buffer, i, c + off - i, null);
                    messages.Add(message);
                    i += message.ByteLength;
                }
                ClientEngine.BufferManager.FreeBuffer(ref sendBuffer);
                return messages;
            }
            catch (Exception)
            {
                if (sendBuffer == BufferManager.EmptyBuffer)
                {
                    ClientEngine.BufferManager.GetBuffer(ref sendBuffer, 16*1024);
                    Buffer.BlockCopy(buffer, offset, sendBuffer, 0, count);
                    sendBufferCount = count;
                }
                return null;
            }
        }


        private void ReceivedChunk(IAsyncResult result)
        {
            if (disposed)
                return;

            try
            {
                var received = dataStream.EndRead(result);
                if (received == 0)
                    throw new WebException("No futher data is available");

                receiveResult.BytesTransferred += received;
                CurrentRequest.TotalReceived += received;

                // We've received everything for this piece, so null it out
                if (CurrentRequest.Complete)
                    CurrentRequest = null;

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
                if (CurrentRequest == null && requestMessages.Count == 0)
                    RequestCompleted();
            }
        }

        private void RequestCompleted()
        {
            dataStream.Dispose();
            dataStream = null;

            // Let MonoTorrent know we've finished requesting everything it asked for
            if (sendResult != null)
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

        private void CreateWebRequests(RequestMessage start, RequestMessage end)
        {
            // Properly handle the case where we have multiple files
            // This is only implemented for single file torrents
            var uri = Uri;

            if (Uri.OriginalString.EndsWith("/"))
                uri = new Uri(uri, Manager.Torrent.Name + "/");

            // startOffset and endOffset are *inclusive*. I need to subtract '1' from the end index so that i
            // stop at the correct byte when requesting the byte ranges from the server
            var startOffset = (long) start.PieceIndex*Manager.Torrent.PieceLength + start.StartOffset;
            var endOffset = (long) end.PieceIndex*Manager.Torrent.PieceLength + end.StartOffset + end.RequestLength;

            foreach (var file in Manager.Torrent.Files)
            {
                var u = uri;
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
                    var request = (HttpWebRequest) WebRequest.Create(u);
                    AddRange(request, startOffset, file.Length - 1);
                    webRequests.Enqueue(new KeyValuePair<WebRequest, int>(request, (int) (file.Length - startOffset)));
                    startOffset = 0;
                    endOffset -= file.Length;
                }
                // All the data we want is from within this file
                else
                {
                    var request = (HttpWebRequest) WebRequest.Create(u);
                    AddRange(request, startOffset, endOffset - 1);
                    webRequests.Enqueue(new KeyValuePair<WebRequest, int>(request, (int) (endOffset - startOffset)));
                    endOffset = 0;
                }
            }
        }

        private static void AddRange(HttpWebRequest request, long startOffset, long endOffset)
        {
            method.Invoke(request.Headers,
                new object[] {"Range", string.Format("bytes={0}-{1}", startOffset, endOffset)});
        }

        private void DoReceive()
        {
            var buffer = receiveResult.Buffer;
            var offset = receiveResult.Offset;
            var count = receiveResult.Count;

            if (CurrentRequest == null && requestMessages.Count > 0)
            {
                CurrentRequest = new HttpRequestData(requestMessages[0]);
                requestMessages.RemoveAt(0);
            }

            if (totalExpected == 0)
            {
                if (webRequests.Count == 0)
                {
                    sendResult.Complete(sendResult.Count);
                }
                else
                {
                    var r = webRequests.Dequeue();
                    totalExpected = r.Value;
                    BeginGetResponse(r.Key, getResponseCallback, r.Key);
                }
                return;
            }

            if (!CurrentRequest.SentLength)
            {
                // The message length counts as the first four bytes
                CurrentRequest.SentLength = true;
                CurrentRequest.TotalReceived += 4;
                Message.Write(receiveResult.Buffer, receiveResult.Offset,
                    CurrentRequest.TotalToReceive - CurrentRequest.TotalReceived);
                receiveResult.Complete(4);
                return;
            }
            if (!CurrentRequest.SentHeader)
            {
                CurrentRequest.SentHeader = true;

                // We have *only* written the messageLength to the stream
                // Now we need to write the rest of the PieceMessage header
                var written = 0;
                written += Message.Write(buffer, offset + written, PieceMessage.MessageId);
                written += Message.Write(buffer, offset + written, CurrentRequest.Request.PieceIndex);
                written += Message.Write(buffer, offset + written, CurrentRequest.Request.StartOffset);
                count -= written;
                offset += written;
                receiveResult.BytesTransferred += written;
                CurrentRequest.TotalReceived += written;
            }

            dataStream.BeginRead(buffer, offset, count, receivedChunkCallback, null);
        }

        private void BeginGetResponse(WebRequest request, AsyncCallback callback, object state)
        {
            var result = request.BeginGetResponse(callback, state);
            ClientEngine.MainLoop.QueueTimeout(ConnectionTimeout, delegate
            {
                if (!result.IsCompleted)
                    request.Abort();
                return false;
            });
        }

        private void GotResponse(IAsyncResult result)
        {
            var r = (WebRequest) result.AsyncState;
            try
            {
                var response = r.EndGetResponse(result);
                dataStream = response.GetResponseStream();

                if (receiveResult != null)
                    DoReceive();
            }
            catch (Exception ex)
            {
                if (sendResult != null)
                    sendResult.Complete(ex);

                if (receiveResult != null)
                    receiveResult.Complete(ex);
            }
        }

        private class HttpResult : AsyncResult
        {
            public readonly byte[] Buffer;
            public readonly int Count;
            public readonly int Offset;
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
                BytesTransferred = bytes;
                base.Complete();
            }
        }

        #region Member Variables

        private byte[] sendBuffer = BufferManager.EmptyBuffer;
        private int sendBufferCount;
        private Stream dataStream;
        private bool disposed;
        private readonly AsyncCallback getResponseCallback;
        private readonly AsyncCallback receivedChunkCallback;
        private HttpResult receiveResult;
        private readonly List<RequestMessage> requestMessages;
        private HttpResult sendResult;
        private int totalExpected;
        private readonly Queue<KeyValuePair<WebRequest, int>> webRequests;

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

        internal TimeSpan ConnectionTimeout { get; set; }

        private HttpRequestData CurrentRequest { get; set; }

        EndPoint IConnection.EndPoint
        {
            get { return null; }
        }

        public bool IsIncoming
        {
            get { return false; }
        }

        public TorrentManager Manager { get; set; }

        public Uri Uri { get; }

        #endregion
    }
}