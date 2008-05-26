using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Connections;
using System.Net;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client;
using System.Threading;

namespace MonoTorrentTests
{
    [TestFixture]
    public class TestWebSeed
    {
        public readonly int Count = 5;
        static void Main(string[] args)
        {
            TestWebSeed s = new TestWebSeed();
            s.Setup();
            s.TestPieceRequest();
            s.TearDown();
        }
        TestRig rig;
        HttpConnection connection;
        HttpListener listener;
        private RequestMessage m;
        
        [SetUp]
        public void Setup()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:16352/announce/");
            listener.Start();
            listener.BeginGetContext(GotContext, null);
            rig = new TestRig("");
            connection = new HttpConnection(new Uri("http://127.0.0.1:16352/announce/"));
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
            ThreadPool.QueueUserWorkItem(delegate {RequestPieces(); });
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
                        Assert.Fail();
                    received += end;
                }
                for (int i = 0; i < total - 9; i++)
                    if (buffer[i + 9] != (byte)((i+1)*(m.PieceIndex * rig.Torrent.PieceLength + m.StartOffset + i)))
                        Assert.Fail();
            }
        }

        private void RequestPieces()
        {
            for (int i = 0; i < Count; i++)
            {
                m = new RequestMessage(i, i * Piece.BlockSize, Piece.BlockSize);
                connection.EndSend(connection.BeginSend(m.Encode(), 0, m.ByteLength, null, null));
            }
        }

        private void GotContext(IAsyncResult result)
        {
            try
            {
                HttpListenerContext c = listener.EndGetContext(result);
                Console.WriteLine("Got Context");
                byte[] data = new byte[Piece.BlockSize];
                for (int i = 0; i < data.Length; i++)
                    data[i] = (byte)((i + 1) * (m.PieceIndex * rig.Torrent.PieceLength + m.StartOffset + i));

                c.Response.Close(data, true);
                listener.BeginGetContext(GotContext, null);
            }
            catch
            {
            }
        }
    }
}
