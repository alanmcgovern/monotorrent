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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Logging;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.Libtorrent;
using MonoTorrent.Messages;

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

        public async Task Setup (bool metadataMode, bool multiFile = false, bool metadataOnly = false)
        {
            LoggerFactory.Register (new TextWriterLogger (TestContext.Out));
            pair = new ConnectionPair ().DisposeAfterTimeout ();
            rig = multiFile ? TestRig.CreateMultiFile (32768, metadataMode) : TestRig.CreateSingleFile (Constants.BlockSize * 27, Constants.BlockSize * 2, metadataMode);
            rig.RecreateManager ().Wait ();

            // Mark the torrent as hash check complete with no data downloaded
            if (rig.Manager.HasMetadata)
                await rig.Manager.LoadFastResumeAsync (new FastResume (rig.Manager.InfoHashes, new BitField (rig.Manager.Torrent.PieceCount ()), new BitField (rig.Manager.Torrent.PieceCount ())));

            var ready = rig.Manager.WaitForState (rig.Manager.HasMetadata ? TorrentState.Downloading : TorrentState.Metadata);
            await rig.Manager.StartAsync (metadataOnly);
            await ready;

            rig.AddConnection (pair.Outgoing);

            var connection = pair.Incoming;

            var result = await EncryptorFactory.CheckIncomingConnectionAsync (connection, rig.Engine.Settings.AllowedEncryption,  ImmutableList.Create<InfoHash> ().Add (rig.Manager.InfoHashes.V1OrV2), Factories.Default, TaskExtensions.Timeout);

            PeerId id = new PeerId (new Peer (new PeerInfo (connection.Uri)), connection, new BitField (rig.Torrent.PieceCount), rig.Manager.InfoHashes.V1OrV2, encryptor: result.Encryptor, decryptor: result.Decryptor, Software.Synthetic);
            decryptor = id.Decryptor;
            encryptor = id.Encryptor;
        }

        [TearDown]
        public async Task Teardown ()
        {
            await rig.Manager.StopAsync ();
            pair.Dispose ();
            rig.Dispose ();
            LoggerFactory.Register (null);
        }

        [Test]
        public async Task UnknownMetadataLength ()
        {
            await Setup (true);

            (var exHand, var releaser) = MessageEncoder.Extended.WriteHandshake(GitInfoHelper.ClientVersionMemory, false, null, 5555);
            Assert.DoesNotThrow (() => ((IMessageHandler) rig.Manager.Mode).HandleMessage (PeerId.CreateNull (1, rig.Manager.InfoHashes.V1OrV2), new Extended.HandshakeMessage (exHand)));
        }

        [Test]
        public async Task RequestMetadata ()
        {
            await Setup (false);
            CustomConnection connection = pair.Incoming;

            // 1) Send local handshake. We've already received the remote handshake as part
            // of the Connect method.
            (var sendHandshake, var releaser) = MessageEncoder.WriteHandshake (rig.Manager.Torrent.InfoHashes.V1OrV2.Span.Slice (0, 20), Enumerable.Repeat((byte)'g', 20).ToArray (), true, true, false);
            await PeerIO.SendMessageAsync (connection, encryptor, sendHandshake);

            (var exHand, releaser) = MessageEncoder.Extended.WriteHandshake(GitInfoHelper.ClientVersionMemory, false, rig.TorrentDict.LengthInBytes (), 5555);

            await PeerIO.SendMessageAsync (connection, encryptor, exHand);

            // 2) Send all our metadata requests
            int length = (rig.TorrentDict.LengthInBytes () + 16383) / 16384;
            for (int i = 0; i < length; i++) {
                (var msg, releaser) = MessageEncoder.Extended.WriteMetadata (new ExtensionSupports (new[] { MessageEncoder.Extended.MetadataExchangeSupport }), Extended.MetadataMessage.MetadataMessageType.Request, i, default);
                await PeerIO.SendMessageAsync (connection, encryptor, msg);
            }
            // 3) Receive all the metadata chunks
            var stream = new MemoryStream ();
            ReadOnlyMemory<byte> m;
            while (length > 0 && (m = await PeerIO.ReceiveMessageAsync (connection, decryptor)).Length > 0) {
                Test ();
                void Test ()
                {
                    if (MessageDispatcher.GetType (m) == MessageType.Extended && MessageDispatcher.GetExtendedMessageType(m) == ExtendedMessageType.Metadata) {
                        var metadata = new Extended.MetadataMessage (m);
                        if (metadata.MessageType == Extended.MetadataMessage.MetadataMessageType.Data) {
                            stream.Write (metadata.MetadataData.Span);
                            length--;
                        }
                    }
                }
                }

                // 4) Verify the hash is the same.
                stream.Position = 0;
            Assert.AreEqual (rig.Torrent.InfoHashes.V1OrV2, new InfoHash (SHA1.Create ().ComputeHash (stream)), "#1");
        }

        [Test]
        public async Task AfterHandshake_SendBitfieldMessage ()
        {
            await Setup (true);
            ReadOnlyBitField bf = new BitField (rig.Torrent.PieceCount);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteBitfield (bf).msg);
        }

        [Test]
        public async Task MetadataOnly_False_WithEvent ()
        {
            var tcs = new TaskCompletionSource<IList<ITorrentManagerFile>> ();
            new CancellationTokenSource (Debugger.IsAttached ? 100_000 : 10_000)
                .Token
                .Register (() => tcs.TrySetCanceled ());

            await Setup (true, metadataOnly: false);

            rig.Manager.MetadataReceived += (o, e) => {
                if (rig.Manager.Files == null)
                    tcs.SetException (new Exception ("Files were not set"));
                else
                    tcs.SetResult (rig.Manager.Files);
            };
            ReadOnlyBitField bf = new BitField (rig.Torrent.PieceCount);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteBitfield (bf).msg, metadataOnly: true);
            Assert.IsNotNull (await tcs.Task);
        }

        [Test]
        public async Task MetadataOnly_False_WithTask ()
        {
            var tcs = new TaskCompletionSource<IList<ITorrentManagerFile>> ();
            new CancellationTokenSource (Debugger.IsAttached ? 100_000 : 10_000)
                .Token
                .Register (() => tcs.TrySetCanceled ());

            await Setup (true, metadataOnly: false);

            async void WaitAsync ()
            {
                await rig.Manager.WaitForMetadataAsync ();
                if (rig.Manager.Files == null)
                    tcs.SetException (new Exception ("Files were not set"));
                else
                    tcs.SetResult (rig.Manager.Files);
            }

            WaitAsync ();

            ReadOnlyBitField bf = new BitField (rig.Torrent.PieceCount);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteBitfield (bf).msg, metadataOnly: true);
            Assert.IsNotNull (await tcs.Task);
        }

        [Test]
        public async Task MetadataOnly_True ()
        {
            var tcs = new TaskCompletionSource<ReadOnlyMemory<byte>> ();
            new CancellationTokenSource (Debugger.IsAttached ? 100000 : 10000)
                .Token
                .Register (() => tcs.TrySetCanceled ());

            await Setup (true, metadataOnly: true);

            rig.Manager.MetadataReceived += (o, e) => tcs.TrySetResult (e);
            ReadOnlyBitField bf = new BitField (rig.Torrent.PieceCount);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteBitfield (bf).msg, metadataOnly: true);
            Assert.IsTrue ((await tcs.Task).Length > 0);
        }

        [Test]
        public async Task AfterHandshake_SendHaveAllMessage ()
        {
            await Setup (true);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteHaveAll ().Item1);
        }

        [Test]
        public async Task AfterHandshake_SendHaveNoneMessage ()
        {
            await Setup (true);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteHaveNone ().Item1);
        }

        [Test]
        public async Task SendMetadata_ToFile ()
        {
            await Setup (true);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteHaveNone ().Item1);
        }

        [Test]
        public async Task SendMetadata_ToFile_CorruptFileExists ()
        {
            File.Create (rig.MetadataPath).Close ();
            await Setup (true);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteHaveNone ().Item1);
        }

        [Test]
        public async Task SendMetadata_ToFile_RealFileExists ()
        {
            await Setup (true);
            Directory.CreateDirectory (Path.GetDirectoryName (rig.MetadataPath));
            File.WriteAllBytes (rig.MetadataPath, rig.TorrentDict.Encode ());

            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteHaveNone ().Item1);
        }

        [Test]
        public async Task SendMetadata_ToFolder ()
        {
            await Setup (true);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteHaveNone ().Item1);
        }

        [Test]
        public async Task SingleFileSavePath ()
        {
            await Setup (true, multiFile: false);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteHaveNone ().Item1);

            Assert.AreEqual (@"test.files", rig.Manager.Torrent.Name);
            Assert.AreEqual (Environment.CurrentDirectory, rig.Manager.SavePath);

            var torrentFiles = rig.Manager.Files;
            Assert.AreEqual (torrentFiles.Count, 1);
            Assert.AreEqual (Path.Combine ("Dir1", "File1"), torrentFiles[0].Path);
            Assert.AreEqual (Path.Combine (Environment.CurrentDirectory, "Dir1", "File1"), torrentFiles[0].FullPath);
        }

        [Test]
        public async Task MultiFileSavePath ()
        {
            await Setup (true, multiFile: true);
            await SendMetadataCore (rig.MetadataPath, MessageEncoder.WriteHaveNone ().Item1);

            Assert.AreEqual (@"test.files", rig.Manager.Torrent.Name);
            Assert.AreEqual (Environment.CurrentDirectory, rig.Manager.SavePath);

            var torrentFiles = rig.Manager.Files;
            Assert.AreEqual (torrentFiles.Count, 4);
            Assert.AreEqual (Path.Combine ("Dir1", "File1"), torrentFiles[0].Path);
            Assert.AreEqual (Path.Combine ("Dir1", "Dir2", "File2"), torrentFiles[1].Path);
            Assert.AreEqual (@"File3", torrentFiles[2].Path);
            Assert.AreEqual (@"File4", torrentFiles[3].Path);

            Assert.AreEqual (Path.Combine (Environment.CurrentDirectory, "test.files", "Dir1", "File1"), torrentFiles[0].FullPath);
            Assert.AreEqual (Path.Combine (Environment.CurrentDirectory, "test.files", "Dir1", "Dir2", "File2"), torrentFiles[1].FullPath);
            Assert.AreEqual (Path.Combine (Environment.CurrentDirectory, "test.files", "File3"), torrentFiles[2].FullPath);
            Assert.AreEqual (Path.Combine (Environment.CurrentDirectory, "test.files", "File4"), torrentFiles[3].FullPath);
        }

        internal async Task SendMetadataCore (string expectedPath, Memory<byte> sendAfterHandshakeMessage, bool metadataOnly = false)
        {
            CustomConnection connection = pair.Incoming;
            var metadataTcs = new TaskCompletionSource<ReadOnlyMemory<byte>> ();
            rig.Manager.MetadataReceived += (o, e) => metadataTcs.TrySetResult (e);

            // 1) Send local handshake. We've already received the remote handshake as part
            // of the Connect method.
            (var sendHandshake, _) = MessageEncoder.WriteHandshake (rig.Manager.InfoHashes.V1OrV2.Truncate ().Span, Enumerable.Repeat ((byte) 'g', 20).ToArray (), true, true, false);
            await PeerIO.SendMessageAsync (connection, encryptor, sendHandshake);
            (var exHand, _) = MessageEncoder.Extended.WriteHandshake (GitInfoHelper.ClientVersionMemory, false, rig.Torrent.InfoMetadata.Length, 5555);
            await PeerIO.SendMessageAsync (connection, encryptor, exHand);

            await PeerIO.SendMessageAsync (connection, encryptor, sendAfterHandshakeMessage);

            bool receivedHaveNone = false;
            // 2) Receive the metadata requests from the other peer and fulfill them
            ReadOnlyMemory<byte> buffer = rig.Torrent.InfoMetadata;
            var unrequestedPieces = new HashSet<int> (Enumerable.Range (0, (buffer.Length + 16383) / 16384));
            ReadOnlyMemory<byte> msgBuffer;
            while (unrequestedPieces.Count > 0 && (msgBuffer = await PeerIO.ReceiveMessageAsync (connection, decryptor)).Length > 0) {

                if (MessageDispatcher.GetType (msgBuffer) == MessageType.Extended && MessageDispatcher.GetExtendedMessageType (msgBuffer) == ExtendedMessageType.Handshake) {
                    Assert.IsNull (new Extended.HandshakeMessage (msgBuffer).MetadataBytes);
                    Assert.AreEqual (Constants.DefaultMaxPendingRequests, new Extended.HandshakeMessage (msgBuffer).MaxRequests);
                } else if (MessageDispatcher.GetType(msgBuffer) == MessageType.HaveNone) {
                    receivedHaveNone = true;
                } else if (MessageDispatcher.GetType (msgBuffer) == MessageType.Extended && MessageDispatcher.GetExtendedMessageType(msgBuffer) == ExtendedMessageType.Metadata) {

                    if (new Extended.MetadataMessage(msgBuffer).MessageType == Extended.MetadataMessage.MetadataMessageType.Request) {

                        (Memory<byte> metadata, var releaser) = MessageEncoder.Extended.WriteMetadata(new ExtensionSupports (new[] { MessageEncoder.Extended.MetadataExchangeSupport }), Extended.MetadataMessage.MetadataMessageType.Data, new Extended.MetadataMessage (msgBuffer).Piece, buffer.Span);
                        await PeerIO.SendMessageAsync (connection, encryptor, metadata);
                        unrequestedPieces.Remove (new Extended.MetadataMessage (msgBuffer).Piece);

                        // Hack this in because testing is... awkward... for most of this library.
                        // The purpose here is to ensure that duplicate pieces don't reset our data or cause the event
                        // to be emitted multiple times.
                        if (unrequestedPieces.Count > 0) {
                            (metadata, releaser) = MessageEncoder.Extended.WriteMetadata (new ExtensionSupports (new[] { MessageEncoder.Extended.MetadataExchangeSupport }), Extended.MetadataMessage.MetadataMessageType.Data, 0, buffer.Span);
                            await PeerIO.SendMessageAsync (connection, encryptor, metadata);
                        }

                        // And let's receive many handshake messages from other peers. Ensure we process this on the correct
                        // thread. It needs to be on the main loop as it's run in the context of the ClientEngine loop.
                        if (rig.Manager.Mode is MetadataMode mode) {
                            (exHand, var _) = MessageEncoder.Extended.WriteHandshake (GitInfoHelper.ClientVersionMemory, false, rig.Torrent.InfoMetadata.Length, 5555);
                            ClientEngine.MainLoop.Post (state => mode.HandleMessage (PeerId.CreateNull (12389, rig.Manager.InfoHashes.V1OrV2), new Extended.HandshakeMessage(exHand)), null);
                        }
                    }
                }
            }

            // We've sent all the pieces. Now we just wait for the torrentmanager to process them all.
            Torrent torrent;
            if (metadataOnly) {
                torrent = Torrent.Load ((await metadataTcs.Task.WithTimeout ()).Span);
            } else {
                await rig.Manager.WaitForState (TorrentState.Downloading).WithTimeout ();
                Assert.IsTrue (File.Exists (expectedPath), "#1");
                torrent = Torrent.Load (expectedPath);
            }

            Assert.AreEqual (rig.Manager.InfoHashes, torrent.InfoHashes, "#2");
            Assert.AreEqual (1, torrent.AnnounceUrls.Count, "#3");
            Assert.AreEqual (2, torrent.AnnounceUrls[0].Count, "#4");

            Assert.IsTrue (receivedHaveNone, "#6");

            if (!metadataOnly) {
                var peer = PeerId.CreateNull (rig.Manager.Bitfield.Length, true, false, true, rig.Manager.InfoHashes.V1OrV2);
                Assert.DoesNotThrow (() => rig.Manager.PieceManager.AddPieceRequests (peer));
            }
        }
    }
}
