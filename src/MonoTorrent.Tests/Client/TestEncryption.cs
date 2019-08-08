using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Threading;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using MonoTorrent.Client.Encryption;
using System.Threading.Tasks;

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

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile();
        }
        [SetUp]
        public void Setup()
        {
            conn = new ConnectionPair(13253);
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

        [OneTimeTearDown]
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
        public void Full_HeaderTestNoInitial()
        {
            try
            {
                Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Header, false);
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                    if (inner is EncryptionException)
                        return;
            }
        }
        [Test]
        public void Full_HeaderTestInitial()
        {
            try
            {
                Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Header, true);
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                    if (inner is EncryptionException)
                        return;
            }
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
        public void Full_NoneTestNoInitial()
        {
            try
            {
                Handshake(EncryptionTypes.RC4Full, EncryptionTypes.PlainText, false);
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                    if (inner is EncryptionException)
                        return;
            }
        }
        [Test]
        public void Full_NoneTestInitial()
        {
            try
            {
                Handshake(EncryptionTypes.RC4Full, EncryptionTypes.PlainText, true);
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                    if (inner is EncryptionException)
                        return;
            }
        }

        [Test]
        public async Task EncrytorFactoryPeerAFullInitial()
        {
            await PeerATest(EncryptionTypes.RC4Full, true);
        }
        [Test]
        public async Task EncrytorFactoryPeerAFullNoInitial()
        {
            await PeerATest(EncryptionTypes.RC4Full, false);
        }

        [Test]
        public async Task EncrytorFactoryPeerAHeaderNoInitial()
        {
            await PeerATest(EncryptionTypes.RC4Header, false);
        }
        [Test]
        public async Task EncrytorFactoryPeerAHeaderInitial()
        {
            await PeerATest(EncryptionTypes.RC4Header, true);
        }

        [Test]
        public async Task EncryptorFactoryPeerAPlain()
        {
            await rig.Engine.StartAll();

            rig.AddConnection(conn.Incoming);

            HandshakeMessage message = new HandshakeMessage(rig.Manager.InfoHash, "ABC123ABC123ABC123AB", VersionInfo.ProtocolStringV100);
            byte[] buffer = message.Encode();

            await conn.Outgoing.SendAsync(buffer, 0, buffer.Length);
            await conn.Outgoing.ReceiveAsync(buffer, 0, buffer.Length);

            message.Decode(buffer, 0, buffer.Length);
            Assert.AreEqual(VersionInfo.ProtocolStringV100, message.ProtocolString);
        }

        [Test]
        public async Task EncrytorFactoryPeerBFull()
        {
            rig.Engine.Settings.PreferEncryption = true;
            await PeerBTest(EncryptionTypes.RC4Full);
        }

        [Test]
        public async Task EncrytorFactoryPeerBHeader()
        {
            rig.Engine.Settings.PreferEncryption = false;
            await PeerBTest(EncryptionTypes.RC4Header);
        }

        async Task PeerATest(EncryptionTypes encryption, bool addInitial)
        {
            rig.Engine.Settings.AllowedEncryption = encryption;
            await rig.Engine.StartAll();

            HandshakeMessage message = new HandshakeMessage(rig.Manager.InfoHash, "ABC123ABC123ABC123AB", VersionInfo.ProtocolStringV100);
            byte[] buffer = message.Encode();
            PeerAEncryption a = new PeerAEncryption(rig.Manager.InfoHash, encryption);
            if (addInitial)
                a.AddPayload(buffer);

            rig.AddConnection(conn.Incoming);
            var result = a.HandshakeAsync(conn.Outgoing);
            if (!result.Wait (4000))
                Assert.Fail("Handshake timed out");

            if (!addInitial)
            {
                a.Encryptor.Encrypt(buffer);
                await conn.Outgoing.SendAsync(buffer, 0, buffer.Length);
            }
         
            int received = await conn.Outgoing.ReceiveAsync(buffer, 0, buffer.Length);
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

        async Task PeerBTest(EncryptionTypes encryption)
        {
            rig.Engine.Settings.AllowedEncryption = encryption;
            await rig.Engine.StartAll();
            rig.AddConnection(conn.Outgoing);

            PeerBEncryption a = new PeerBEncryption(new InfoHash[] { rig.Manager.InfoHash }, EncryptionTypes.All);
            var result = a.HandshakeAsync(conn.Incoming);
            if (!result.Wait(4000))
                Assert.Fail("Handshake timed out");

            HandshakeMessage message = new HandshakeMessage();
            byte[] buffer = new byte[68];

            await conn.Incoming.ReceiveAsync(buffer, 0, buffer.Length);

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
            HandshakeMessage m = new HandshakeMessage(rig.Torrent.InfoHash, "12345123451234512345", VersionInfo.ProtocolStringV100);
            byte[] handshake = m.Encode();

            PeerAEncryption a = new PeerAEncryption(rig.Torrent.InfoHash, encryptionA);
            
            if (addInitial)
                a.AddPayload(handshake);

            PeerBEncryption b = new PeerBEncryption(new InfoHash[] { rig.Torrent.InfoHash }, encryptionB);

            var resultA = a.HandshakeAsync(conn.Outgoing);
            var resultB = b.HandshakeAsync(conn.Incoming);

            if (!Task.WhenAll (resultA, resultB).Wait (5000))
                    Assert.Fail("Could not handshake");


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
