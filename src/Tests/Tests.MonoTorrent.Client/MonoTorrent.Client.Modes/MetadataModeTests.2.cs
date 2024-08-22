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
using MonoTorrent.Messages.Peer.FastPeer;
using MonoTorrent.Messages.Peer.Libtorrent;

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

            metadataMode.HandleMessage (peer, new ExtendedHandshakeMessage (false, torrent.InfoMetadata.Length, 12345), default);
            while (manager.Torrent is null) {
                metadataMode.Tick (0);
                PeerMessage message;
                while (peer.IsConnected && (message = peer.MessageQueue.TryDequeue ()) != null) {
                    if (message is LTMetadata metadata) {
                        if (metadata.MetadataMessageType == LTMetadata.MessageType.Request) {
                            var data = torrent.InfoMetadata.Slice (metadata.Piece * Constants.BlockSize);
                            data = data.Slice (0, Math.Min (Constants.BlockSize, data.Length));
                            metadataMode.HandleMessage (peer, new LTMetadata (LTMetadata.Support.MessageId, LTMetadata.MessageType.Data, metadata.Piece, data), default);
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

            manager.Mode.HandleMessage (peer, new ExtendedHandshakeMessage (false, torrent.InfoMetadata.Length, 12345), default);
            Assert.AreNotEqual (0, peer.AmRequestingPiecesCount);

            engine.ConnectionManager.CleanupSocket (manager, peer);
            Assert.AreEqual (0, peer.AmRequestingPiecesCount);

            peer = manager.AddConnectedPeer (supportsLTMetdata: true);
            manager.Mode.HandleMessage (peer, new ExtendedHandshakeMessage (false, torrent.InfoMetadata.Length, 12345), default);

            while (manager.Torrent is null) {
                manager.Mode.Tick (0);
                PeerMessage message;
                while (peer.IsConnected && (message = peer.MessageQueue.TryDequeue ()) != null) {
                    if (message is LTMetadata metadata) {
                        if (metadata.MetadataMessageType == LTMetadata.MessageType.Request) {
                            var data = torrent.InfoMetadata.Slice (metadata.Piece * Constants.BlockSize);
                            data = data.Slice (0, Math.Min (Constants.BlockSize, data.Length));
                            manager.Mode.HandleMessage (peer, new LTMetadata (LTMetadata.Support.MessageId, LTMetadata.MessageType.Data, metadata.Piece, data), default);
                        }
                    }
                }
            }

            Assert.AreEqual (manager.Torrent.InfoHashes, manager.InfoHashes);
        }
    }
}
