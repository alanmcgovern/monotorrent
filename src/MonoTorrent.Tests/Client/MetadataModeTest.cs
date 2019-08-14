//
// MetadataModeTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class MetadataModeTests
    {
        IEncryption decryptor = PlainTextEncryption.Instance;
        IEncryption encryptor = PlainTextEncryption.Instance;

        private ConnectionPair pair;
        private TestRig rig;

        public async Task Setup(bool metadataMode, string metadataPath)
        {
            pair = new ConnectionPair(55432);
            rig = TestRig.CreateSingleFile(1024 * 1024 * 1024, 32768, metadataMode);
            rig.MetadataPath = metadataPath;
            rig.RecreateManager().Wait();

            rig.Manager.HashChecked = true;
            await rig.Manager.StartAsync();
            rig.AddConnection(pair.Outgoing);

            var connection = pair.Incoming;
            PeerId id = new PeerId(new Peer("", connection.Uri), rig.Manager);
            id.Connection = connection;


            byte[] data = EncryptorFactory.CheckEncryptionAsync(id, 68, new InfoHash[] { id.TorrentManager.InfoHash }).Result;
            decryptor = id.Decryptor;
            encryptor = id.Encryptor;
        }

        [TearDown]
        public async Task Teardown()
        {
            await rig.Manager.StopAsync();
            pair.Dispose();
            rig.Dispose();
        }

        [Test]
        public async Task RequestMetadata()
        {
            await Setup(false, "path.torrent");
            CustomConnection connection = pair.Incoming;

            // 1) Send local handshake. We've already received the remote handshake as part
            // of the Connect method.
            SendMessage(new HandshakeMessage(rig.Manager.Torrent.InfoHash, new string('g', 20), VersionInfo.ProtocolStringV100, true, true), connection);
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
        public async Task SendMetadata_ToFile()
        {
            var torrent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file.torrent");
            await Setup(true, torrent);
            SendMetadataCore(torrent);
        }

        [Test]
        public async Task SendMetadata_ToFile_CorruptFileExists ()
        {
            var torrent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file.torrent");
            File.Create (torrent).Close ();
            await Setup(true, torrent);
            SendMetadataCore(torrent);
        }

        [Test]
        public async Task SendMetadata_ToFile_RealFileExists ()
        {
            var torrent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file.torrent");
            await Setup(true, torrent);
            File.WriteAllBytes (torrent, rig.Torrent.ToBytes ());

            SendMetadataCore(torrent);
        }

        [Test]
        public async Task SendMetadata_ToFolder()
        {
            await Setup(true, AppDomain.CurrentDomain.BaseDirectory);
            SendMetadataCore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rig.Torrent.InfoHash.ToHex () + ".torrent"));
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
            var sendTask = connection.SendAsync(b, 0, b.Length);
            if (!sendTask.Wait(5000))
                throw new Exception("Message didn't send correctly");
            GC.KeepAlive (sendTask.Result);
        }

        private PeerMessage ReceiveMessage(CustomConnection connection)
        {
            return TransferTest.ReceiveMessage(connection, decryptor, rig.Manager);
        }
    }
}

