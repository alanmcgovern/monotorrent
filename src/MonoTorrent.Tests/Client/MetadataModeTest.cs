using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class MetadataModeTests
    {
        //static void Main(string[] args)
        //{
        //    MetadataModeTests t = new MetadataModeTests();
        //   t.SendMetadata_ToFolder();
        //}

        IEncryption decryptor = new PlainTextEncryption();
        IEncryption encryptor = new PlainTextEncryption();

        private ConnectionPair pair;
        private TestRig rig;

        public void Setup(bool metadataMode, string metadataPath)
        {
            pair = new ConnectionPair(55432);
            rig = TestRig.CreateSingleFile(1024 * 1024 * 1024, 32768, metadataMode);
            rig.MetadataPath = metadataPath;
            rig.RecreateManager();

            rig.Manager.HashChecked = true;
            rig.Manager.Start();
            rig.AddConnection(pair.Outgoing);

            var connection = pair.Incoming;
            PeerId id = new PeerId(new Peer("", connection.Uri), rig.Manager);
            id.Connection = connection;
            byte[] data;

            EncryptorFactory.EndCheckEncryption(EncryptorFactory.BeginCheckEncryption(id, 68, null, null, new InfoHash[] { id.TorrentManager.InfoHash }), out data);
            decryptor = id.Decryptor;
            encryptor = id.Encryptor;
        }

        [TearDown]
        public void Teardown()
        {
            rig.Manager.Stop();
            pair.Dispose();
            rig.Dispose();
        }

        [Test]
        public void RequestMetadata()
        {
            Setup(false, "path.torrent");
            CustomConnection connection = pair.Incoming;

            // 1) Send local handshake. We've already received the remote handshake as part
            // of the Connect method.
            SendMessage(new HandshakeMessage(rig.Manager.Torrent.infoHash, new string('g', 20), VersionInfo.ProtocolStringV100, true, true), connection);
            ExtendedHandshakeMessage exHand = new ExtendedHandshakeMessage(rig.TorrentDict.LengthInBytes());
            exHand.Supports.Add(LTMetadata.Support);
            SendMessage(exHand, connection);

            // 2) Send all our metadata requests
            int length = (rig.TorrentDict.LengthInBytes() + 16383) / 16384;
            for (int i = 0; i < length; i++)
                SendMessage(new LTMetadata(LTMetadata.Support.MessageId, LTMetadata.eMessageType.Request, i, null), connection);
            // 3) Receive all the metadata chunks
            PeerMessage m;
            var stream = new MemoryStream();
            while (length > 0 && (m = ReceiveMessage(connection)) != null)
            {
                LTMetadata metadata = m as LTMetadata;
                if (metadata != null)
                {
                    if (metadata.MetadataMessageType == LTMetadata.eMessageType.Data)
                    {
                        stream.Write(metadata.MetadataPiece, 0, metadata.MetadataPiece.Length);
                        length--;
                    }
                }
            }

            // 4) Verify the hash is the same.
            stream.Position = 0;
            Assert.AreEqual(rig.Torrent.InfoHash, new InfoHash(new SHA1Managed().ComputeHash(stream)), "#1");
        }

        [Test]
        public void SendMetadata_ToFile()
        {
            Setup(true, "file.torrent");
            SendMetadataCore("file.torrent");
        }

        [Test]
        public void SendMetadata_ToFolder()
        {
            Setup(true, Environment.CurrentDirectory);
            SendMetadataCore(Path.Combine(Environment.CurrentDirectory, rig.Torrent.InfoHash.ToHex () + ".torrent"));
        }

        public void SendMetadataCore (string expectedPath)
        {
            CustomConnection connection = pair.Incoming;

            // 1) Send local handshake. We've already received the remote handshake as part
            // of the Connect method.
            SendMessage(new HandshakeMessage(rig.Manager.InfoHash, new string('g', 20), VersionInfo.ProtocolStringV100, true, true), connection);
            ExtendedHandshakeMessage exHand = new ExtendedHandshakeMessage(rig.Torrent.Metadata.Length);
            exHand.Supports.Add(LTMetadata.Support);
            SendMessage(exHand, connection);

            // 2) Receive the metadata requests from the other peer and fulfill them
            byte[] buffer = rig.Torrent.Metadata;
            int length = (buffer.Length + 16383) / 16384;
            PeerMessage m;
            while (length > 0 && (m = ReceiveMessage(connection)) != null)
            {
                LTMetadata metadata = m as LTMetadata;
                if (metadata != null)
                {
                    if (metadata.MetadataMessageType == LTMetadata.eMessageType.Request)
                    {
                        metadata = new LTMetadata (LTMetadata.Support.MessageId, LTMetadata.eMessageType.Data, metadata.Piece, buffer);
                        SendMessage(metadata, connection);
                        length--;
                    }
                }
            }

            // We've sent all the pieces. Now we just wait for the torrentmanager to process them all.
            while (rig.Manager.Mode is MetadataMode)
                System.Threading.Thread.Sleep(10);

            Assert.IsTrue(File.Exists(expectedPath), "#1");
            Torrent torrent = Torrent.Load(expectedPath);
            Assert.AreEqual(rig.Manager.InfoHash, torrent.InfoHash, "#2");
        }

        private void SendMessage(PeerMessage message, CustomConnection connection)
        {
            byte[] b = message.Encode();
            encryptor.Encrypt(b);
            IAsyncResult result = connection.BeginSend(b, 0, b.Length, null, null);
            if (!result.AsyncWaitHandle.WaitOne(5000, true))
                throw new Exception("Message didn't send correctly");
            connection.EndSend(result);
        }

        private PeerMessage ReceiveMessage(CustomConnection connection)
        {
            return TransferTest.ReceiveMessage(connection, decryptor, rig.Manager);
        }
    }
}

