using System;
using System.Threading;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Tests.Client
{
    public class TestEncryption : IDisposable
    {
        public TestEncryption()
        {
            rig = TestRig.CreateMultiFile();

            conn = new ConnectionPair(13253);
            conn.Incoming.Name = "Incoming";
            conn.Outgoing.Name = "Outgoing";
            rig.Engine.Settings.AllowedEncryption = EncryptionTypes.All;
            rig.Manager.HashChecked = true;
        }

        public void Dispose()
        {
            conn.Dispose();
            rig.Engine.StopAll();

            for (var i = 0; i < 1000; i++)
            {
                Thread.Sleep(4);
                var result = true;
                foreach (var torrent in rig.Engine.Torrents)
                    result &= torrent.State == TorrentState.Stopped;

                if (result)
                {
                    rig.Dispose();
                    return;
                }
            }

            Assert.True(false, "Timed out waiting for handle");
        }

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

        private readonly TestRig rig;
        private readonly ConnectionPair conn;

        private void PeerATest(EncryptionTypes encryption, bool addInitial)
        {
            rig.Engine.Settings.AllowedEncryption = encryption;
            rig.Engine.StartAll();

            var message = new HandshakeMessage(rig.Manager.InfoHash, "ABC123ABC123ABC123AB",
                VersionInfo.ProtocolStringV100);
            var buffer = message.Encode();
            var a = new PeerAEncryption(rig.Manager.InfoHash, encryption);
            if (addInitial)
                a.AddPayload(buffer);

            rig.AddConnection(conn.Incoming);
            var result = a.BeginHandshake(conn.Outgoing, null, null);
            if (!result.AsyncWaitHandle.WaitOne(4000, true))
                Assert.True(false, "Handshake timed out");
            a.EndHandshake(result);

            if (!addInitial)
            {
                a.Encryptor.Encrypt(buffer);
                conn.Outgoing.EndSend(conn.Outgoing.BeginSend(buffer, 0, buffer.Length, null, null));
            }

            var received = conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(buffer, 0, buffer.Length, null, null));
            Assert.Equal(68, received);

            a.Decryptor.Decrypt(buffer);
            message.Decode(buffer, 0, buffer.Length);
            Assert.Equal(VersionInfo.ProtocolStringV100, message.ProtocolString);

            if (encryption == EncryptionTypes.RC4Full)
                Assert.True(a.Encryptor is RC4);
            else if (encryption == EncryptionTypes.RC4Header)
                Assert.True(a.Encryptor is RC4Header);
            else if (encryption == EncryptionTypes.PlainText)
                Assert.True(a.Encryptor is RC4Header);
        }

        private void PeerBTest(EncryptionTypes encryption)
        {
            rig.Engine.Settings.AllowedEncryption = encryption;
            rig.Engine.StartAll();
            rig.AddConnection(conn.Outgoing);

            var a = new PeerBEncryption(new[] {rig.Manager.InfoHash}, EncryptionTypes.All);
            var result = a.BeginHandshake(conn.Incoming, null, null);
            if (!result.AsyncWaitHandle.WaitOne(4000, true))
                Assert.True(false, "Handshake timed out");
            a.EndHandshake(result);

            var message = new HandshakeMessage();
            var buffer = new byte[68];

            conn.Incoming.EndReceive(conn.Incoming.BeginReceive(buffer, 0, buffer.Length, null, null));

            a.Decryptor.Decrypt(buffer);
            message.Decode(buffer, 0, buffer.Length);
            Assert.Equal(VersionInfo.ProtocolStringV100, message.ProtocolString);
            if (encryption == EncryptionTypes.RC4Full)
                Assert.True(a.Encryptor is RC4);
            else if (encryption == EncryptionTypes.RC4Header)
                Assert.True(a.Encryptor is RC4Header);
            else if (encryption == EncryptionTypes.PlainText)
                Assert.True(a.Encryptor is RC4Header);
        }


        private void Handshake(EncryptionTypes encryptionA, EncryptionTypes encryptionB, bool addInitial)
        {
            var doneA = false;
            var doneB = false;

            var m = new HandshakeMessage(rig.Torrent.InfoHash, "12345123451234512345",
                VersionInfo.ProtocolStringV100);
            var handshake = m.Encode();

            var a = new PeerAEncryption(rig.Torrent.InfoHash, encryptionA);

            if (addInitial)
                a.AddPayload(handshake);

            var b = new PeerBEncryption(new[] {rig.Torrent.InfoHash}, encryptionB);

            var resultA = a.BeginHandshake(conn.Outgoing, null, null);
            var resultB = b.BeginHandshake(conn.Incoming, null, null);

            var handles = new[] {resultA.AsyncWaitHandle, resultB.AsyncWaitHandle};
            var count = 1000;
            while (!WaitHandle.WaitAll(handles, 5, true))
            {
                if (!doneA && (doneA = resultA.IsCompleted))
                    a.EndHandshake(resultA);
                if (!doneB && (doneB = resultB.IsCompleted))
                    b.EndHandshake(resultB);

                if (count-- == 0)
                    Assert.True(false, "Could not handshake");
            }
            if (!doneA)
                a.EndHandshake(resultA);
            if (!doneB)
                b.EndHandshake(resultB);

            var d = new HandshakeMessage();
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
            Assert.Equal(m, d);


            if (encryptionA == EncryptionTypes.RC4Full || encryptionB == EncryptionTypes.RC4Full)
            {
                Assert.True(a.Encryptor is RC4);
                Assert.True(b.Encryptor is RC4);
            }
            else if (encryptionA == EncryptionTypes.RC4Header || encryptionB == EncryptionTypes.RC4Header)
            {
                Assert.True(a.Encryptor is RC4Header);
                Assert.True(b.Encryptor is RC4Header);
            }
            else if (encryptionA == EncryptionTypes.PlainText || encryptionB == EncryptionTypes.PlainText)
            {
                Assert.True(a.Encryptor is PlainTextEncryption);
                Assert.True(b.Encryptor is PlainTextEncryption);
            }
        }

        [Fact]
        public void EncryptorFactoryPeerAPlain()
        {
            rig.Engine.StartAll();

            rig.AddConnection(conn.Incoming);

            var message = new HandshakeMessage(rig.Manager.InfoHash, "ABC123ABC123ABC123AB",
                VersionInfo.ProtocolStringV100);
            var buffer = message.Encode();

            conn.Outgoing.EndSend(conn.Outgoing.BeginSend(buffer, 0, buffer.Length, null, null));
            conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(buffer, 0, buffer.Length, null, null));

            message.Decode(buffer, 0, buffer.Length);
            Assert.Equal(VersionInfo.ProtocolStringV100, message.ProtocolString);
        }

        [Fact]
        public void EncrytorFactoryPeerAFullInitial()
        {
            PeerATest(EncryptionTypes.RC4Full, true);
        }

        [Fact]
        public void EncrytorFactoryPeerAFullNoInitial()
        {
            PeerATest(EncryptionTypes.RC4Full, false);
        }

        [Fact]
        public void EncrytorFactoryPeerAHeaderInitial()
        {
            PeerATest(EncryptionTypes.RC4Header, true);
        }

        [Fact]
        public void EncrytorFactoryPeerAHeaderNoInitial()
        {
            PeerATest(EncryptionTypes.RC4Header, false);
        }

        [Fact]
        public void EncrytorFactoryPeerBFull()
        {
            rig.Engine.Settings.PreferEncryption = true;
            PeerBTest(EncryptionTypes.RC4Full);
        }

        [Fact]
        public void EncrytorFactoryPeerBHeader()
        {
            rig.Engine.Settings.PreferEncryption = false;
            PeerBTest(EncryptionTypes.RC4Header);
        }

        [Fact]
        public void Full_AutoTestInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.All, true);
        }

        [Fact]
        public void Full_AutoTestNoInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.All, false);
        }

        [Fact]
        public void Full_FullTestInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Full, true);
        }

        [Fact]
        public void Full_FullTestNoInitial()
        {
            Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Full, false);
        }

        [Fact]
        public void Full_HeaderTestInitial()
        {
            Assert.Throws<EncryptionException>(() => Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Header, true));
        }

        [Fact]
        public void Full_HeaderTestNoInitial()
        {
            Assert.Throws<EncryptionException>(
                () => Handshake(EncryptionTypes.RC4Full, EncryptionTypes.RC4Header, false));
        }

        [Fact]
        public void Full_NoneTestInitial()
        {
            Assert.Throws<EncryptionException>(() => Handshake(EncryptionTypes.RC4Full, EncryptionTypes.PlainText, true));
        }

        [Fact]
        public void Full_NoneTestNoInitial()
        {
            Assert.Throws<EncryptionException>(
                () => Handshake(EncryptionTypes.RC4Full, EncryptionTypes.PlainText, false));
        }
    }
}