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

namespace MonoTorrent.Tests
{
    [TestFixture]
    public class MetadataModeTests
    {
        //static void Main(string[] args)
        //{
        //    MetadataModeTests t = new MetadataModeTests();
        //    t.Setup();
        //    t.RequestMetadata();
        //}

        IEncryption decryptor = new PlainTextEncryption();
        IEncryption encryptor = new PlainTextEncryption();

        private ConnectionPair pair;
        private TestRig rig;

        [SetUp]
        public void Setup()
        {
            pair = new ConnectionPair(55432);
            rig = TestRig.CreateSingleFile(1024 * 1024 * 1024, 32768);
            rig.Manager.HashChecked = true;
            rig.Manager.Start();
            rig.AddConnection(pair.Outgoing);
            Connect(pair.Incoming);
        }

        [TearDown]
        public void Teardown()
        {
            rig.Manager.Stop();
            pair.Dispose();
            rig.Dispose();
        }

        public void Connect(CustomConnection connection)
        {
            PeerId id = new PeerId(new Peer("", connection.Uri), rig.Manager);
            id.Connection = connection;
            id.recieveBuffer = new ArraySegment<byte>(new byte[68]);
            byte[] data = id.recieveBuffer.Array;
            id.BytesToRecieve = 68;

            EncryptorFactory.EndCheckEncryption(EncryptorFactory.BeginCheckEncryption(id, null, null, new InfoHash[] { id.TorrentManager.InfoHash }), out data);
            decryptor = id.Decryptor;
            encryptor = id.Encryptor;
        }

        [Test]
        public void RequestMetadata()
        {
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
            byte[] buffer = new byte[4];
            IAsyncResult result = connection.BeginReceive(buffer, 0, 4, null, null);
            if (!result.AsyncWaitHandle.WaitOne(5000, true))
                throw new Exception("Message length didn't receive correctly");
            connection.EndReceive(result);
            decryptor.Decrypt(buffer);

            int count = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, 0));
            byte[] message = new byte[count + 4];
            Buffer.BlockCopy(buffer, 0, message, 0, 4);

            result = connection.BeginReceive(message, 4, count, null, null);
            if (!result.AsyncWaitHandle.WaitOne(5000, true))
                throw new Exception("Message body didn't receive correctly");
            connection.EndReceive(result);
            decryptor.Decrypt(message, 4, count);

            return PeerMessage.DecodeMessage(message, 0, message.Length, rig.Manager);
        }
    }
}

