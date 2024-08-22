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

using MonoTorrent.BEncoding;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;
using MonoTorrent.Messages.Peer.Libtorrent;

using NUnit.Framework;

namespace MonoTorrent.Client.Modes
{
    [TestFixture]
    public class PieceHashesModeTests
    {
        string HybridTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (MetadataModeTests2).Assembly.Location), "MonoTorrent", "bittorrent-v2-hybrid-test.torrent");
        string V2OnlyTorrentPath => Path.Combine (Path.GetDirectoryName (typeof (MetadataModeTests2).Assembly.Location), "MonoTorrent", "bittorrent-v2-test.torrent");

        async Task<(ClientEngine engine, TorrentManager manager, PieceHashesV2 hashes)> CreateTorrent(string path)
        {
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (autoSaveLoadMagnetLinkMetadata: false));

            var dict = (BEncodedDictionary) BEncodedDictionary.Decode (File.ReadAllBytes (V2OnlyTorrentPath));
            var rawLayers = (BEncodedDictionary) dict["piece layers"];
            dict.Remove ("piece layers");

            var torrent = await Torrent.LoadAsync (dict.Encode ());
            var layers = new PieceHashesV2 (torrent.PieceLength, torrent.Files, rawLayers);

            var manager = await engine.AddAsync (torrent, "bbb");
            return (engine, manager, layers);
        }

        [Test]
        public async Task InitialStateIsCorrect ()
        {
            (var engine, var manager, var layers) = await CreateTorrent (V2OnlyTorrentPath);

            var pieceHashesMode = new PieceHashesMode (manager, engine.DiskManager, engine.ConnectionManager, engine.Settings, true);
            var peer = manager.AddConnectedPeer (supportsLTMetdata: true);

            Assert.AreEqual (manager.PieceHashes.Count, 0);
            Assert.IsFalse (manager.PendingV2PieceHashes.AllFalse);
            Assert.IsFalse (manager.PieceHashes.HasV2Hashes);
            Assert.IsFalse (manager.PieceHashes.HasV1Hashes);
        }

        [Test]
        public async Task RequestHashes ()
        {
            (var engine, var manager, var layers) = await CreateTorrent (V2OnlyTorrentPath);
            var pieceHashesMode = new PieceHashesMode (manager, engine.DiskManager, engine.ConnectionManager, engine.Settings, true);
            var peer = manager.AddConnectedPeer (supportsLTMetdata: true);
            pieceHashesMode.HandleMessage (peer, new ExtendedHandshakeMessage (false, manager.Torrent.InfoMetadata.Length, 12345), default);
            while (!manager.PendingV2PieceHashes.AllFalse && manager.PieceHashes.Count == 0 && !manager.PieceHashes.HasV2Hashes) {
                pieceHashesMode.Tick (0);
                PeerMessage message;
                while ((message = peer.MessageQueue.TryDequeue ()) != null)
                    if (message is HashRequestMessage hashRequest)
                        pieceHashesMode.HandleMessage (peer, FulfillRequest (hashRequest, layers), default);
            }

            Assert.AreEqual (manager.PieceHashes.Count, manager.Torrent.PieceCount);
            Assert.IsTrue (manager.PendingV2PieceHashes.AllFalse);
            Assert.IsTrue (manager.PieceHashes.HasV2Hashes);
            Assert.IsFalse (manager.PieceHashes.HasV1Hashes);
        }

        [Test]
        public async Task RequestHashes_OnePeerDisconnects ()
        {
            (var engine, var manager, var layers) = await CreateTorrent (V2OnlyTorrentPath);

            var pieceHashesMode = new PieceHashesMode (manager, engine.DiskManager, engine.ConnectionManager, engine.Settings, true);
            var peer = manager.AddConnectedPeer (supportsLTMetdata: true);
            pieceHashesMode.Tick (0);
            Assert.AreNotEqual (0, peer.AmRequestingPiecesCount);
            engine.ConnectionManager.CleanupSocket (manager, peer);
            Assert.AreEqual (0, manager.Peers.ConnectedPeers.Count);

            peer = manager.AddConnectedPeer (supportsLTMetdata: true);

            while (!manager.PendingV2PieceHashes.AllFalse && manager.PieceHashes.Count == 0 && !manager.PieceHashes.HasV2Hashes) {
                pieceHashesMode.Tick (0);
                PeerMessage message;
                while ((message = peer.MessageQueue.TryDequeue ()) != null)
                    if (message is HashRequestMessage hashRequest)
                        pieceHashesMode.HandleMessage (peer, FulfillRequest (hashRequest, layers), default);
            }

            Assert.AreEqual (manager.PieceHashes.Count, manager.Torrent.PieceCount);
            Assert.IsTrue (manager.PendingV2PieceHashes.AllFalse);
            Assert.IsTrue (manager.PieceHashes.HasV2Hashes);
            Assert.IsFalse (manager.PieceHashes.HasV1Hashes);
        }

        static HashesMessage FulfillRequest(HashRequestMessage hashRequest, PieceHashesV2 layers)
        {
            Memory<byte> totalBuffer = new byte[(hashRequest.Length + hashRequest.ProofLayers) * 32];
            Assert.IsTrue (layers.TryGetV2Hashes (hashRequest.PiecesRoot, hashRequest.BaseLayer, hashRequest.Index, hashRequest.Length, hashRequest.ProofLayers, totalBuffer.Span, out int bytesWritten));
            return new HashesMessage (hashRequest.PiecesRoot, hashRequest.BaseLayer, hashRequest.Index, hashRequest.Length, hashRequest.ProofLayers, totalBuffer.Slice (0, bytesWritten), default);
        }
    }
}
