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
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class TestWebSeed
    {
        Regex rangeMatcher = new Regex(@"(\d{1,10})-(\d{1,10})");
       
        bool partialData;
        public readonly int Count = 5;
        TestRig rig;
        HttpConnection connection;
        HttpListener listener;
        //private RequestMessage m;
        public const string ListenerURL = "http://127.0.0.1:51423/announce/";

        PeerId id;
        MessageBundle requests;
        int numberOfPieces = 50;

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(ListenerURL);
            listener.Start();

            listener.BeginGetContext(GotContext, listener);
        }

        [OneTimeTearDown]
        public void FixtureTeardown ()
        {
            listener.Close();
        }

        [SetUp]
        public void Setup()
        {
            requestedUrl.Clear();
            partialData = false;

            rig = TestRig.CreateMultiFile();
            connection = new HttpConnection(new Uri(ListenerURL));
            connection.Manager = rig.Manager;

            id = new PeerId(new Peer("this is my id", connection.Uri), rig.Manager);
            id.Connection = connection;
            id.IsChoking = false;
            id.AmInterested = true;
            id.BitField.SetAll(true);
            id.MaxPendingRequests = numberOfPieces;
            
            requests = new MessageBundle (rig.Manager.PieceManager.Picker.PickPiece(id, new List<PeerId>(), numberOfPieces));
        }

        [TearDown]
        public void TearDown()
        {
            rig.Dispose();
        }

        [Test]
        public void Cancel_ReceiveFirst ()
        {
            var task = connection.ReceiveAsync (new byte[100], 0, 100);
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public void Cancel_SendFirst ()
        {
            var task = connection.SendAsync (new MessageBundle (requests).Encode (), 0, requests.ByteLength);
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public void Cancel_SendAndReceiveFirst ()
        {
            var sendTask = connection.SendAsync (new MessageBundle (requests).Encode (), 0, requests.ByteLength);
            var receiveTask = connection.ReceiveAsync (new byte[100000], 0, 100000);
            connection.Dispose ();
            Assert.CatchAsync<OperationCanceledException> (() => sendTask, "#1");
            Assert.CatchAsync<OperationCanceledException> (async () => {
                await receiveTask;
                await connection.ReceiveAsync (new byte[100000], 0, 100000);
            }, "#2");
        }

        [Test]
        public void TestPartialData()
        {
            partialData = true;
            Assert.ThrowsAsync<WebException> (() => RecieveFirst());
        }

        [Test]
        public void TestInactiveServer()
        {
            connection.ConnectionTimeout = TimeSpan.FromMilliseconds(100);
            listener.Stop();

            Assert.ThrowsAsync<WebException> (() => RecieveFirst());
        }

        [Test]
        public async Task RecieveFirst()
        {
            byte[] buffer = new byte[1024 * 1024 * 3];
            var receiveTask = NetworkIO.ReceiveAsync(connection, buffer, 0, 4, null, null, null);
            var task = Send (requests.Encode (), 0, requests.ByteLength);

            await CompleteSendOrReceiveFirst(buffer, receiveTask.ContinueWith (t=> 4));
            await task;
        }

        [Test]
        public async Task SendFirst()
        {
            byte[] buffer = new byte[1024 * 1024 * 3];
            var task = Send (requests.Encode (), 0, requests.ByteLength);
            var receiveTask  = connection.ReceiveAsync (buffer, 0, 4);

            await CompleteSendOrReceiveFirst(buffer, receiveTask);
            await task;
        }

        [Test]
        public void ChunkedRequest()
        {
            if (requests.Messages.Count != 0)
                rig.Manager.PieceManager.Picker.CancelRequests(id);
            
            requests = new MessageBundle (rig.Manager.PieceManager.Picker.PickPiece(id, new List<PeerId>(), 256));

            byte[] sendBuffer = requests.Encode();
            var sendTask = Send (sendBuffer, 0, sendBuffer.Length, 1);

            Assert.ThrowsAsync<ArgumentException>(() => sendTask);
        }

        [Test]
        public void MultipleChunkedRequests()
        {
            ChunkedRequest();
            ChunkedRequest();
            ChunkedRequest();
        }

        async Task Send(byte[] buffer, int offset, int count, int maxBytesPerChunk = -1)
        {
            if (maxBytesPerChunk == -1)
            {
                await NetworkIO.SendAsync(connection, buffer, offset, count, null, null, null);
            }
            else
            {
                while (count > 0)
                {
                    var toSend = Math.Min(maxBytesPerChunk, count);
                    await NetworkIO.SendAsync (connection, buffer, offset, toSend, null, null, null);
                    count -= toSend;
                }
            }
        }

        private async Task CompleteSendOrReceiveFirst(byte[] buffer, Task receiveTask)
        {
            while (requests.Messages.Count > 0)
            {
                await receiveTask;
                int size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 0));

                await NetworkIO.ReceiveAsync (connection, buffer, 4, size, null, null, null);

                PieceMessage m = (PieceMessage)PeerMessage.DecodeMessage(buffer, 0, size + 4, rig.Manager);
                RequestMessage request = (RequestMessage)requests.Messages[0];
                Assert.AreEqual(request.PieceIndex, m.PieceIndex, "#1");
                Assert.AreEqual(request.RequestLength, m.RequestLength, "#1");
                Assert.AreEqual(request.StartOffset, m.StartOffset, "#1");

                for (int i = 0; i < request.RequestLength; i++)
                    if (buffer[i + 13] != (byte)(m.PieceIndex * rig.Torrent.PieceLength + m.StartOffset + i))
                        throw new Exception("Corrupted data received");
                
                requests.Messages.RemoveAt(0);

                if (requests.Messages.Count == 0)
                {
                    break;
                }
                else
                {
                    receiveTask = NetworkIO.ReceiveAsync(connection, buffer, 0, 4, null, null, null);
                }
            }

            Uri baseUri = new Uri(ListenerURL);
            baseUri = new Uri(baseUri, rig.Manager.Torrent.Name + "/");
            if (rig.Manager.Torrent.Files.Length > 1)
            {
                Assert.AreEqual(new Uri(baseUri, rig.Manager.Torrent.Files[0].Path), requestedUrl[0]);
                Assert.AreEqual(new Uri(baseUri, rig.Manager.Torrent.Files[1].Path), requestedUrl[1]);
            }
        }

        private List<string> requestedUrl = new List<string>();
        private void GotContext(IAsyncResult result)
        {
            try
            {
                if (result.AsyncState != listener)
                    throw new Exception ("give up");

                HttpListenerContext c = ((HttpListener) result.AsyncState).EndGetContext(result);
                Console.WriteLine("Got Context");
                requestedUrl.Add(c.Request.Url.OriginalString);
                Match match = null;
                string range = c.Request.Headers["range"];

                if (!(range != null && (match = rangeMatcher.Match(range)).Success))
                    Assert.Fail("No valid range specified");

                int start = int.Parse(match.Groups[1].Captures[0].Value);
                int end = int.Parse(match.Groups[2].Captures[0].Value);


                long globalStart = 0;
                bool exists = false;
                string p;
                if(rig.Manager.Torrent.Files.Length > 1)
                    p = c.Request.RawUrl.Substring(10 + rig.Torrent.Name.Length + 1);
                else
                    p = c.Request.RawUrl.Substring(10);
                foreach (TorrentFile file in rig.Manager.Torrent.Files)
                {
                    if (file.Path.Replace('\\', '/') != p)
                    {
                        globalStart += file.Length;
                        continue;
                    }
                    globalStart += start;
                    exists = start < file.Length && end < file.Length;
                    break;
                }

                TorrentFile[] files = rig.Manager.Torrent.Files;
                if (files.Length == 1 && rig.Torrent.GetRightHttpSeeds[0] == c.Request.Url.OriginalString)
                {
                    globalStart = 0;
                    exists = start < files[0].Length && end < files[0].Length;
                }

                if (!exists)
                {
                    c.Response.StatusCode = (int) HttpStatusCode.RequestedRangeNotSatisfiable;
                    c.Response.Close();
                }
                else
                {
                    byte[] data = partialData ? new byte[(end - start) / 2] : new byte[end - start + 1];
                    for (int i = 0; i < data.Length; i++)
                        data[i] = (byte)(globalStart + i);

                    c.Response.Close(data, false);
                }

            }
            catch
            {
            }
            finally
            {
                try {
                    if (result.AsyncState == listener)
                        ((HttpListener)result.AsyncState).BeginGetContext(GotContext, result.AsyncState);
                } catch {

                }
            }
        }

        [Test]
        public async Task SingleFileTorrent()
        {
            rig.Dispose();
            rig = TestRig.CreateSingleFile();
            string url = rig.Torrent.GetRightHttpSeeds[0];
            connection = new HttpConnection(new Uri (url));
            connection.Manager = rig.Manager;

            id = new PeerId(new Peer("this is my id", connection.Uri), rig.Manager);
            id.Connection = connection;
            id.IsChoking = false;
            id.AmInterested = true;
            id.BitField.SetAll(true);
            id.MaxPendingRequests = numberOfPieces;

            requests = new MessageBundle (rig.Manager.PieceManager.Picker.PickPiece(id, new List<PeerId>(), numberOfPieces));
            await RecieveFirst();
            Assert.AreEqual(url, requestedUrl[0]);
        }

        void Wait(WaitHandle handle)
        {
            Assert.IsTrue(handle.WaitOne(5000, true), "WaitHandle did not trigger");
        }
    }
}
