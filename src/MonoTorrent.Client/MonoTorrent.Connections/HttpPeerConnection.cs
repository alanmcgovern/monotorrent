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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Logging;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer
{
    sealed class HttpPeerConnection : IPeerConnection
    {
        static readonly Logger Log = Logger.Create (nameof (HttpPeerConnection));

        static readonly Uri PaddingFileUri = new Uri ("http://__paddingfile__");

        class HttpRequestData
        {
            public bool SentLength;
            public bool SentHeader;
            public readonly int TotalToReceive;
            public int TotalReceived;

            public BlockInfo BlockInfo { get; }

            public bool Complete => TotalToReceive == TotalReceived;

            public HttpRequestData (BlockInfo blockInfo)
            {
                BlockInfo = blockInfo;
                var m = new PieceMessage (BlockInfo.PieceIndex, BlockInfo.StartOffset, BlockInfo.RequestLength);
                TotalToReceive = m.ByteLength;
            }
        }
        class ZeroStream : Stream
        {
            public override bool CanRead { get; } = true;
            public override bool CanSeek { get; } = false;
            public override bool CanWrite { get; } = false;
            public override long Length { get; } = -1;
            public override long Position { get; set; }

            public override void Flush ()
            {
                throw new NotImplementedException ();
            }

            public override int Read (byte[] buffer, int offset, int count)
            {
                buffer.AsSpan (offset, count).Fill (0);
                return count;
            }
#if !NETSTANDARD2_0 && !NET472
            public override int Read (Span<byte> buffer)
            {
                buffer.Fill (0);
                return buffer.Length;
            }
#endif
            public override Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                buffer.AsSpan (offset, count).Fill (0);
                return Task.FromResult (count);
            }
#if !NETSTANDARD2_0 && !NET472
            public override ValueTask<int> ReadAsync (Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                buffer.Span.Fill (0);
                return new ValueTask<int> (buffer.Length);
            }
#endif
            public override long Seek (long offset, SeekOrigin origin)
                => throw new NotImplementedException ();

            public override void SetLength (long value)
                => throw new NotImplementedException ();

            public override void Write (byte[] buffer, int offset, int count)
                => throw new NotImplementedException ();
        }

        #region Member Variables

        public ReadOnlyMemory<byte> AddressBytes { get; } = new byte[4];

        public bool CanReconnect => false;

        public bool Disposed { get; set; }

        Factories RequestCreator { get; }

        HttpClient? Requester { get; set; }

        public TimeSpan ConnectionTimeout {
            get; set;
        }

        HttpRequestData? CurrentRequest { get; set; }

        Stream? DataStream { get; set; }

        long DataStreamCount { get; set; }

        WebResponse? DataStreamResponse { get; set; }

        public IPEndPoint? EndPoint { get; } = null;

        public bool IsIncoming => false;

        ITorrentManagerInfo TorrentData { get; set; }

        AutoResetEvent ReceiveWaiter { get; } = new AutoResetEvent (false);

        List<BlockInfo> RequestMessages { get; } = new List<BlockInfo> ();

        TaskCompletionSource<object?>? SendResult { get; set; }

        public Uri Uri { get; }

        Queue<(Uri fileUri, long startOffset, long count)> WebRequests { get; } = new Queue<(Uri fileUri, long startOffset, long count)> ();

        #endregion


        #region Constructors

        public HttpPeerConnection (ITorrentManagerInfo torrentData, TimeSpan connectionTimeout, Factories requestCreator, Uri uri)
        {
            ConnectionTimeout = connectionTimeout;
            RequestCreator = requestCreator;
            TorrentData = torrentData ?? throw new ArgumentNullException (nameof (torrentData));
            Uri = uri;
        }

        #endregion Constructors

        public ReusableTask ConnectAsync ()
        {
            return ReusableTask.CompletedTask;
        }

        public async ReusableTask<int> ReceiveAsync (Memory<byte> socketBuffer)
        {
            // This is a little tricky, so let's spell it out in comments...
            if (Disposed)
                throw new OperationCanceledException ();

            // If this is the first time ReceiveAsync is invoked, then we should get the first PieceMessage from the queue
            if (CurrentRequest == null) {
                // When we call 'SendAsync' with request piece messages, we add them to the list and then toggle the handle.
                // When this returns it means we have requests ready to go!
                await Task.Run (() => ReceiveWaiter.WaitOne ());
                if (Disposed)
                    throw new OperationCanceledException ();

                // Grab the request. We know the 'SendAsync' call won't return until we process all the queued requests, so
                // this is threadsafe now.
                CurrentRequest = new HttpRequestData (RequestMessages[0]);
                RequestMessages.RemoveAt (0);
            }

            // If we have not sent the length header for this message, send it now
            if (!CurrentRequest.SentLength) {
                // The message length counts as the first four bytes
                CurrentRequest.SentLength = true;
                CurrentRequest.TotalReceived += 4;
                Message.Write (socketBuffer.Span, CurrentRequest.TotalToReceive - CurrentRequest.TotalReceived);
                return 4;
            }

            // Once we've sent the length header, the next thing we need to send is the metadata for the 'Piece' message
            int written = 0;
            if (!CurrentRequest.SentHeader) {
                CurrentRequest.SentHeader = true;

                // We have *only* written the messageLength to the stream
                // Now we need to write the rest of the PieceMessage header
                Message.Write (socketBuffer.Span.Slice (written, 1), PieceMessage.MessageId);
                written++;

                Message.Write (socketBuffer.Span.Slice (written, 4), CurrentRequest.BlockInfo.PieceIndex);
                written += 4;

                Message.Write (socketBuffer.Span.Slice (written, 4), CurrentRequest.BlockInfo.StartOffset);
                written += 4;

                socketBuffer = socketBuffer.Slice (written);
                CurrentRequest.TotalReceived += written;
            }

            // Once we have sent the message length, and the metadata, we now need to add the actual data from the HTTP server.
            // If we have already connected to the server then DataStream will be non-null and we can just read the next bunch
            // of data from it.
            if (DataStream != null) {
                int result = 0;
                // If DataStreamCount is zero, then don't try try to read from the DataStream.
                // We do not expect there to be any extra data available.
                if (DataStreamCount > 0) {
                    // If we are reading from the DataStream, we should ensure that the buffer passed to the stream
                    // is sliced to be the same length as the maximum number of bytes we expect to receive. This
                    // prevents unintentional over-reading if the stream happens to contain more bytes than we need right now.
                    var toRead = Math.Min ((int) DataStreamCount, socketBuffer.Length);
                    result = await DataStream.ReadAsync (socketBuffer.Slice (0, toRead));
                    socketBuffer = socketBuffer.Slice (result);
                }

                DataStreamCount -= result;
                // If result is zero it means we've read the last data from the stream.
                if (result == 0) {
                    using (DataStreamResponse)
                        DataStream.Dispose ();

                    DataStreamResponse = null;
                    DataStream = null;

                    // If we requested more data (via the range header) than we were actually given, then it's a truncated
                    // stream and we can give up immediately.
                    if (DataStreamCount > 0)
                        throw new WebException ("Unexpected end of stream");
                } else {
                    // Otherwise if we have received non-zero data we can accumulate that!
                    CurrentRequest.TotalReceived += result;
                    // If the request is complete we should dequeue the next RequestMessage so we can process
                    // that the next ReceiveAsync is invoked. Otherwise, if we have processed all the queued
                    // messages we can mark the 'SendAsync' as complete and we can wait for the piece picker
                    // to add more requests for us to process.
                    if (CurrentRequest.Complete) {
                        if (RequestMessages.Count > 0) {
                            CurrentRequest = new HttpRequestData (RequestMessages[0]);
                            RequestMessages.RemoveAt (0);
                        } else {
                            using (DataStreamResponse)
                                DataStream.Dispose ();

                            DataStreamResponse = null;
                            DataStream = null;

                            CurrentRequest = null;

                            SendResult!.TrySetResult (null);
                        }
                    }
                    return result + written;
                }
            }

            // Finally, if we have had no datastream what we need to do is execute the next web request in our list,
            // and then begin reading data from that stream.
            while (WebRequests.Count > 0) {
                var rr = WebRequests.Dequeue ();

                Requester?.Dispose ();
                if (rr.fileUri == PaddingFileUri) {
                    DataStream = new ZeroStream ();
                    DataStreamCount = rr.count;
                } else {
                    Requester = RequestCreator.CreateHttpClient ();
                    var msg = new HttpRequestMessage (HttpMethod.Get, rr.fileUri);
                    msg.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue (rr.startOffset, rr.startOffset + rr.count - 1);
                    Requester.Timeout = ConnectionTimeout;
                    DataStream = await (await Requester.SendAsync (msg)).Content.ReadAsStreamAsync ();
                    DataStreamCount = rr.count;
                }
                return await ReceiveAsync (socketBuffer) + written;
            }

            // If we reach this point it means that we processed all webrequests and still ended up receiving *less* data than we required,
            // and we did not throw an unexpected end of stream exception.
            throw new WebException ("Unable to download the required data from the server");
        }

        public async ReusableTask<int> SendAsync (Memory<byte> socketBuffer)
        {
            SendResult = new TaskCompletionSource<object?> ();

            var info = TorrentData.TorrentInfo;
            List<BlockInfo> bundle = DecodeMessages (socketBuffer.Span);

            // If the messages in the send buffer are anything *other* than piece request messages,
            // just pretend the bytes were sent and everything was fine.
            if (bundle.Count == 0 || info == null)
                return socketBuffer.Length;

            // Otherwise, if there were 1 or more piece request messages, convert these to HTTP requests.
            // and only mark the bytes as successfully sent after all webrequests have run to completion
            // and the data has been received.
            RequestMessages.AddRange (bundle);
            ValidateWebRequests (info, RequestMessages.ToArray ());
            CreateWebRequestsForSequentialRange (RequestMessages[0], RequestMessages[RequestMessages.Count - 1]);
            ReceiveWaiter.Set ();
            await SendResult.Task;
            return socketBuffer.Length;
        }

        static List<BlockInfo> DecodeMessages (ReadOnlySpan<byte> buffer)
        {
            var messages = new List<BlockInfo> ();
            for (int i = 0; i < buffer.Length;) {
                var payload = PeerMessage.DecodeMessage (buffer.Slice (i), null);
                if (payload.message is RequestMessage msg)
                    messages.Add (new BlockInfo (msg.PieceIndex, msg.StartOffset, msg.RequestLength));
                i += payload.message.ByteLength;
                payload.releaser.Dispose ();
            }
            return messages;
        }

        static void ValidateWebRequests (ITorrentInfo torrentInfo, BlockInfo[] blocks)
        {
            if (blocks.Length > 0) {
                BlockInfo startBlock = blocks[0];
                BlockInfo currentBlock = startBlock;
                for (int i = 1; i < blocks.Length; i++) {
                    BlockInfo previousBlock = blocks[i - 1];
                    currentBlock = blocks[i];
                    long endOffsetOfPreviousEndBlock = torrentInfo.PieceIndexToByteOffset (previousBlock.PieceIndex) + previousBlock.StartOffset + previousBlock.RequestLength;
                    long startOffsetOfCurrentBlock = torrentInfo.PieceIndexToByteOffset (currentBlock.PieceIndex) + currentBlock.StartOffset;
                    if (endOffsetOfPreviousEndBlock != startOffsetOfCurrentBlock) {
                        var msg = "Piece requests made from HTTP peers must be a strictly sequential range of blocks. The range must be 1 or more blocks long.";
                        Log.Error (msg);
                        throw new InvalidOperationException (msg);
                    }
                }
            }
        }

        void CreateWebRequestsForSequentialRange (BlockInfo start, BlockInfo end)
        {
            Uri uri = Uri;

            if (Uri.OriginalString.EndsWith ("/"))
                uri = new Uri (uri, $"{TorrentData.Name}/");

            // startOffset and endOffset are *inclusive*. I need to subtract '1' from the end index so that i
            // stop at the correct byte when requesting the byte ranges from the server.
            //
            // These values are also always relative to the *current* file as we iterate through the list of files.
            long startOffset = TorrentData.TorrentInfo!.PieceIndexToByteOffset (start.PieceIndex) + start.StartOffset;
            long count = TorrentData.TorrentInfo!.PieceIndexToByteOffset (end.PieceIndex) + end.StartOffset + end.RequestLength - startOffset;

            foreach (var file in TorrentData.Files) {
                // Bail out after we've read all the data.
                if (count == 0)
                    break;

                // If the first byte of data is from the next file, move to the next file immediately
                // and adjust start offset to be relative to that file.
                if (startOffset >= file.Length + file.Padding) {
                    startOffset -= file.Length + file.Padding;
                    continue;
                }

                Uri u = uri;
                if (TorrentData.Files.Count > 1)
                    u = new Uri (u, file.Path);

                // Should data be read from this file?
                if (startOffset < file.Length) {
                    var toReadFromFile = Math.Min (count, file.Length - startOffset);
                    WebRequests.Enqueue ((u, startOffset, toReadFromFile));
                    count -= toReadFromFile;
                }

                // Should data be read from this file's padding?
                if (file.Padding > 0 && count > 0) {
                    var toReadFromPadding = Math.Min (count, file.Padding);
                    WebRequests.Enqueue ((PaddingFileUri, 0, toReadFromPadding));
                    count -= toReadFromPadding;
                }

                // As of the next read, we'll be reading data from the start of the file.
                startOffset = 0;
            }
        }

        public void Dispose ()
        {
            if (Disposed)
                return;

            Disposed = true;

            Requester?.Dispose ();
            SendResult?.TrySetCanceled ();
            DataStreamResponse?.Dispose ();
            DataStream?.Dispose ();
            ReceiveWaiter.Set ();

            DataStreamResponse = null;
            DataStream = null;
        }
    }
}
