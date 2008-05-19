using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrentTests;
using System.Threading;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Encryption.EncryptionTests
{
    [TestFixture]
    public class TestEncryption
    {
        public static void Main(string[] args)
        {
            int i = 0;
            while (true)
            {
                TestEncryption d = new TestEncryption();
                d.Setup(i++);
                d.HandshakeTest();
                d.Teardown();
                if (i==1000)
                    break;
            }
        }
        
        private TestRig rig;
        private ConnectionPair conn;
        [SetUp]
        public void Setup(int count)
        {
            rig = new TestRig("");
            conn = new ConnectionPair(13253);
            conn.Incoming.Name = count.ToString();
            conn.Outgoing.Name = count.ToString();
        }

        [Test]
        public void HandshakeTest()
        {
            ManualResetEvent handle = new ManualResetEvent(false);
            ManualResetEvent handl2 = new ManualResetEvent(false);

            PeerAEncryption a = new PeerAEncryption(rig.Torrent.InfoHash, EncryptionTypes.Auto);
            PeerBEncryption b = new PeerBEncryption(new byte[][] { rig.Torrent.InfoHash }, EncryptionTypes.Auto);

            conn.Incoming.BeginReceiveStarted += delegate { Receive(true); };
            conn.Incoming.EndReceiveStarted += delegate { Receive(false); };
            conn.Incoming.BeginSendStarted += delegate { Send(true); };
            conn.Incoming.EndSendStarted += delegate { Send(false); };

            a.BeginHandshake(conn.Outgoing, delegate (IAsyncResult result) 
                { Console.WriteLine("Outgoing{1} - Successful? {0}", result != null, conn.Outgoing.Name); handl2.Set(); 
                }, null);
            b.BeginHandshake(conn.Incoming, delegate(IAsyncResult result)
                { Console.WriteLine("Incoming{1} - Successful? {0}", result != null, conn.Incoming.Name); handle.Set(); }, null);

            handle.WaitOne();
            handl2.WaitOne();
            //Console.WriteLine();

            HandshakeMessage m = new HandshakeMessage(rig.Torrent.InfoHash, "12345123451234512345", VersionInfo.ProtocolStringV100);
            byte[] handshake = m.Encode();
            try
            {
                a.Encrypt(handshake, 0, handshake.Length);
                b.Decrypt(handshake, 0, handshake.Length);
            }
            catch
            {
                return;
            }
            HandshakeMessage d = new HandshakeMessage();
            d.Decode(handshake, 0, handshake.Length);
            Assert.AreEqual(m, d);
        }

        private int sendCount;
        private int receiveCount;

        private void Send(bool start)
        {
            if (start)
                sendCount++;
            //Console.WriteLine("Send: {0} - {1}", sendCount, start ? "B" : "E");
        }

        private void Receive(bool start)
        {
            if (start)
                receiveCount++;
            //Console.WriteLine("Receive: {0} - {1}", receiveCount, start ? "B" : "E");
        }

        [TearDown]
        public void Teardown()
        {
            conn.Dispose();
            rig.Engine.StopAll()[0].WaitOne();
            rig.Engine.Dispose();
        }
    }
}
