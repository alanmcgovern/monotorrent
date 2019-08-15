//
// TestEncryption.cs
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
using System.Threading.Tasks;

using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client.Encryption
{
    [TestFixture]
    public class TestEncryption
    {
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
            Assert.AreEqual (HandshakeMessage.HandshakeLength, received, "Recived handshake");

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
            byte[] buffer = new byte[HandshakeMessage.HandshakeLength];

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
