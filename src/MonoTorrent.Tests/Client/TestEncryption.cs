using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Threading;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
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
        //        try { d.EncryptorFactoryPeerAPlain(); }
        //        catch { Console.WriteLine("******** FAILURE ********"); }
        //        d.Teardown();
        //        if (i++ == 100)
        //            break;
        //    }
        //}
        
        private TestRig rig;
        private ConnectionPair conn;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile();
        }
        [SetUp]
        public void Setup()
        {
            conn = new ConnectionPair(13253);
            conn.Incoming.Name = "Incoming";
            conn.Outgoing.Name = "Outgoing";
            rig.Engine.Settings.AllowedEncryption = EncryptionTypes.All;
            rig.Manager.HashChecked = true;
        }

        [TearDown]
        public void Teardown()
        {
            conn.Dispose();
            rig.Engine.StopAll();

            for (int i = 0; i < 1000; i++)
            {
                System.Threading.Thread.Sleep(4);
                bool result = true;
                foreach (var torrent in rig.Engine.Torrents)
                    result &= torrent.State == TorrentState.Stopped;

                if (result)
                    return;
            }

            Assert.Fail ("Timed out waiting for handle");
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [Test]
        public void Full_FullTestNoInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Full, false);
        }
        [Test]
        public void Full_FullTestInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Full, true);
        }

        [Test]
        [ExpectedException(typeof(EncryptionException))]
        public void Full_HeaderTestNoInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Header, false);
        }
        [Test]
        [ExpectedException(typeof(EncryptionException))]
        public void Full_HeaderTestInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Header, true);
        }

        [Test]
        public void Full_AutoTestNoInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.All, false);
        }
        [Test]
        public void Full_AutoTestInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.All, true);
        }

        [Test]
        [ExpectedException(typeof(EncryptionException))]
        public void Full_NoneTestNoInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.PlainText, false);
        }
        [Test]
        [ExpectedException(typeof(EncryptionException))]
        public void Full_NoneTestInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.PlainText, true);
        }

        [Test]
        public void EncrytorFactoryPeerAFullInitial()
        {
            PeerATest(EncryptionTypes.RC4Full, true);
        }
        [Test]
        public void EncrytorFactoryPeerAFullNoInitial()
        {
            PeerATest(EncryptionTypes.RC4Full, false);
        }

        [Test]
        public void EncrytorFactoryPeerAHeaderNoInitial()
        {
            PeerATest(EncryptionTypes.RC4Header, false);
        }
        [Test]
        public void EncrytorFactoryPeerAHeaderInitial()
        {
            PeerATest(EncryptionTypes.RC4Header, true);
        }

        [Test]
        public void EncryptorFactoryPeerAPlain()
        {
            rig.Engine.StartAll();

            rig.AddConnection(conn.Incoming);

            HandshakeMessage message = new HandshakeMessage(rig.Manager.InfoHash, "ABC123ABC123ABC123AB", VersionInfo.ProtocolStringV100);
            byte[] buffer = message.Encode();

            conn.Outgoing.EndSend(conn.Outgoing.BeginSend(buffer, 0, buffer.Length, null, null));
            conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(buffer, 0, buffer.Length, null, null));

            message.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(VersionInfo.ProtocolStringV100, message.ProtocolString);
        }

        [Test]
        public void EncrytorFactoryPeerBFull()
        {
            rig.Engine.Settings.PreferEncryption = true;
            PeerBTest(EncryptionTypes.RC4Full);
        }

        [Test]
        public void EncrytorFactoryPeerBHeader()
        {
            rig.Engine.Settings.PreferEncryption = false;
            PeerBTest(EncryptionTypes.RC4Header);
        }

        private void PeerATest(EncryptionTypes encryption, bool addInitial)
        {
            rig.Engine.Settings.AllowedEncryption = encryption;
            rig.Engine.StartAll();

            HandshakeMessage message = new HandshakeMessage(rig.Manager.InfoHash, "ABC123ABC123ABC123AB", VersionInfo.ProtocolStringV100);
            byte[] buffer = message.Encode();
            PeerAEncryption a = new PeerAEncryption(rig.Manager.InfoHash, encryption);
            if (addInitial)
                a.AddPayload(buffer);

            rig.AddConnection(conn.Incoming);
            IAsyncResult result = a.BeginHandshake(conn.Outgoing, null, null);
            if (!result.AsyncWaitHandle.WaitOne(4000, true))
                Assert.Fail("Handshake timed out");
            a.EndHandshake(result);

            if (!addInitial)
            {
                a.Encryptor.Encrypt(buffer);
                conn.Outgoing.EndSend(conn.Outgoing.BeginSend(buffer, 0, buffer.Length, null, null));
            }
         
            int received = conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(buffer, 0, buffer.Length, null, null));
            Assert.AreEqual (68, received, "Recived handshake");

            a.Decryptor.Decrypt(buffer);
            message.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(VersionInfo.ProtocolStringV100, message.ProtocolString);

            if (encryption == EncryptionTypes.RC4Full)
                Assert.IsTrue(a.Encryptor is RC4);
            else if (encryption == EncryptionTypes.RC4Header)
                Assert.IsTrue(a.Encryptor is RC4Header);
            else if (encryption == EncryptionTypes.PlainText)
                Assert.IsTrue(a.Encryptor is RC4Header);
        }

        private void PeerBTest(EncryptionTypes encryption)
        {
            rig.Engine.Settings.AllowedEncryption = encryption;
            rig.Engine.StartAll();
            rig.AddConnection(conn.Outgoing);

            PeerBEncryption a = new PeerBEncryption(new InfoHash[] { rig.Manager.InfoHash }, EncryptionTypes.All);
            IAsyncResult result = a.BeginHandshake(conn.Incoming, null, null);
            if (!result.AsyncWaitHandle.WaitOne(4000, true))
                Assert.Fail("Handshake timed out");
            a.EndHandshake(result);

            HandshakeMessage message = new HandshakeMessage();
            byte[] buffer = new byte[68];

            conn.Incoming.EndReceive(conn.Incoming.BeginReceive(buffer, 0, buffer.Length, null, null));

            a.Decryptor.Decrypt(buffer);
            message.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(VersionInfo.ProtocolStringV100, message.ProtocolString);
            if (encryption == EncryptionTypes.RC4Full)
                Assert.IsTrue(a.Encryptor is RC4);
            else if (encryption == EncryptionTypes.RC4Header)
                Assert.IsTrue(a.Encryptor is RC4Header);
            else if (encryption == EncryptionTypes.PlainText)
                Assert.IsTrue(a.Encryptor is RC4Header);
        }


        private void Handshake(EncryptionTypes encryptionA, EncryptionTypes encryptionB, bool addInitial)
        {
            bool doneA = false;
            bool doneB = false;

            HandshakeMessage m = new HandshakeMessage(rig.Torrent.InfoHash, "12345123451234512345", VersionInfo.ProtocolStringV100);
            byte[] handshake = m.Encode();

            PeerAEncryption a = new PeerAEncryption(rig.Torrent.InfoHash, encryptionA);
            
            if (addInitial)
                a.AddPayload(handshake);

            PeerBEncryption b = new PeerBEncryption(new InfoHash[] { rig.Torrent.InfoHash }, encryptionB);

            IAsyncResult resultA = a.BeginHandshake(conn.Outgoing, null, null);
            IAsyncResult resultB = b.BeginHandshake(conn.Incoming, null, null);

            WaitHandle[] handles = new WaitHandle[] { resultA.AsyncWaitHandle, resultB.AsyncWaitHandle };
            int count = 1000;
            while (!WaitHandle.WaitAll(handles, 5, true))
            {
                if (!doneA && (doneA = resultA.IsCompleted))
                    a.EndHandshake(resultA);
                if (!doneB && (doneB = resultB.IsCompleted))
                    b.EndHandshake(resultB);

                if (count-- == 0)
                    Assert.Fail("Could not handshake");
            }
            if (!doneA)
                a.EndHandshake(resultA);
            if (!doneB)
                b.EndHandshake(resultB);

            HandshakeMessage d = new HandshakeMessage();
            if (!addInitial)
            {
                a.Encrypt(handshake, 0, handshake.Length);
                b.Decrypt(handshake, 0, handshake.Length);
                d.Decode(handshake, 0, handshake.Length);
            }
            else
            {
                d.Decode(b.InitialData, 0, b.InitialData.Length);
            }
            Assert.AreEqual(m, d);


            if (encryptionA == EncryptionTypes.RC4Full || encryptionB == EncryptionTypes.RC4Full)
            {
                Assert.IsTrue(a.Encryptor is RC4);
                Assert.IsTrue(b.Encryptor is RC4);
            }
            else if (encryptionA == EncryptionTypes.RC4Header || encryptionB == EncryptionTypes.RC4Header)
            {
                Assert.IsTrue(a.Encryptor is RC4Header);
                Assert.IsTrue(b.Encryptor is RC4Header);
            }
            else if (encryptionA == EncryptionTypes.PlainText || encryptionB == EncryptionTypes.PlainText)
            {
                Assert.IsTrue(a.Encryptor is PlainTextEncryption);
                Assert.IsTrue(b.Encryptor is PlainTextEncryption);
            }
        }
    }
}
