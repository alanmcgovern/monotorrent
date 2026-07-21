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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.Libtorrent;
using MonoTorrent.Messages;

using NUnit.Framework;

namespace MonoTorrent.Client.Modes
{
    [TestFixture]
    public class MetadataModeTests2
    {
        string HybridTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (MetadataModeTests2).Assembly.Location), "MonoTorrent", "bittorrent-v2-hybrid-test.torrent");
        string V2OnlyTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (MetadataModeTests2).Assembly.Location), "MonoTorrent", "bittorrent-v2-test.torrent");

        [Test]
        public async Task RequestMetadata ()
        {
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (autoSaveLoadMagnetLinkMetadata: false));
            var torrent = await Torrent.LoadAsync (HybridTorrentPath);
            var manager = await engine.AddAsync (new MagnetLink (torrent.InfoHashes), "bbb");
            var metadataMode = new MetadataMode (manager, engine.DiskManager, engine.ConnectionManager, engine.Settings, "blarp", true);
            var peer = manager.AddConnectedPeer (supportsLTMetdata: true);

            Test ();
            void Test ()
            {
                (var handshakeMsg, var handshakeMsgReleaser) = MessageEncoder.Extended.WriteHandshake (GitInfoHelper.ClientVersionMemory, false, torrent.InfoMetadata.Length, 12345);
                metadataMode.HandleMessage (peer, new Extended.HandshakeMessage (handshakeMsg));
                while (manager.Torrent is null) {
                    metadataMode.Tick (0);
                    ReadOnlyMemory<byte> msg;
                    while (peer.IsConnected && (msg = peer.MessageQueue.TryDequeue ()).Length > 0) {
                        var msgType = (MessageType) msg.Span[4];
                        var exType = (ExtendedMessageType) msg.Span[5];
                        if (MessageDispatcher.GetType (msg) == MessageType.Extended && MessageDispatcher.GetExtendedMessageType (msg) == ExtendedMessageType.Metadata) {
                            var metadata = new Extended.MetadataMessage (msg);
                            if (metadata.MessageType == Extended.MetadataMessage.MetadataMessageType.Request) {
                                (var response, var releaser) = MessageEncoder.Extended.WriteMetadata (peer.ExtensionSupports, Extended.MetadataMessage.MetadataMessageType.Data, metadata.Piece, torrent.InfoMetadata.Span);
                                metadataMode.HandleMessage (peer, new Extended.MetadataMessage(response));
                            }

                        }
                    }
                }
            }

            Assert.AreEqual (manager.Torrent.InfoHashes, manager.InfoHashes);
        }

        [Test]
        public async Task RequestMetadata_OnePeerDisconnects ()
        {
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (autoSaveLoadMagnetLinkMetadata: false));
            var torrent = await Torrent.LoadAsync (HybridTorrentPath);
            var manager = await engine.AddAsync (new MagnetLink (torrent.InfoHashes), "bbb");
            manager.Mode = new MetadataMode (manager, engine.DiskManager, engine.ConnectionManager, engine.Settings, "blarp", true);
            var peer = manager.AddConnectedPeer (supportsLTMetdata: true);

            (var handshakeMsg, var releaser) = MessageEncoder.Extended.WriteHandshake (GitInfoHelper.ClientVersionMemory, false, torrent.InfoMetadata.Length, 12345);
            ((IMessageHandler) manager.Mode).HandleMessage (peer, new Extended.HandshakeMessage (handshakeMsg));
            Assert.AreNotEqual (0, peer.AmRequestingPiecesCount);

            engine.ConnectionManager.CleanupSocket (manager, peer);
            Assert.AreEqual (0, peer.AmRequestingPiecesCount);

            peer = manager.AddConnectedPeer (supportsLTMetdata: true);

            (handshakeMsg, releaser) = MessageEncoder.Extended.WriteHandshake (GitInfoHelper.ClientVersionMemory, false, torrent.InfoMetadata.Length, 12345);
            ((IMessageHandler) manager.Mode).HandleMessage (peer, new Extended.HandshakeMessage (handshakeMsg));

            Test ();
            void Test ()
            {
                while (manager.Torrent is null) {
                    manager.Mode.Tick (0);
                    while (peer.IsConnected) {
                        var msg = peer.MessageQueue.TryDequeue ();
                        if (MessageDispatcher.GetType (msg) != MessageType.Extended || MessageDispatcher.GetExtendedMessageType (msg) != ExtendedMessageType.Metadata)
                            continue;

                        var metadata = new Extended.MetadataMessage (msg);
                        if (metadata.MessageType == Extended.MetadataMessage.MetadataMessageType.Request) {
                            (var response, var releaser) = MessageEncoder.Extended.WriteMetadata (peer.ExtensionSupports, Extended.MetadataMessage.MetadataMessageType.Data, metadata.Piece, torrent.InfoMetadata.Span);
                            ((IMessageHandler) manager.Mode).HandleMessage (peer, new Extended.MetadataMessage (response));
                        }
                    }
                }
            }

            Assert.AreEqual (manager.Torrent.InfoHashes, manager.InfoHashes);
        }
    }
}
