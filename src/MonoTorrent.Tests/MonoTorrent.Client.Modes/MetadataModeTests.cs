//
// MetadataModeTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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

namespace MonoTorrent.Client.Modes
{
    [TestFixture]
    public class MetadataModeTests
    {
        IEncryption decryptor = PlainTextEncryption.Instance;
        IEncryption encryptor = PlainTextEncryption.Instance;

        private ConnectionPair pair;
        private TestRig rig;

        public async Task Setup (bool metadataMode, string metadataPath, bool multiFile = false)
        {
            pair = new ConnectionPair ().WithTimeout ();
            rig = multiFile ? TestRig.CreateMultiFile (32768, metadataMode) : TestRig.CreateSingleFile (1024 * 1024 * 1024, 32768, metadataMode);
            rig.MetadataPath = metadataPath;
            rig.RecreateManager ().Wait ();

            rig.Manager.HashChecked = true;
            await rig.Manager.StartAsync ();
            rig.AddConnection (pair.Outgoing);

            var connection = pair.Incoming;
            PeerId id = new PeerId (new Peer ("", connection.Uri), connection, rig.Manager.Bitfield?.Clone ().SetAll (false));

            var result = await EncryptorFactory.CheckIncomingConnectionAsync (id.Connection, id.Peer.AllowedEncryption, rig.Engine.Settings, new[] { rig.Manager.InfoHash });
            decryptor = id.Decryptor = result.Decryptor;
            encryptor = id.Encryptor = result.Encryptor;
        }

        [TearDown]
        public async Task Teardown ()
        {
            await rig.Manager.StopAsync ();
            pair.Dispose ();
            rig.Dispose ();
        }

        [Test]
        public async Task RequestMetadata ()
        {
            await Setup (false, "path.torrent");
            CustomConnection connection = pair.Incoming;

            // 1) Send local handshake. We've already received the remote handshake as part
            // of the Connect method.
            var sendHandshake = new HandshakeMessage (rig.Manager.Torrent.InfoHash, new string ('g', 20), VersionInfo.ProtocolStringV100, true, true);
            await PeerIO.SendMessageAsync (connection, encryptor, sendHandshake);
            ExtendedHandshakeMessage exHand = new ExtendedHandshakeMessage (false, rig.TorrentDict.LengthInBytes (), 5555);
            exHand.Supports.Add (LTMetadata.Support);
            await PeerIO.SendMessageAsync (connection, encryptor, exHand);

            // 2) Send all our metadata requests
            int length = (rig.TorrentDict.LengthInBytes () + 16383) / 16384;
            for (int i = 0; i < length; i++)
                await PeerIO.SendMessageAsync (connection, encryptor, new LTMetadata (LTMetadata.Support.MessageId, LTMetadata.eMessageType.Request, i, null));
            // 3) Receive all the metadata chunks
            PeerMessage m;
            var stream = new MemoryStream ();
            while (length > 0 && (m = await PeerIO.ReceiveMessageAsync (connection, decryptor)) != null) {
                if (m is LTMetadata metadata) {
                    if (metadata.MetadataMessageType == LTMetadata.eMessageType.Data) {
                        stream.Write (metadata.MetadataPiece, 0, metadata.MetadataPiece.Length);
                        length--;
                    }
                }
            }

            // 4) Verify the hash is the same.
            stream.Position = 0;
            Assert.AreEqual (rig.Torrent.InfoHash, new InfoHash (new SHA1Managed ().ComputeHash (stream)), "#1");
        }

        [Test]
        public async Task SendMetadata_ToFile ()
        {
            var torrent = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "file.torrent");
            await Setup (true, torrent);
            await SendMetadataCore (torrent);
        }

