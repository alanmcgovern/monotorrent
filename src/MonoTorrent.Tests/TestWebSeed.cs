using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Connections;
using System.Net;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client;

namespace MonoTorrentTests
{
    [TestFixture]
    public class TestWebSeed
    {
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
            byte[] buffer = new byte[1024 * 20];
            m = new RequestMessage(0, 0, Piece.BlockSize);
            m.Encode(buffer, 0);

            IAsyncResult sendResult = connection.BeginSend(buffer, 0, m.ByteLength, null, null);
            IAsyncResult receiveResult = connection.BeginReceive(buffer, 0, 4, null, null);
            int received = connection.EndReceive(receiveResult);

            Assert.AreEqual(4, received, "#1");

            received = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, 0));

            int total = 0;
            while (total != received)
            {
                int end = connection.EndReceive(connection.BeginReceive(buffer, total, Math.Min(received - total, 2048), null, null));
                if (end == 0)
                    Assert.Fail();
                total += end;
            }
            for(int i=0;i<total-9;i++)
                if(buffer[i+9] != (byte)(m.PieceIndex*rig.Torrent.PieceLength + m.StartOffset + i))
                    Assert.Fail();
        }

        private void GotContext(IAsyncResult result)
        {
            HttpListenerContext c = listener.EndGetContext(result);
            byte[] data = new byte[Piece.BlockSize];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(m.PieceIndex * rig.Torrent.PieceLength + m.StartOffset + i);

            c.Response.Close(data, true);
        }
    }
}
