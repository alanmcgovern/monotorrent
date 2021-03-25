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
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;

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
        HttpConnection connection;
        HttpListener listener;
        //private RequestMessage m;
        public string ListenerURL;

        PeerId id;
        RequestBundle requests;
        readonly int numberOfPieces = 50;

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            for (int i = 0; i < 10; i++) {
                try {
                    listener = new HttpListener ();
                    ListenerURL = $"http://127.0.0.1:{new Random ().Next (10000, 50000)}/announce/";
                    listener.Prefixes.Add (ListenerURL);
                    listener.Start ();
                    break;
                } catch {

                }
            }
            listener.BeginGetContext (GotContext, listener);
        }

        [OneTimeTearDown]
        public void FixtureTeardown ()
        {
            listener.Close ();
        }

        [SetUp]
        public void Setup ()
        {
            requestedUrl.Clear ();
            partialData = false;

            rig = TestRig.CreateMultiFile ();

            connection = new HttpConnection (new Uri (ListenerURL));
            connection.Manager = rig.Manager;
            rig.Manager.UnhashedPieces.SetAll (false);

            id = new PeerId (new Peer ("this is my id", connection.Uri), connection, new MutableBitField (rig.Manager.PieceCount ()).SetAll (true));
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
            rig.Dispose ();
        }

        [Test]
        public void Cancel_ReceiveFirst ()
        {
            var task = connection.ReceiveAsync (new ByteBuffer (100), 0, 100).AsTask ();
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public void Cancel_SendFirst ()
        {
            var sendBuffer = new ByteBuffer (new MessageBundle (requests).ByteLength);
            new MessageBundle (requests).Encode (sendBuffer.Data, 0);
            var task = connection.SendAsync (sendBuffer, 0, requests.ByteLength).AsTask ();
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public void Cancel_SendAndReceiveFirst ()
        {
            var sendBuffer = new ByteBuffer (new MessageBundle (requests).ByteLength);
            new MessageBundle (requests).Encode (sendBuffer.Data, 0);

            var sendTask = connection.SendAsync (sendBuffer, 0, requests.ByteLength).AsTask ();
            var receiveTask = connection.ReceiveAsync (new ByteBuffer (100000), 0, 100000);
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => sendTask, "#1");
            Assert.CatchAsync<OperationCanceledException> (async () => {
                await receiveTask;
                await connection.ReceiveAsync (new ByteBuffer (100000), 0, 100000);
            }, "#2");
        }

        [Test]
        public void CreatePeerIds ()
        {
            var ids = new HashSet<BEncodedString> ();
            for (int i = 0; i < 20; i++) {
                var id = HttpConnection.CreatePeerId ();
                Assert.AreEqual (20, id.TextBytes.Length, "#1");
                Assert.IsTrue (ids.Add (id), "#2");
            }
        }

        [Test]
        public void TestPartialData ()
        {
            partialData = true;
            Assert.ThrowsAsync<WebException> (ReceiveFirst);
        }

        [Test]
        public void TestInactiveServer ()
        {
            connection.ConnectionTimeout = TimeSpan.FromMilliseconds (100);
            listener.Stop ();

            Assert.ThrowsAsync<WebException> (ReceiveFirst);
        }

        [Test]
        public async Task ReceiveFirst ()
        {
            var buffer = new ByteBuffer (1024 * 1024 * 3);
            var sendBuffer = new ByteBuffer (requests.ByteLength);
            requests.Encode (sendBuffer.Data, 0);

            var receiveTask = NetworkIO.ReceiveAsync (connection, buffer, 0, 4, null, null, null);
            var task = Send (sendBuffer, 0, requests.ByteLength);

            await receiveTask;
            await CompleteSendOrReceiveFirst (buffer);
            await task;
        }

        [Test]
        public async Task SendFirst ()
        {
            var buffer = new ByteBuffer (1024 * 1024 * 3);
            var sendBuffer = new ByteBuffer (requests.ByteLength);
            requests.Encode (sendBuffer.Data, 0);

            var task = Send (sendBuffer, 0, requests.ByteLength);
            var receiveTask = connection.ReceiveAsync (buffer, 0, 4);

            await receiveTask;
            await CompleteSendOrReceiveFirst (buffer);
            await task;
        }

        [Test]
        public void ChunkedRequest ()
        {
            rig.Manager.PieceManager.CancelRequests (id);

            rig.Manager.PieceManager.AddPieceRequests (id);
            requests = (RequestBundle) id.MessageQueue.TryDequeue ();

            var sendBuffer = new ByteBuffer (requests.ByteLength);
            requests.Encode (sendBuffer.Data, 0);
            var sendTask = Send (sendBuffer, 0, sendBuffer.Data.Length, 1);

            Assert.ThrowsAsync<ArgumentException> (() => sendTask);
        }

        [Test]
        public void CompleteChunkBeforeRequestNext ()
        {
            var messages = requests.ToRequestMessages ().ToList ();
            for (int i = 0; i < messages.Count - 1; i++) {
                rig.Manager.PieceManager.PieceDataReceived (id, new PieceMessage (messages[i].PieceIndex, messages[i].StartOffset, messages[i].RequestLength), out _, out _);
                int orig = id.AmRequestingPiecesCount;
                rig.Manager.PieceManager.AddPieceRequests (id);
                Assert.AreEqual (orig, id.AmRequestingPiecesCount, "#1." + i);
            }

            rig.Manager.PieceManager.PieceDataReceived (id, new PieceMessage (messages.Last ().PieceIndex, messages.Last ().StartOffset, messages.Last ().RequestLength), out _, out _);
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

        async Task Send (ByteBuffer buffer, int offset, int count, int maxBytesPerChunk = -1)
        {
            if (maxBytesPerChunk == -1) {
                await NetworkIO.SendAsync (connection, buffer, offset, count, null, null, null);
            } else {
                while (count > 0) {
                    var toSend = Math.Min (maxBytesPerChunk, count);
                    await NetworkIO.SendAsync (connection, buffer, offset, toSend, null, null, null);
                    count -= toSend;
                }
            }
        }

        private async Task CompleteSendOrReceiveFirst (ByteBuffer buffer)
        {
            var allRequests = requests.ToRequestMessages ().ToList ();
            while (allRequests.Count > 0) {
                int size = IPAddress.NetworkToHostOrder (BitConverter.ToInt32 (buffer.Data, 0));

                await NetworkIO.ReceiveAsync (connection, buffer, 4, size, null, null, null);

                PieceMessage m = (PieceMessage) PeerMessage.DecodeMessage (buffer.Data, 0, size + 4, rig.Manager);
                var request = allRequests[0];
                Assert.AreEqual (request.PieceIndex, m.PieceIndex, "#1");
                Assert.AreEqual (request.RequestLength, m.RequestLength, "#1");
                Assert.AreEqual (request.StartOffset, m.StartOffset, "#1");

                for (int i = 0; i < request.RequestLength; i++)
                    if (buffer.Data[i + 13] != (byte) (m.PieceIndex * rig.Torrent.PieceLength + m.StartOffset + i))
                        throw new Exception ("Corrupted data received");

                allRequests.RemoveAt (0);

                if (allRequests.Count == 0) {
                    break;
                } else {
                    await NetworkIO.ReceiveAsync (connection, buffer, 0, 4, null, null, null);
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
                Console.WriteLine ("Got Context");
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
                if (files.Count == 1 && rig.Torrent.HttpSeeds[0] == c.Request.Url.OriginalString) {
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
            rig.Torrent.HttpSeeds.Add ($"{ListenerURL}File1.exe");

            string url = rig.Torrent.HttpSeeds[0];
            connection = new HttpConnection (new Uri (url));
            connection.Manager = rig.Manager;
            rig.Manager.UnhashedPieces.SetAll (false);

            id = new PeerId (new Peer ("this is my id", connection.Uri), id.Connection, new MutableBitField (rig.Manager.PieceCount ()).SetAll (true));
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
