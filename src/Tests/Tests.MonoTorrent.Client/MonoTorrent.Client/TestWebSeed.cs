//
// TestWebSeed.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Modes;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;
using MonoTorrent.PiecePicking;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class WebSeedTests
    {
        readonly Regex rangeMatcher = new Regex (@"(\d{1,10})-(\d{1,10})");

        bool partialData;
        public readonly int Count = 5;
        TestRig rig;
        IPeerConnection connection;
        HttpListener listener;
        //private RequestMessage m;
        public string ListenerURL;

        PeerId id;
        RequestBundle requests;
        readonly int numberOfPieces = 50;

        [SetUp]
        public void Setup ()
        {
            var rand = new Random ();
            for (int i = 0; i < 10; i++) {
                try {
                    listener = new HttpListener ();
                    ListenerURL = $"http://127.0.0.1:{rand.Next (10000, 50000)}/announce/";
                    listener.Prefixes.Add (ListenerURL);
                    listener.Start ();
                    break;
                } catch {

                }
            }
            listener.BeginGetContext (GotContext, listener);

            requestedUrl.Clear ();
            partialData = false;

            rig = TestRig.CreateMultiFile ();

            connection = new HttpPeerConnection (rig.Manager, rig.Engine.Settings.WebSeedConnectionTimeout, rig.Engine.Factories, new Uri (ListenerURL));
            rig.Manager.UnhashedPieces.SetAll (false);

            id = new PeerId (new Peer (new PeerInfo (connection.Uri, "this is my id")), connection, new BitField (rig.Manager.Torrent.PieceCount ()).SetAll (true), rig.Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, Software.Synthetic);
            id.IsChoking = false;
            id.AmInterested = true;
            id.MaxPendingRequests = numberOfPieces;
            id.MessageQueue.SetReady ();

            rig.Manager.PieceManager.AddPieceRequests (id);
            requests = (RequestBundle) id.MessageQueue.TryDequeue ();
        }

        [TearDown]
        public void TearDown ()
        {
            listener.Close ();
            rig.Dispose ();
        }

        [Test]
        public void Cancel_ReceiveFirst ()
        {
            using var releaser = MemoryPool.Default.Rent (100, out Memory<byte> buffer);
            var task = connection.ReceiveAsync (buffer).AsTask ();
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public void Cancel_SendFirst ()
        {
            using var releaser = MemoryPool.Default.Rent (requests.ByteLength, out Memory<byte> sendBuffer);
            requests.Encode (sendBuffer.Span);
            var task = connection.SendAsync (sendBuffer).AsTask ();
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public void Cancel_SendAndReceiveFirst ()
        {
            using var r1 = MemoryPool.Default.Rent (requests.ByteLength, out Memory<byte> sendBuffer);
            using var r2 = MemoryPool.Default.Rent (100000, out Memory<byte> receiveBuffer);

            requests.Encode (sendBuffer.Span);

            var sendTask = connection.SendAsync (sendBuffer).AsTask ();
            var receiveTask = connection.ReceiveAsync (receiveBuffer);
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => sendTask, "#1");
            Assert.CatchAsync<OperationCanceledException> (async () => {
                await receiveTask;
                await connection.ReceiveAsync (receiveBuffer);
            }, "#2");
        }

        [Test]
        public void CreatePeerIds ()
        {
            var ids = new HashSet<BEncodedString> ();
            for (int i = 0; i < 20; i++) {
                var id = Mode.CreatePeerId ();
                Assert.AreEqual (20, id.Span.Length, "#1");
                Assert.IsTrue (ids.Add (id), "#2");
            }
        }

        [Test]
        public async Task TestPartialData ()
        {
            partialData = true;
            bool success = false;
            try {
                await ReceiveFirst ();
                success = true;
            } catch {

            }
            // We can't assert on the exception type as it's an internal type.
            Assert.IsFalse (success);
        }

        [Test]
        public async Task TestInactiveServer ()
        {
            ((HttpPeerConnection) connection).ConnectionTimeout = TimeSpan.FromMilliseconds (100);
            listener.Stop ();

            try {
                await ReceiveFirst ();
                Assert.Fail ("An OperationCanceledException or TaskCanceledException should have been raised");
            } catch {
                // We get an OperationCanceledException from the built-in SocketsHttpHandler implmentation.
                //
                // Under any old version of the .NET framework, which uses the StandardSocketsHttpHandler nuget,
                // the TaskCanceledException is wrapped by a HttpRequestException.
            }
        }

        [Test]
        public async Task ReceiveFirst ()
        {
            using var releaser = new MemoryPool ().Rent (1024 * 1024 * 3, out Memory<byte> buffer);
            using var sendReleaser = new MemoryPool ().Rent (requests.ByteLength, out Memory<byte> sendBuffer);
            requests.Encode (sendBuffer.Span);

            var receiveTask = NetworkIO.ReceiveAsync (connection, buffer.Slice (0, 4), null, null, null);
            var task = Send (sendBuffer);

            await receiveTask;
            await CompleteSendOrReceiveFirst (buffer);
            await task;
        }

        [Test]
        public async Task SendFirst ()
        {
            using var r1 = MemoryPool.Default.Rent (1024 * 1024 * 3, out Memory<byte> receiveBuffer);
            using var r2 = MemoryPool.Default.Rent (requests.ByteLength, out Memory<byte> sendBuffer);

            requests.Encode (sendBuffer.Span);

            var task = Send (sendBuffer.Slice (0, requests.ByteLength));
            var receiveTask = connection.ReceiveAsync (receiveBuffer.Slice (0, 4));

            await receiveTask;
            await CompleteSendOrReceiveFirst (receiveBuffer);
            await task;
        }

        [Test]
        public void ChunkedRequest ()
        {
            rig.Manager.PieceManager.CancelRequests (id);
            while (id.MessageQueue.QueueLength > 0)
                id.MessageQueue.TryDequeue ();

            rig.Manager.PieceManager.AddPieceRequests (id);
            requests = (RequestBundle) id.MessageQueue.TryDequeue ();

            using var releaser = MemoryPool.Default.Rent (requests.ByteLength, out Memory<byte> buffer);
            requests.Encode (buffer.Span);
            var sendTask = Send (buffer, 1);

            Assert.ThrowsAsync<ArgumentException> (() => sendTask);
        }

        [Test]
        public void CompleteChunkBeforeRequestNext ()
        {
            var messages = new List<RequestMessage> ();
            var requestsBuffer = requests.Encode ();
            while (requestsBuffer.Length > 0) {
                var message = (RequestMessage) PeerMessage.DecodeMessage (requestsBuffer.Span, null).message;
                messages.Add (message);
                requestsBuffer = requestsBuffer.Slice (message.ByteLength);
            }

            for (int i = 0; i < messages.Count - 1; i++) {
                rig.Manager.PieceManager.PieceDataReceived (id, new PieceMessage (messages[i].PieceIndex, messages[i].StartOffset, messages[i].RequestLength), out _, new HashSet<IRequester> ());
                int orig = id.AmRequestingPiecesCount;
                rig.Manager.PieceManager.AddPieceRequests (id);
                Assert.AreEqual (orig, id.AmRequestingPiecesCount, "#1." + i);
            }

            rig.Manager.PieceManager.PieceDataReceived (id, new PieceMessage (messages.Last ().PieceIndex, messages.Last ().StartOffset, messages.Last ().RequestLength), out _, new HashSet<IRequester> ());
            Assert.AreEqual (0, id.AmRequestingPiecesCount, "#2");

            rig.Manager.PieceManager.AddPieceRequests (id);
            Assert.AreNotEqual (0, id.AmRequestingPiecesCount, "#3");
        }

        [Test]
        public void MultipleChunkedRequests ()
        {
            ChunkedRequest ();
            ChunkedRequest ();
            ChunkedRequest ();
        }

        async Task Send (Memory<byte> buffer, int maxBytesPerChunk = -1)
        {
            if (maxBytesPerChunk == -1) {
                await NetworkIO.SendAsync (connection, buffer, null, null, null);
            } else {
                while (buffer.Length > 0) {
                    var toSend = Math.Min (maxBytesPerChunk, buffer.Length);
                    await NetworkIO.SendAsync (connection, buffer.Slice (0, toSend), null, null, null);
                    buffer = buffer.Slice (toSend);
                }
            }
        }

        private async Task CompleteSendOrReceiveFirst (Memory<byte> buffer)
        {
            var allRequests = new List<RequestMessage> ();
            var requestsBuffer = requests.Encode ();
            while (requestsBuffer.Length > 0) {
                var message = (RequestMessage) PeerMessage.DecodeMessage (requestsBuffer.Span, null).message;
                allRequests.Add (message);
                requestsBuffer = requestsBuffer.Slice (message.ByteLength);
            }

            while (allRequests.Count > 0) {
                int size = Message.ReadInt (buffer.Span);

                await NetworkIO.ReceiveAsync (connection, buffer.Slice (4, size), null, null, null);

                PieceMessage m = (PieceMessage) PeerMessage.DecodeMessage (buffer.Span.Slice (0, size + 4), rig.Manager).message;
                var request = allRequests[0];
                Assert.AreEqual (request.PieceIndex, m.PieceIndex, "#1");
                Assert.AreEqual (request.RequestLength, m.RequestLength, "#1");
                Assert.AreEqual (request.StartOffset, m.StartOffset, "#1");

                for (int i = 0; i < request.RequestLength; i++)
                    if (buffer.Span[i + 13] != (byte) (m.PieceIndex * rig.Torrent.PieceLength + m.StartOffset + i))
                        throw new Exception ("Corrupted data received");

                allRequests.RemoveAt (0);

                if (allRequests.Count == 0) {
                    break;
                } else {
                    await NetworkIO.ReceiveAsync (connection, buffer.Slice (0, 4), null, null, null);;
                }
            }

            Uri baseUri = new Uri (ListenerURL);
            baseUri = new Uri (baseUri, $"{rig.Manager.Torrent.Name}/");
            if (rig.Manager.Torrent.Files.Count > 1) {
                Assert.AreEqual (new Uri (baseUri, rig.Manager.Torrent.Files[0].Path), requestedUrl[0]);
                Assert.AreEqual (new Uri (baseUri, rig.Manager.Torrent.Files[1].Path), requestedUrl[1]);
            }
        }

        private readonly List<string> requestedUrl = new List<string> ();
        private void GotContext (IAsyncResult result)
        {
            try {
                if (result.AsyncState != listener)
                    throw new Exception ("give up");

                HttpListenerContext c = ((HttpListener) result.AsyncState).EndGetContext (result);
                requestedUrl.Add (c.Request.Url.OriginalString);
                Match match = null;
                string range = c.Request.Headers["range"];

                if (!(range != null && (match = rangeMatcher.Match (range)).Success))
                    Assert.Fail ("No valid range specified");

                int start = int.Parse (match.Groups[1].Captures[0].Value);
                int end = int.Parse (match.Groups[2].Captures[0].Value);


                long globalStart = 0;
                bool exists = false;
                string p = rig.Manager.Torrent.Files.Count > 1
                    ? c.Request.RawUrl.Substring (10 + rig.Torrent.Name.Length + 1)
                    : c.Request.RawUrl.Substring (10);
                foreach (TorrentFile file in rig.Manager.Torrent.Files) {
                    if (file.Path.Replace ('\\', '/') != p) {
                        globalStart += file.Length;
                        continue;
                    }
                    globalStart += start;
                    exists = start < file.Length && end < file.Length;
                    break;
                }

                var files = rig.Manager.Torrent.Files;
                if (files.Count == 1 && rig.Torrent.HttpSeeds[0] == c.Request.Url) {
                    globalStart = 0;
                    exists = start < files[0].Length && end < files[0].Length;
                }

                if (!exists) {
                    c.Response.StatusCode = (int) HttpStatusCode.RequestedRangeNotSatisfiable;
                    c.Response.Close ();
                } else {
                    byte[] data = partialData ? new byte[(end - start) / 2] : new byte[end - start + 1];
                    for (int i = 0; i < data.Length; i++)
                        data[i] = (byte) (globalStart + i);

                    c.Response.Close (data, false);
                }

            } catch {
            } finally {
                try {
                    if (result.AsyncState == listener)
                        ((HttpListener) result.AsyncState).BeginGetContext (GotContext, result.AsyncState);
                } catch {

                }
            }
        }

        [Test]
        public async Task SingleFileTorrent ()
        {
            rig.Dispose ();
            rig = TestRig.CreateSingleFile ();
            rig.Torrent.HttpSeeds.Add (new Uri ($"{ListenerURL}File1.exe"));

            Uri url = rig.Torrent.HttpSeeds[0];
            connection = new HttpPeerConnection (rig.Manager, rig.Engine.Settings.WebSeedConnectionTimeout, rig.Engine.Factories, url);
            rig.Manager.UnhashedPieces.SetAll (false);

            id = new PeerId (new Peer (new PeerInfo (connection.Uri, "this is my id")), id.Connection, new BitField (rig.Manager.Torrent.PieceCount ()).SetAll (true), rig.Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, Software.Synthetic);
            id.IsChoking = false;
            id.AmInterested = true;
            id.MaxPendingRequests = numberOfPieces;
            id.MessageQueue.SetReady ();

            rig.Manager.PieceManager.AddPieceRequests (id);
            requests = (RequestBundle) id.MessageQueue.TryDequeue ();
            await ReceiveFirst ();
            Assert.AreEqual (url, requestedUrl[0]);
        }

        void Wait (WaitHandle handle)
        {
            Assert.IsTrue (handle.WaitOne (5000, true), "WaitHandle did not trigger");
        }
    }
}
