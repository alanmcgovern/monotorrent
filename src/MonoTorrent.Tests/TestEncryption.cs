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
        //public static void Main(string[] args)
        //{
        //    int i = 0;
        //    while (true)
        //    {
        //        TestEncryption d = new TestEncryption();
        //        d.Setup();
        //        try { d.Full_AutoTest(); }
        //        catch { Console.WriteLine("******** FAILURE ********"); }
        //        d.Teardown();
        //        if (i == 100)
        //            break;
        //    }
        //}
        
        private TestRig rig;
        private ConnectionPair conn;

        [SetUp]
        public void Setup()
        {
            rig = new TestRig("");
            conn = new ConnectionPair(13253);
            conn.Incoming.Name = "Incoming";
            conn.Outgoing.Name = "Outgoing";
        }

        [Test]
        public void Full_FullTest()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Full);
        }

        [Test]
        [ExpectedException(typeof(EncryptionException))]
        public void Full_HeaderTest()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Header);
        }

        [Test]
        public void Full_AutoTest()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.Auto);
        }

        [Test]
        [ExpectedException(typeof(EncryptionException))]
        public void Full_NoneTest()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.None);
        }

        [Test]
        public void EncrytorFactoryPeerAFull()
        {
            PeerATest(EncryptionTypes.RC4Full);
        }

        [Test]
        public void EncrytorFactoryPeerAHeader()
        {
            PeerATest(EncryptionTypes.RC4Header);
        }

        [Test]
        public void EncryptorFactoryPeerAPlain()
        {
            rig.Engine.StartAll();

            rig.AddConnection(conn.Incoming);

            HandshakeMessage message = new HandshakeMessage(rig.Manager.Torrent.InfoHash, "ABC123ABC123ABC123AB", VersionInfo.ProtocolStringV100);
            byte[] buffer = message.Encode();

            conn.Outgoing.EndSend(conn.Outgoing.BeginSend(buffer, 0, buffer.Length, null, null));
            conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(buffer, 0, buffer.Length, null, null));

            message.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(VersionInfo.ProtocolStringV100, message.ProtocolString);
        }

        [Test]
        public void EncrytorFactoryPeerBFull()
        {
            PeerBTest(EncryptionTypes.RC4Full);
        }

        [Test]
        public void EncrytorFactoryPeerBHeader()
        {
            PeerBTest(EncryptionTypes.RC4Header);
        }

        private void PeerATest(EncryptionTypes encryption)
        {
            rig.Engine.StartAll();
            PeerAEncryption a = new PeerAEncryption(rig.Manager.Torrent.InfoHash, encryption);

            rig.AddConnection(conn.Incoming);
            IAsyncResult result = a.BeginHandshake(conn.Outgoing, null, null);
            if (!result.AsyncWaitHandle.WaitOne(5000, true))
                Assert.Fail("Handshake timed out");
            a.EndHandshake(result);

            HandshakeMessage message = new HandshakeMessage(rig.Manager.Torrent.InfoHash, "ABC123ABC123ABC123AB", VersionInfo.ProtocolStringV100);
            byte[] buffer = message.Encode();
            a.Encryptor.Encrypt(buffer);

            conn.Outgoing.EndSend(conn.Outgoing.BeginSend(buffer, 0, buffer.Length, null, null));
            conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(buffer, 0, buffer.Length, null, null));

            a.Decryptor.Decrypt(buffer);
            message.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(VersionInfo.ProtocolStringV100, message.ProtocolString);
        }

        private void PeerBTest(EncryptionTypes encryption)
        {
            rig.Engine.StartAll();
            rig.AddConnection(conn.Outgoing);

            PeerBEncryption a = new PeerBEncryption(new byte[][] { rig.Manager.Torrent.InfoHash }, encryption);
            IAsyncResult result = a.BeginHandshake(conn.Incoming, null, null);
            if(!result.AsyncWaitHandle.WaitOne(5000, true))
                Assert.Fail("Handshake timed out");
            a.EndHandshake(result);

            HandshakeMessage message = new HandshakeMessage();
            byte[] buffer = new byte[68];

            conn.Incoming.EndReceive(conn.Incoming.BeginReceive(buffer, 0, buffer.Length, null, null));

            a.Decryptor.Decrypt(buffer);
            message.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(VersionInfo.ProtocolStringV100, message.ProtocolString);
        }
 
        [TearDown]
        public void Teardown()
        {
            conn.Dispose();
            rig.Engine.StopAll()[0].WaitOne();
            rig.Engine.Dispose();
        }


        private void Handshake(EncryptionTypes encryptionA, EncryptionTypes encryptionB)
        {
            bool doneA = false;
            bool doneB = false;
            PeerAEncryption a = new PeerAEncryption(rig.Torrent.InfoHash, encryptionA);
            PeerBEncryption b = new PeerBEncryption(new byte[][] { rig.Torrent.InfoHash }, encryptionB);

            IAsyncResult resultA = a.BeginHandshake(conn.Outgoing, null, null);
            IAsyncResult resultB = b.BeginHandshake(conn.Incoming, null, null);

            while (!resultA.AsyncWaitHandle.WaitOne(10, true) || !resultB.AsyncWaitHandle.WaitOne())
            {
                if (!doneA && (doneA = resultA.IsCompleted))
                    a.EndHandshake(resultA);
                if (!doneB && (doneB = resultB.IsCompleted))
                    b.EndHandshake(resultB);
            }
            if (!doneA)
                a.EndHandshake(resultA);
            if (!doneB)
                b.EndHandshake(resultB);

            HandshakeMessage m = new HandshakeMessage(rig.Torrent.InfoHash, "12345123451234512345", VersionInfo.ProtocolStringV100);
            byte[] handshake = m.Encode();

            a.Encrypt(handshake, 0, handshake.Length);
            b.Decrypt(handshake, 0, handshake.Length);

            HandshakeMessage d = new HandshakeMessage();
            d.Decode(handshake, 0, handshake.Length);
            Assert.AreEqual(m, d);
            Console.WriteLine("Success");
        }
    }
}
