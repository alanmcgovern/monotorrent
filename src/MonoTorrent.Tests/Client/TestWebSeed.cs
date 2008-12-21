using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Connections;
using System.Net;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client;
using System.Threading;
using MonoTorrent.Client.Messages;
using System.Text.RegularExpressions;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class TestWebSeed
    {
        Regex rangeMatcher = new Regex(@"(\d{1,10})-(\d{1,10})");
        //static void Main(string[] args)
        //{
        //    TestWebSeed s = new TestWebSeed();
        //    for (int i = 0; i < 50; i++)
        //    {
        //        s.Setup();
        //        s.TestPartialData();
        //        s.TearDown();
        //    }
        //}

        bool partialData;
        public readonly int Count = 5;
        TestRig rig;
        HttpConnection connection;
        HttpListener listener;
        //private RequestMessage m;
        private string listenerURL = "http://127.0.0.1:120/announce/";
        int amountSent;

        PeerId id;
        MessageBundle requests;
        int numberOfPieces = 50;

        [SetUp]
        public void Setup()
        {
            requestedUrl.Clear();
            partialData = false;
            int i;
            for (i = 0; i < 1000; i++)
            {
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add(string.Format(listenerURL, i));
                    listener.Start();
                    break;
                }
                catch
                {

                }
            }
            listener.BeginGetContext(GotContext, null);
            rig = new TestRig("");
            connection = new HttpConnection(new Uri(string.Format(listenerURL, i)));
            connection.Manager = rig.Manager;

            id = new PeerId(new Peer("this is my id", connection.Uri), rig.Manager);
            id.Connection = connection;
            id.IsChoking = false;
            id.AmInterested = true;
            id.BitField.SetAll(true);
            id.MaxPendingRequests = numberOfPieces;
            
            requests = rig.Manager.PieceManager.Picker.PickPiece(id, new List<PeerId>(), numberOfPieces);
        }

        [TearDown]
        public void TearDown()
        {
            listener.Close();
            rig.Dispose();
        }

        [Test]
        [ExpectedException(typeof(WebException))]
        public void TestPartialData()
        {
            partialData = true;
            RecieveFirst();
        }

        [Test]
        [ExpectedException(typeof(WebException))]
        public void TestInactiveServer()
        {
            listener.Stop();
            RecieveFirst();
        }

        [Test]
        public void RecieveFirst()
        {
            byte[] buffer = new byte[1024 * 1024 * 3];
            IAsyncResult receiveResult = connection.BeginReceive(buffer, 0, 4, null, null);
            IAsyncResult sendResult = connection.BeginSend(requests.Encode(), 0, requests.ByteLength, null, null);
            amountSent = requests.ByteLength;

            CompleteSendOrReceiveFirst(buffer, receiveResult, sendResult);
        }

        [Test]
        public void SendFirst()
        {
            byte[] buffer = new byte[1024 * 1024 * 3];
            IAsyncResult sendResult = connection.BeginSend(requests.Encode(), 0, requests.ByteLength, null, null);
            IAsyncResult receiveResult = connection.BeginReceive(buffer, 0, 4, null, null);
            amountSent = requests.ByteLength;

            CompleteSendOrReceiveFirst(buffer, receiveResult, sendResult);
        }

        private void CompleteSendOrReceiveFirst(byte[] buffer, IAsyncResult receiveResult, IAsyncResult sendResult)
        {
            int received = 0;
            while ((received = connection.EndReceive(receiveResult)) != 0)
            {
                if (received != 4)
                    throw new Exception("Should be 4 bytes");

                int size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 0));
                received = 0;

                while (received != size)
                    received += connection.EndReceive(connection.BeginReceive(buffer, received + 4, size - received, null, null));

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
                    receiveResult = connection.BeginReceive(buffer, 0, 4, null, null);
                    Assert.AreEqual(connection.EndSend(sendResult), amountSent);
                    break;
                }
                else
                {
                    receiveResult = connection.BeginReceive(buffer, 0, 4, null, null);
                }
            }

            
        }

        private List<string> requestedUrl = new List<string>();
        private void GotContext(IAsyncResult result)
        {
            try
            {
                HttpListenerContext c = listener.EndGetContext(result);
                Console.WriteLine("Got Context");
                requestedUrl.Add(c.Request.Url.OriginalString);
                Match match = null;
                string range = c.Request.Headers["range"];

                if (!(range != null && (match = rangeMatcher.Match(range)) != null))
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

                listener.BeginGetContext(GotContext, null);
            }
            catch
            {
            }
        }

        [Test]
        public void SingleFileTorrent()
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

            requests = rig.Manager.PieceManager.Picker.PickPiece(id, new List<PeerId>(), numberOfPieces);
            RecieveFirst();
            Assert.AreEqual(url, requestedUrl[0]);
        }
    }
}
