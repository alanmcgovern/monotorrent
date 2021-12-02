//
// ClientEngineTests.cs
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Client
{

    [TestFixture]
    public class ClientEngineTests
    {
        [Test]
        public async Task AddPeers_Dht ()
        {
            using var rig = TestRig.CreateMultiFile (new TestWriter ());
            var dht = (ManualDhtEngine) rig.Engine.DhtEngine;

            var tcs = new TaskCompletionSource<DhtPeersAdded> ();
            var manager = rig.Engine.Torrents[0];
            manager.PeersFound += (o, e) => {
                if (e is DhtPeersAdded args)
                    tcs.TrySetResult (args);
            };

            var peer = rig.CreatePeer (false).Peer;
            dht.RaisePeersFound (manager.InfoHash, new[] { new PeerInfo (peer.ConnectionUri, peer.PeerId.AsMemory ()) });
            var result = await tcs.Task.WithTimeout (TimeSpan.FromSeconds (5));
            Assert.AreEqual (1, result.NewPeers, "#2");
            Assert.AreEqual (0, result.ExistingPeers, "#3");
            Assert.AreEqual (1, manager.Peers.AvailablePeers.Count, "#4");
        }

        [Test]
        public async Task AddPeers_Dht_Private ()
        {
            // You can't manually add peers to private torrents
            using var rig = TestRig.CreateMultiFile (new TestWriter ());
            var editor = new TorrentEditor (rig.TorrentDict) {
                CanEditSecureMetadata = true,
                Private = true
            };

            var manager = await rig.Engine.AddAsync (editor.ToTorrent (), "path", new TorrentSettings ());

            var dht = (ManualDhtEngine) rig.Engine.DhtEngine;

            var tcs = new TaskCompletionSource<DhtPeersAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is DhtPeersAdded args)
                    tcs.TrySetResult (args);
            };

            var peer = rig.CreatePeer (false).Peer;
            dht.RaisePeersFound (manager.InfoHash, new[] { new PeerInfo (peer.ConnectionUri, peer.PeerId.AsMemory ()) });
            var result = await tcs.Task.WithTimeout (TimeSpan.FromSeconds (5));
            Assert.AreEqual (0, result.NewPeers, "#2");
            Assert.AreEqual (0, result.ExistingPeers, "#3");
            Assert.AreEqual (0, manager.Peers.AvailablePeers.Count, "#4");
        }

        [Test]
        public async Task AddPeers_LocalPeerDiscovery ()
        {
            using var rig = TestRig.CreateMultiFile (new TestWriter ());
            var localPeer = (ManualLocalPeerListener) rig.Engine.LocalPeerDiscovery;

            var tcs = new TaskCompletionSource<LocalPeersAdded> ();
            var manager = rig.Engine.Torrents[0];
            manager.PeersFound += (o, e) => {
                if (e is LocalPeersAdded args)
                    tcs.TrySetResult (args);
            };

            localPeer.RaisePeerFound (manager.InfoHash, rig.CreatePeer (false).Uri);
            var result = await tcs.Task.WithTimeout (TimeSpan.FromSeconds (5));
            Assert.AreEqual (1, result.NewPeers, "#2");
            Assert.AreEqual (0, result.ExistingPeers, "#3");
            Assert.AreEqual (1, manager.Peers.AvailablePeers.Count, "#4");
        }

        [Test]
        public async Task AddPeers_LocalPeerDiscovery_Private ()
        {
            // You can't manually add peers to private torrents
            using var rig = TestRig.CreateMultiFile (new TestWriter ());
            var editor = new TorrentEditor (rig.TorrentDict) {
                CanEditSecureMetadata = true,
                Private = true
            };

            var manager = await rig.Engine.AddAsync (editor.ToTorrent (), "path", new TorrentSettings ());

            var localPeer = (ManualLocalPeerListener) rig.Engine.LocalPeerDiscovery;

            var tcs = new TaskCompletionSource<LocalPeersAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is LocalPeersAdded args)
                    tcs.TrySetResult (args);
            };

            localPeer.RaisePeerFound (manager.InfoHash, rig.CreatePeer (false).Uri);
            var result = await tcs.Task.WithTimeout (TimeSpan.FromSeconds (5));
            Assert.AreEqual (0, result.NewPeers, "#2");
            Assert.AreEqual (0, result.ExistingPeers, "#3");
            Assert.AreEqual (0, manager.Peers.AvailablePeers.Count, "#4");
        }

        [Test]
        public void CacheDirectory_IsFile_Constructor ()
        {
            var tmp = TempDir.Create ();
            var cachePath = Path.Combine (tmp.Path, "test.file");
            using (var file = File.Create (cachePath)) { }
            Assert.Throws<ArgumentException> (() => new ClientEngine (new EngineSettingsBuilder { CacheDirectory = cachePath }.ToSettings ()));
        }

        [Test]
        public void CacheDirectory_IsFile_UpdateSettings ()
        {
            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests ());
            var tmp = TempDir.Create ();
            var cachePath = Path.Combine (tmp.Path, "test.file");
            using (var file = File.Create (cachePath)) { }
            Assert.ThrowsAsync<ArgumentException> (() => engine.UpdateSettingsAsync (new EngineSettingsBuilder { CacheDirectory = cachePath }.ToSettings ()));
        }

        [Test]
        public async Task UsePartialFiles_InitiallyOff_ToggleOn ()
        {
            var pieceLength = Constants.BlockSize * 4;
            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests (usePartialFiles: false));
            var torrent = TestRig.CreateMultiFileTorrent (TorrentFile.Create (pieceLength, Constants.BlockSize, Constants.BlockSize * 2, Constants.BlockSize * 3), pieceLength, out BEncoding.BEncodedDictionary _);

            var manager = await engine.AddAsync (torrent, "");
            Assert.AreEqual (manager.Files[0].DownloadCompleteFullPath, manager.Files[0].DownloadIncompleteFullPath);

            var settings = new EngineSettingsBuilder (engine.Settings) { UsePartialFiles = true }.ToSettings ();
            await engine.UpdateSettingsAsync (settings);
            Assert.AreNotEqual (manager.Files[0].DownloadCompleteFullPath, manager.Files[0].DownloadIncompleteFullPath);
        }

        [Test]
        public async Task UsePartialFiles_InitiallyOn_ToggleOff ()
        {
            var pieceLength = Constants.BlockSize * 4;
            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests (usePartialFiles: true));
            var torrent = TestRig.CreateMultiFileTorrent (TorrentFile.Create (pieceLength, Constants.BlockSize, Constants.BlockSize * 2, Constants.BlockSize * 3), pieceLength, out BEncoding.BEncodedDictionary _);

            var manager = await engine.AddAsync (torrent, "");
            Assert.AreNotEqual (manager.Files[0].DownloadCompleteFullPath, manager.Files[0].DownloadIncompleteFullPath);

            var settings = new EngineSettingsBuilder (engine.Settings) { UsePartialFiles = false }.ToSettings ();
            await engine.UpdateSettingsAsync (settings);
            Assert.AreEqual (manager.Files[0].DownloadCompleteFullPath, manager.Files[0].DownloadIncompleteFullPath);
        }

        [Test]
        public void DownloadMetadata_Cancelled ()
        {
            var cts = new CancellationTokenSource ();
            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests ());
            var task = engine.DownloadMetadataAsync (new MagnetLink (new InfoHash (new byte[20])), cts.Token);
            cts.Cancel ();
            Assert.ThrowsAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public void DownloadMetadata_SameTwice ()
        {
            var link = MagnetLink.Parse ("magnet:?xt=urn:btih:1234512345123451234512345123451234512345");
            using var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests ());
            var first = engine.DownloadMetadataAsync (link, CancellationToken.None);
            Assert.ThrowsAsync<TorrentException> (() => engine.DownloadMetadataAsync (link, CancellationToken.None));
        }

        [Test]
        public async Task SaveRestoreState_NoTorrents ()
        {
            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests ());
            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync ());
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
        }

        [Test]
        [TestCase (true)]
        [TestCase (false)]
        public async Task SaveRestoreState_OneInMemoryTorrent (bool addStreaming)
        {
            var pieceLength = Constants.BlockSize * 4;
            using var tmpDir = TempDir.Create ();

            var torrent = TestRig.CreateMultiFileTorrent (TorrentFile.Create (pieceLength, Constants.BlockSize, Constants.BlockSize * 2, Constants.BlockSize * 3), pieceLength, out BEncoding.BEncodedDictionary metadata);

            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests (cacheDirectory: tmpDir.Path));
            TorrentManager torrentManager;
            if (addStreaming)
                torrentManager = await engine.AddStreamingAsync (torrent, "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = true }.ToSettings ());
            else
                torrentManager = await engine.AddAsync (torrent, "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = true }.ToSettings ());

            await torrentManager.SetFilePriorityAsync (torrentManager.Files[0], Priority.High);
            await torrentManager.MoveFileAsync (torrentManager.Files[1], Path.GetFullPath ("some_fake_path.txt"));

            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync ());
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
            Assert.AreEqual (engine.Torrents[0].Torrent.Name, restoredEngine.Torrents[0].Torrent.Name);
            Assert.AreEqual (engine.Torrents[0].SavePath, restoredEngine.Torrents[0].SavePath);
            Assert.AreEqual (engine.Torrents[0].Settings, restoredEngine.Torrents[0].Settings);
            Assert.AreEqual (engine.Torrents[0].InfoHash, restoredEngine.Torrents[0].InfoHash);
            Assert.AreEqual (engine.Torrents[0].MagnetLink.ToV1String (), restoredEngine.Torrents[0].MagnetLink.ToV1String ());

            Assert.AreEqual (engine.Torrents[0].Files.Count, restoredEngine.Torrents[0].Files.Count);
            for (int i = 0; i < engine.Torrents.Count; i++) {
                Assert.AreEqual (engine.Torrents[0].Files[i].FullPath, restoredEngine.Torrents[0].Files[i].FullPath);
                Assert.AreEqual (engine.Torrents[0].Files[i].Priority, restoredEngine.Torrents[0].Files[i].Priority);
            }
        }

        [Test]
        [TestCase (true)]
        [TestCase (false)]
        public async Task SaveRestoreState_OneMagnetLink (bool addStreaming)
        {
            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests ());
            if (addStreaming)
                await engine.AddStreamingAsync (new MagnetLink (new InfoHash (new byte[20]), "test"), "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings ());
            else
                await engine.AddAsync (new MagnetLink (new InfoHash (new byte[20]), "test"), "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings ());

            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync ());
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
            Assert.AreEqual (engine.Torrents[0].SavePath, restoredEngine.Torrents[0].SavePath);
            Assert.AreEqual (engine.Torrents[0].Settings, restoredEngine.Torrents[0].Settings);
            Assert.AreEqual (engine.Torrents[0].InfoHash, restoredEngine.Torrents[0].InfoHash);
            Assert.AreEqual (engine.Torrents[0].MagnetLink.ToV1Uri (), restoredEngine.Torrents[0].MagnetLink.ToV1Uri ());
            Assert.AreEqual (engine.Torrents[0].Files, restoredEngine.Torrents[0].Files);
        }

        [Test]
        public async Task SaveRestoreState_OneTorrentFile_ContainingDirectory ()
        {
            var pieceLength = Constants.BlockSize * 4;
            using var tmpDir = TempDir.Create ();

            TestRig.CreateMultiFileTorrent (TorrentFile.Create (pieceLength, Constants.BlockSize, Constants.BlockSize * 2, Constants.BlockSize * 3), pieceLength, out BEncoding.BEncodedDictionary metadata);
            var metadataFile = Path.Combine (tmpDir.Path, "test.torrent");
            File.WriteAllBytes (metadataFile, metadata.Encode ());

            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests (cacheDirectory: tmpDir.Path));
            var torrentManager = await engine.AddStreamingAsync (metadataFile, "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = true }.ToSettings ());
            await torrentManager.SetFilePriorityAsync (torrentManager.Files[0], Priority.High);
            await torrentManager.MoveFileAsync (torrentManager.Files[1], Path.GetFullPath ("some_fake_path.txt"));

            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync ());
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
            Assert.AreEqual (engine.Torrents[0].Torrent.Name, restoredEngine.Torrents[0].Torrent.Name);
            Assert.AreEqual (engine.Torrents[0].SavePath, restoredEngine.Torrents[0].SavePath);
            Assert.AreEqual (engine.Torrents[0].Settings, restoredEngine.Torrents[0].Settings);
            Assert.AreEqual (engine.Torrents[0].InfoHash, restoredEngine.Torrents[0].InfoHash);
            Assert.AreEqual (engine.Torrents[0].MagnetLink.ToV1String (), restoredEngine.Torrents[0].MagnetLink.ToV1String ());

            Assert.AreEqual (engine.Torrents[0].Files.Count, restoredEngine.Torrents[0].Files.Count);
            for (int i = 0; i < engine.Torrents.Count; i++) {
                Assert.AreEqual (engine.Torrents[0].Files[i].FullPath, restoredEngine.Torrents[0].Files[i].FullPath);
                Assert.AreEqual (engine.Torrents[0].Files[i].Priority, restoredEngine.Torrents[0].Files[i].Priority);
            }
        }

        [Test]
        public async Task SaveRestoreState_OneTorrentFile_NoContainingDirectory ()
        {
            var pieceLength = Constants.BlockSize * 4;
            using var tmpDir = TempDir.Create ();

            TestRig.CreateMultiFileTorrent (TorrentFile.Create (pieceLength, Constants.BlockSize, Constants.BlockSize * 2, Constants.BlockSize * 3), pieceLength, out BEncoding.BEncodedDictionary metadata);
            var metadataFile = Path.Combine (tmpDir.Path, "test.torrent");
            File.WriteAllBytes (metadataFile, metadata.Encode ());

            var engine = new ClientEngine (EngineSettingsBuilder.CreateForTests (cacheDirectory: tmpDir.Path));
            await engine.AddStreamingAsync (metadataFile, "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings ());

            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync ());
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
            Assert.AreEqual (engine.Torrents[0].Torrent.Name, restoredEngine.Torrents[0].Torrent.Name);
            Assert.AreEqual (engine.Torrents[0].SavePath, restoredEngine.Torrents[0].SavePath);
            Assert.AreEqual (engine.Torrents[0].Settings, restoredEngine.Torrents[0].Settings);
            Assert.AreEqual (engine.Torrents[0].InfoHash, restoredEngine.Torrents[0].InfoHash);
            Assert.AreEqual (engine.Torrents[0].MagnetLink.ToV1String (), restoredEngine.Torrents[0].MagnetLink.ToV1String ());

            Assert.AreEqual (engine.Torrents[0].Files.Count, restoredEngine.Torrents[0].Files.Count);
            for (int i = 0; i < engine.Torrents.Count; i++) {
                Assert.AreEqual (engine.Torrents[0].Files[i].FullPath, restoredEngine.Torrents[0].Files[i].FullPath);
                Assert.AreEqual (engine.Torrents[0].Files[i].Priority, restoredEngine.Torrents[0].Files[i].Priority);
            }
        }

        [Test]
        public async Task StopTest ()
        {
            using var rig = TestRig.CreateMultiFile (new TestWriter ());
            var hashingState = rig.Manager.WaitForState (TorrentState.Hashing);
            var stoppedState = rig.Manager.WaitForState (TorrentState.Stopped);

            await rig.Manager.StartAsync ();
            Assert.IsTrue (hashingState.Wait (5000), "Started");
            await rig.Manager.StopAsync ();
            Assert.IsTrue (stoppedState.Wait (5000), "Stopped");
        }
    }
}