        [Test]
        public async Task SendMetadata_ToFile_CorruptFileExists ()
        {
            var torrent = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "file.torrent");
            File.Create (torrent).Close ();
            await Setup (true, torrent);
            await SendMetadataCore (torrent);
        }

        [Test]
        public async Task SendMetadata_ToFile_RealFileExists ()
        {
            var torrent = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "file.torrent");
            await Setup (true, torrent);
            File.WriteAllBytes (torrent, rig.Torrent.ToBytes ());

            await SendMetadataCore (torrent);
        }

        [Test]
        public async Task SendMetadata_ToFolder ()
        {
            await Setup (true, AppDomain.CurrentDomain.BaseDirectory);
            await SendMetadataCore (Path.Combine (AppDomain.CurrentDomain.BaseDirectory,
                $"{rig.Torrent.InfoHash.ToHex ()}.torrent"));
        }

        [Test]
        public async Task SingleFileSavePath ()
        {
            var torrent = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "file.torrent");
            await Setup (true, torrent, multiFile: false);
            await SendMetadataCore (torrent);

            Assert.AreEqual (@"test.files", rig.Manager.Torrent.Name);
            Assert.AreEqual (@"", rig.Manager.SavePath);

            var torrentFiles = rig.Manager.Torrent.Files;
            Assert.AreEqual (torrentFiles.Length, 1);
            Assert.AreEqual (Path.Combine ("Dir1", "File1"), torrentFiles[0].Path);
            Assert.AreEqual (Path.Combine ("Dir1", "File1"), torrentFiles[0].FullPath);
        }

        [Test]
        public async Task MultiFileSavePath ()
        {
            var torrent = Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "file.torrent");
            await Setup (true, torrent, multiFile: true);
            await SendMetadataCore (torrent);

            Assert.AreEqual (@"test.files", rig.Manager.Torrent.Name);
            Assert.AreEqual (@"test.files", rig.Manager.SavePath);

            var torrentFiles = rig.Manager.Torrent.Files;
            Assert.AreEqual (torrentFiles.Length, 4);
            Assert.AreEqual (Path.Combine ("Dir1", "File1"), torrentFiles[0].Path);
            Assert.AreEqual (Path.Combine ("Dir1", "Dir2", "File2"), torrentFiles[1].Path);
            Assert.AreEqual (@"File3", torrentFiles[2].Path);
            Assert.AreEqual (@"File4", torrentFiles[3].Path);

            Assert.AreEqual (Path.Combine ("test.files", "Dir1", "File1"), torrentFiles[0].FullPath);
            Assert.AreEqual (Path.Combine ("test.files", "Dir1", "Dir2", "File2"), torrentFiles[1].FullPath);
            Assert.AreEqual (Path.Combine ("test.files", "File3"), torrentFiles[2].FullPath);
            Assert.AreEqual (Path.Combine ("test.files", "File4"), torrentFiles[3].FullPath);
        }

        public async Task SendMetadataCore (string expectedPath)
        {
            CustomConnection connection = pair.Incoming;

            // 1) Send local handshake. We've already received the remote handshake as part
            // of the Connect method.
            var sendHandshake = new HandshakeMessage (rig.Manager.InfoHash, new string ('g', 20), VersionInfo.ProtocolStringV100, true, true);
            await PeerIO.SendMessageAsync (connection, encryptor, sendHandshake);
            ExtendedHandshakeMessage exHand = new ExtendedHandshakeMessage (false, rig.Torrent.Metadata.Length, 5555);
            exHand.Supports.Add (LTMetadata.Support);
            await PeerIO.SendMessageAsync (connection, encryptor, exHand);

            // 2) Receive the metadata requests from the other peer and fulfill them
            byte[] buffer = rig.Torrent.Metadata;
            int length = (buffer.Length + 16383) / 16384;
            PeerMessage m;
            while (length > 0 && (m = await PeerIO.ReceiveMessageAsync (connection, decryptor)) != null) {
                if (m is ExtendedHandshakeMessage ex) {
                    Assert.AreEqual (ClientEngine.DefaultMaxPendingRequests, ex.MaxRequests);
                } else if (m is LTMetadata metadata) {
                    if (metadata.MetadataMessageType == LTMetadata.eMessageType.Request) {
                        metadata = new LTMetadata (LTMetadata.Support.MessageId, LTMetadata.eMessageType.Data, metadata.Piece, buffer);
                        await PeerIO.SendMessageAsync (connection, encryptor, metadata);
                        length--;
                    }
                }
            }

            // We've sent all the pieces. Now we just wait for the torrentmanager to process them all.
            while (rig.Manager.Mode is MetadataMode)
                System.Threading.Thread.Sleep (10);

            Assert.IsTrue (File.Exists (expectedPath), "#1");
            Torrent torrent = Torrent.Load (expectedPath);
            Assert.AreEqual (rig.Manager.InfoHash, torrent.InfoHash, "#2");
        }
    }
}
