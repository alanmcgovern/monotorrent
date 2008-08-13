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

namespace MonoTorrentTests
{
    [TestFixture]
    public class TestWebSeed
    {
        static Regex rangeMatcher = new Regex(@"(\d{1,10})-(\d{1,10})");
        static void Main(string[] args)
        {
            TestWebSeed s = new TestWebSeed();
            for (int i = 0; i < 5; i++)
            {
                s.Setup();
                s.Get50Blocks();
                s.TearDown();
            }
        }

        bool partialData;
        public readonly int Count = 5;
        TestRig rig;
        HttpConnection connection;
        HttpListener listener;
        private RequestMessage m;
        private string listenerURL = "http://127.0.0.1:16352/announce/";
        [SetUp]
        public void Setup()
        {
            partialData = false;
            listener = new HttpListener();
            listener.Prefixes.Add(listenerURL);
            listener.Start();
            listener.BeginGetContext(GotContext, null);
            rig = new TestRig("");
            connection = new HttpConnection(new Uri(listenerURL));
            connection.Manager = rig.Manager;
        }

        [TearDown]
        public void TearDown()
        {
            listener.Close();
            rig.Engine.Dispose();
        }

        [Test]
        public void TestPieceRequest()
        {
            ThreadPool.QueueUserWorkItem(delegate { RequestPieces(); });
            byte[] buffer = new byte[1024 * 20];
            for (int j = 0; j < Count; j++)
            {
                IAsyncResult receiveResult = connection.BeginReceive(buffer, 0, 4, null, null);
                int received = connection.EndReceive(receiveResult);

                Assert.AreEqual(4, received, "#1");

                int total = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, 0));

                received = 0;
                while (total != received)
                {
                    int end = connection.EndReceive(connection.BeginReceive(buffer, received, Math.Min(total - received, 2048), null, null));
                    if (end == 0)
                        throw new Exception("Not enough data received");
                    received += end;
                }
                for (int i = 4; i < total - 9 - 4; i++)
                    if (buffer[i + 9] != (byte)(m.PieceIndex * rig.Torrent.PieceLength + m.StartOffset + i))
                        throw new Exception("Corrupted data received");
            }
        }

        [Test]
        [ExpectedException(typeof(Exception))]
        public void TestPartialData()
        {
            partialData = true;
            TestPieceRequest();
        }

        [Test]
        [ExpectedException(typeof(WebException))]
        public void TestInactiveServer()
        {
            listener.Stop();
            TestPieceRequest();
        }

        [Test]
        public void Get50Blocks()
        {
            int numberOfPieces = 50;
            PeerId id = new PeerId(new Peer("this is my id", connection.Uri), rig.Manager);
            id.Connection = connection;
            id.IsChoking = false;
            id.AmInterested = true;
            id.BitField.SetAll(true);
            id.MaxPendingRequests = numberOfPieces;
            MessageBundle bundle = rig.Manager.PieceManager.PickPiece(id, new List<PeerId>(), numberOfPieces);
            AutoResetEvent handle = new AutoResetEvent(false);
            ThreadPool.QueueUserWorkItem(delegate {
                for (int jj = 0; jj < 5; jj++)
                {
                    IAsyncResult r = connection.BeginSend(bundle.Encode(), 0, bundle.ByteLength, null, null);
                    //handle.Set();
                    connection.EndSend(r);
                    //handle.Set();
                }
            });

            for (int kk = 0; kk < 5; kk++)
            {
                //Assert.IsTrue(handle.WaitOne(10000, true), "#a");
                RequestMessage startMessage = (RequestMessage)bundle.Messages[0];
                RequestMessage endMessage = (RequestMessage)bundle.Messages[bundle.Messages.Count - 1];

                System.Threading.Thread.Sleep(10);

                for (int i = 0; i < numberOfPieces; i++)
                {
                    m = (RequestMessage)bundle.Messages[i];
                    PieceMessage piece = ReceiveMessage();
                    Assert.AreEqual(m.PieceIndex, piece.PieceIndex, "#1");
                    Assert.AreEqual(m.RequestLength, piece.RequestLength, "#2");
                    Assert.AreEqual(m.StartOffset, piece.StartOffset, "#3");
                }
                //Assert.IsTrue(handle.WaitOne(10000, true), "#b");
            }
        }

        private PieceMessage ReceiveMessage()
        {
            byte[] buffer = new byte[1024 * 20];
            int received = connection.EndReceive(connection.BeginReceive(buffer, 0, 4, null, null));

            Assert.AreEqual(4, received, "#1");

            int total = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, 0));
            total += 4; // we already have 4, so include that
            
            while (total != received)
            {
                int end = connection.EndReceive(connection.BeginReceive(buffer, received, Math.Min(total - received, 2048), null, null));
                if (end == 0)
                    throw new Exception("Not enough data received");
                received += end;
            }

            for (int i = 4; i < total - 9 - 4; i++)
                if (buffer[i + 13] != (byte)(m.PieceIndex * rig.Torrent.PieceLength + m.StartOffset + i))
                    throw new Exception("Corrupted data received");

            return (PieceMessage)PeerMessage.DecodeMessage(buffer, 0, total, rig.Manager);
        }
        private void RequestPieces()
        {
            try
            {
                for (int i = 0; i < Count; i++)
                {
                    m = new RequestMessage(i, i * Piece.BlockSize, Piece.BlockSize);
                    connection.EndSend(connection.BeginSend(m.Encode(), 0, m.ByteLength, null, null));
                }
            }
            catch
            {
                // This should happen
            }
        }

        private void GotContext(IAsyncResult result)
        {
            try
            {
                HttpListenerContext c = listener.EndGetContext(result);
                Console.WriteLine("Got Context");
                
                Match match;
                string range = c.Request.Headers["range"];
                if (range != null && (match = rangeMatcher.Match(range)) != null)
                {
                    int start = int.Parse(match.Groups[1].Captures[0].Value);
                    int end = int.Parse(match.Groups[2].Captures[0].Value);

                    byte[] data = partialData ? new byte[(end - start) / 2] : new byte[end - start];
                    for (int i = 0; i < data.Length; i++)
                        data[i] = (byte)(start + i);

                    c.Response.OutputStream.Write(data, 0, data.Length);
                    c.Response.Close();
                }
                else
                {
                    Assert.Fail("No valid range specified");
                }
                listener.BeginGetContext(GotContext, null);
            }
            catch
            {
            }
        }
    }
}
