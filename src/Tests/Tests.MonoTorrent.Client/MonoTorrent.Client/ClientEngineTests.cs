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
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.PieceWriter;

using NUnit.Framework;

namespace MonoTorrent.Client
{

    [TestFixture]
    public class ClientEngineTests
    {
        [Test]
        public async Task AddPeers_Dht ()
        {
            var dht = new ManualDhtEngine ();
            var factories = EngineHelpers.Factories.WithDhtCreator (() => dht);
            var settings = EngineHelpers.CreateSettings (dhtEndPoint: new IPEndPoint (IPAddress.Any, 1234));

            using var engine = new ClientEngine (settings, factories);
            var manager = await engine.AddAsync (new MagnetLink (InfoHash.FromMemory (new byte[20])), "asd");

            var tcs = new TaskCompletionSource<DhtPeersAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is DhtPeersAdded args)
                    tcs.TrySetResult (args);
            };

            var peer = new PeerInfo (new Uri ("ipv4://123.123.123.123:1515"));
            dht.RaisePeersFound (manager.InfoHashes.V1OrV2, new[] { peer });
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
            dht.RaisePeersFound (manager.InfoHashes.V1OrV2, new[] { peer.Info });
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

            localPeer.RaisePeerFound (manager.InfoHashes.V1OrV2, rig.CreatePeer (false).Uri);
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

            localPeer.RaisePeerFound (manager.InfoHashes.V1OrV2, rig.CreatePeer (false).Uri);
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
            var engine = EngineHelpers.Create ();
            var tmp = TempDir.Create ();
            var cachePath = Path.Combine (tmp.Path, "test.file");
            using (var file = File.Create (cachePath)) { }
            Assert.ThrowsAsync<ArgumentException> (() => engine.UpdateSettingsAsync (new EngineSettingsBuilder { CacheDirectory = cachePath }.ToSettings ()));
        }

        [Test]
        public async Task ContainingDirectory_InvalidCharacters ()
        {
            // You can't manually add peers to private torrents
            using var rig = TestRig.CreateMultiFile (new TestWriter ());
            await rig.Engine.RemoveAsync (rig.Engine.Torrents[0]);

            var editor = new TorrentEditor (rig.TorrentDict);
            editor.CanEditSecureMetadata = true;
            editor.Name = $"{Path.GetInvalidPathChars()[0]}test{Path.GetInvalidPathChars ()[0]}";

            var manager = await rig.Engine.AddAsync (editor.ToTorrent (), "path", new TorrentSettings ());
            Assert.IsFalse(manager.ContainingDirectory.Contains (manager.Torrent.Name));
            Assert.IsTrue (manager.ContainingDirectory.StartsWith (manager.SavePath));
            Assert.AreEqual (Path.GetFullPath (manager.ContainingDirectory), manager.ContainingDirectory);
            Assert.AreEqual (Path.GetFullPath (manager.SavePath), manager.SavePath);
        }

        [Test]
        public async Task ContainingDirectory_PathBusting ()
        {
            // You can't manually add peers to private torrents
            using var rig = TestRig.CreateMultiFile (new TestWriter ());
            await rig.Engine.RemoveAsync (rig.Engine.Torrents[0]);

            var editor = new TorrentEditor (rig.TorrentDict);
            editor.CanEditSecureMetadata = true;
            editor.Name = $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}test{Path.GetInvalidPathChars ()[0]}";

            Assert.ThrowsAsync<ArgumentException> (() => rig.Engine.AddAsync (editor.ToTorrent (), "path", new TorrentSettings ()));
        }

        [Test]
        [TestCase (true)]
        [TestCase (false)]
        public async Task UsePartialFiles_InitiallyOff_ChangeFullPath_ToggleOn (bool createFile)
        {
            var writer = new TestWriter ();
            var pieceLength = Constants.BlockSize * 4;
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (usePartialFiles: false), EngineHelpers.Factories.WithPieceWriterCreator (t => writer));
            var torrent = TestRig.CreateMultiFileTorrent (TorrentFile.Create (pieceLength, Constants.BlockSize, Constants.BlockSize * 2, Constants.BlockSize * 3), pieceLength, out BEncoding.BEncodedDictionary _);

            using var tempDir = TempDir.Create ();
            var manager = await engine.AddAsync (torrent, tempDir.Path);
            Assert.AreEqual (manager.Files[0].DownloadCompleteFullPath, manager.Files[0].DownloadIncompleteFullPath);

            var newPath = Path.GetFullPath (Path.Combine (tempDir.Path, "new_full_path.fake"));

            await manager.MoveFileAsync (manager.Files[0], newPath);
            Assert.AreEqual (newPath, manager.Files[0].FullPath);
            Assert.AreEqual (newPath, manager.Files[0].DownloadCompleteFullPath);
            Assert.AreEqual (newPath, manager.Files[0].DownloadIncompleteFullPath);

            if (createFile)
                await writer.CreateAsync (manager.Files[0], FileCreationOptions.PreferSparse);

            var settings = new EngineSettingsBuilder (engine.Settings) { UsePartialFiles = true }.ToSettings ();
            await engine.UpdateSettingsAsync (settings);
            Assert.AreEqual (newPath + TorrentFileInfo.IncompleteFileSuffix, manager.Files[0].FullPath);
            Assert.AreEqual (newPath, manager.Files[0].DownloadCompleteFullPath);
            Assert.AreEqual (newPath + TorrentFileInfo.IncompleteFileSuffix, manager.Files[0].DownloadIncompleteFullPath);

            if (createFile) {
                Assert.IsFalse (writer.FilesWithLength.ContainsKey (newPath));
                Assert.IsTrue (writer.FilesWithLength.ContainsKey (newPath + TorrentFileInfo.IncompleteFileSuffix));
            } else {
                Assert.IsFalse (writer.FilesWithLength.ContainsKey (newPath));
                Assert.IsFalse (writer.FilesWithLength.ContainsKey (newPath + TorrentFileInfo.IncompleteFileSuffix));
            }
        }

        [Test]
        public async Task UsePartialFiles_InitiallyOff_ToggleOn ()
        {
            var pieceLength = Constants.BlockSize * 4;
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (usePartialFiles: false));
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
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (usePartialFiles: true));
            var torrent = TestRig.CreateMultiFileTorrent (TorrentFile.Create (pieceLength, Constants.BlockSize, Constants.BlockSize * 2, Constants.BlockSize * 3), pieceLength, out BEncoding.BEncodedDictionary _);

            var manager = await engine.AddAsync (torrent, "");
            Assert.AreNotEqual (manager.Files[0].DownloadCompleteFullPath, manager.Files[0].DownloadIncompleteFullPath);

            var settings = new EngineSettingsBuilder (engine.Settings) { UsePartialFiles = false }.ToSettings ();
            await engine.UpdateSettingsAsync (settings);
            Assert.AreEqual (manager.Files[0].DownloadCompleteFullPath, manager.Files[0].DownloadIncompleteFullPath);
        }

        [Test]
        [TestCase (true)]
        [TestCase (false)]
        public async Task UsePartialFiles_InitiallyOn_ChangeFullPath_ToggleOff (bool createFile)
        {
            var writer = new TestWriter ();
            var pieceLength = Constants.BlockSize * 4;
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (usePartialFiles: true), EngineHelpers.Factories.WithPieceWriterCreator (t => writer));
            var torrent = TestRig.CreateMultiFileTorrent (TorrentFile.Create (pieceLength, Constants.BlockSize, Constants.BlockSize * 2, Constants.BlockSize * 3), pieceLength, out BEncoding.BEncodedDictionary _);

            using var tempDir = TempDir.Create ();
            var manager = await engine.AddAsync (torrent, tempDir.Path);
            Assert.AreNotEqual (manager.Files[0].DownloadCompleteFullPath, manager.Files[0].DownloadIncompleteFullPath);

            var newPath = Path.GetFullPath (Path.Combine (tempDir.Path, "new_full_path.fake"));

            await manager.MoveFileAsync (manager.Files[0], newPath);
            Assert.AreEqual (newPath + TorrentFileInfo.IncompleteFileSuffix, manager.Files[0].FullPath);
            Assert.AreEqual (newPath, manager.Files[0].DownloadCompleteFullPath);
            Assert.AreEqual (newPath + TorrentFileInfo.IncompleteFileSuffix, manager.Files[0].DownloadIncompleteFullPath);

            if (createFile)
                await writer.CreateAsync (manager.Files[0], FileCreationOptions.PreferSparse);

            var settings = new EngineSettingsBuilder (engine.Settings) { UsePartialFiles = false }.ToSettings ();
            await engine.UpdateSettingsAsync (settings);
            Assert.AreEqual (newPath, manager.Files[0].FullPath);
            Assert.AreEqual (newPath, manager.Files[0].DownloadCompleteFullPath);
            Assert.AreEqual (newPath, manager.Files[0].DownloadIncompleteFullPath);

            if (createFile) {
                Assert.IsFalse (writer.FilesWithLength.ContainsKey (newPath + TorrentFileInfo.IncompleteFileSuffix));
                Assert.IsTrue (writer.FilesWithLength.ContainsKey (newPath));
            } else {
                Assert.IsFalse (writer.FilesWithLength.ContainsKey (newPath));
                Assert.IsFalse (writer.FilesWithLength.ContainsKey (newPath + TorrentFileInfo.IncompleteFileSuffix));
            }
        }

        [Test]
        public void DownloadMetadata_Cancelled ()
        {
            var cts = new CancellationTokenSource ();
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings ());
            var task = engine.DownloadMetadataAsync (new MagnetLink (InfoHashes.FromV1 (new InfoHash (new byte[20]))), cts.Token);
            cts.Cancel ();
            Assert.ThrowsAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public void DownloadMagnetLink_SameTwice ()
        {
            var link = MagnetLink.Parse ("magnet:?xt=urn:btih:1234512345123451234512345123451234512345");
            using var engine = EngineHelpers.Create (EngineHelpers.CreateSettings ());
            var first = engine.AddAsync (link, "");
            Assert.ThrowsAsync<TorrentException> (() => engine.AddAsync (link, ""));
        }

        [Test]
        public void DownloadMetadata_SameTwice ()
        {
            var link = MagnetLink.Parse ("magnet:?xt=urn:btih:1234512345123451234512345123451234512345");
            using var engine = EngineHelpers.Create (EngineHelpers.CreateSettings ());
            var first = engine.DownloadMetadataAsync (link, CancellationToken.None);
            Assert.ThrowsAsync<TorrentException> (() => engine.DownloadMetadataAsync (link, CancellationToken.None));
        }

        class FakeListener : IPeerConnectionListener
        {
            public IPEndPoint LocalEndPoint { get; set; }
            public IPEndPoint PreferredLocalEndPoint { get; set; }
            public ListenerStatus Status { get; }

#pragma warning disable 0067
            public event EventHandler<PeerConnectionEventArgs> ConnectionReceived;
            public event EventHandler<EventArgs> StatusChanged;
#pragma warning restore 0067

            public FakeListener (int port)
                => (PreferredLocalEndPoint) = (new IPEndPoint (IPAddress.Any, port));

            public void Start ()
            {
            }

            public void Stop ()
            {
            }
        }

        [Test]
        public void GetPortFromListener_ipv4 ()
        {
            var listener = new FakeListener (0);
            var settingsBuilder = new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) { ListenEndPoints = new System.Collections.Generic.Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Any, 0) } } };
            var engine = EngineHelpers.Create (settingsBuilder.ToSettings (), EngineHelpers.Factories.WithPeerConnectionListenerCreator (t => listener));
            Assert.AreSame (engine.PeerListeners.Single (), listener);

            // a port of zero isn't an actual listen port. The listener is not bound.
            listener.LocalEndPoint = null;
            listener.PreferredLocalEndPoint = new IPEndPoint (IPAddress.Any, 0);
            Assert.AreEqual (null, engine.GetOverrideOrActualListenPort ("ipv4"));

            // The listener is unbound, but it should eventually bind to 1221
            listener.LocalEndPoint = null;
            listener.PreferredLocalEndPoint = new IPEndPoint (IPAddress.Any, 1221);
            Assert.AreEqual (1221, engine.GetOverrideOrActualListenPort ("ipv4"));

            // The bound port is 1423, the preferred is zero
            listener.LocalEndPoint = new IPEndPoint (IPAddress.Any, 1425);
            listener.PreferredLocalEndPoint = new IPEndPoint (IPAddress.Any, 0);
            Assert.AreEqual (1425, engine.GetOverrideOrActualListenPort ("ipv4"));
        }

        [Test]
        public async Task SaveRestoreState_NoTorrents ()
        {
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings ());
            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync (), engine.Factories);
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

            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (cacheDirectory: tmpDir.Path));
            TorrentManager torrentManager;
            if (addStreaming)
                torrentManager = await engine.AddStreamingAsync (torrent, "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = true }.ToSettings ());
            else
                torrentManager = await engine.AddAsync (torrent, "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = true }.ToSettings ());

            await torrentManager.SetFilePriorityAsync (torrentManager.Files[0], Priority.High);
            await torrentManager.MoveFileAsync (torrentManager.Files[1], Path.GetFullPath ("some_fake_path.txt"));

            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync (), engine.Factories);
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
            Assert.AreEqual (engine.Torrents[0].Torrent.Name, restoredEngine.Torrents[0].Torrent.Name);
            Assert.AreEqual (engine.Torrents[0].SavePath, restoredEngine.Torrents[0].SavePath);
            Assert.AreEqual (engine.Torrents[0].Settings, restoredEngine.Torrents[0].Settings);
            Assert.AreEqual (engine.Torrents[0].InfoHashes, restoredEngine.Torrents[0].InfoHashes);
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
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings ());
            if (addStreaming)
                await engine.AddStreamingAsync (new MagnetLink (new InfoHash (new byte[20]), "test"), "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings ());
            else
                await engine.AddAsync (new MagnetLink (new InfoHash (new byte[20]), "test"), "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings ());

            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync (), engine.Factories);
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
            Assert.AreEqual (engine.Torrents[0].SavePath, restoredEngine.Torrents[0].SavePath);
            Assert.AreEqual (engine.Torrents[0].Settings, restoredEngine.Torrents[0].Settings);
            Assert.AreEqual (engine.Torrents[0].InfoHashes, restoredEngine.Torrents[0].InfoHashes);
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

            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (cacheDirectory: tmpDir.Path));
            var torrentManager = await engine.AddStreamingAsync (metadataFile, "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = true }.ToSettings ());
            await torrentManager.SetFilePriorityAsync (torrentManager.Files[0], Priority.High);
            await torrentManager.MoveFileAsync (torrentManager.Files[1], Path.GetFullPath ("some_fake_path.txt"));

            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync (), engine.Factories);
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
            Assert.AreEqual (engine.Torrents[0].Torrent.Name, restoredEngine.Torrents[0].Torrent.Name);
            Assert.AreEqual (engine.Torrents[0].SavePath, restoredEngine.Torrents[0].SavePath);
            Assert.AreEqual (engine.Torrents[0].Settings, restoredEngine.Torrents[0].Settings);
            Assert.AreEqual (engine.Torrents[0].InfoHashes, restoredEngine.Torrents[0].InfoHashes);
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

            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (cacheDirectory: tmpDir.Path));
            await engine.AddStreamingAsync (metadataFile, "mySaveDirectory", new TorrentSettingsBuilder { CreateContainingDirectory = false }.ToSettings ());

            var restoredEngine = await ClientEngine.RestoreStateAsync (await engine.SaveStateAsync (), engine.Factories);
            Assert.AreEqual (engine.Settings, restoredEngine.Settings);
            Assert.AreEqual (engine.Torrents[0].Torrent.Name, restoredEngine.Torrents[0].Torrent.Name);
            Assert.AreEqual (engine.Torrents[0].SavePath, restoredEngine.Torrents[0].SavePath);
            Assert.AreEqual (engine.Torrents[0].Settings, restoredEngine.Torrents[0].Settings);
            Assert.AreEqual (engine.Torrents[0].InfoHashes, restoredEngine.Torrents[0].InfoHashes);
            Assert.AreEqual (engine.Torrents[0].MagnetLink.ToV1String (), restoredEngine.Torrents[0].MagnetLink.ToV1String ());

            Assert.AreEqual (engine.Torrents[0].Files.Count, restoredEngine.Torrents[0].Files.Count);
            for (int i = 0; i < engine.Torrents.Count; i++) {
                Assert.AreEqual (engine.Torrents[0].Files[i].FullPath, restoredEngine.Torrents[0].Files[i].FullPath);
                Assert.AreEqual (engine.Torrents[0].Files[i].Priority, restoredEngine.Torrents[0].Files[i].Priority);
            }
        }

        [Test]
        public async Task StartAsyncAlwaysCreatesEmptyFiles ()
        {
            var writer = new TestWriter ();
            var files = TorrentFile.Create (Constants.BlockSize * 4, 0, 1, 2, 3);
            using var accessor = TempDir.Create ();
            using var rig = TestRig.CreateMultiFile (files, Constants.BlockSize * 4, writer, baseDirectory: accessor.Path);

            for (int i = 0; i < 2; i++) {

                await rig.Manager.StartAsync ();
                Assert.DoesNotThrowAsync (() => rig.Manager.WaitForState (TorrentState.Downloading), "Started");
                Assert.IsTrue (writer.FilesWithLength.ContainsKey (rig.Manager.Files[0].FullPath));
                Assert.IsTrue (rig.Manager.Files[0].BitField.AllTrue);

                // Files can be moved after they have been created.
                await rig.Manager.MoveFileAsync (rig.Manager.Files[0], rig.Manager.Files[0].FullPath + "new_path");

                await rig.Manager.StopAsync ();
                Assert.DoesNotThrowAsync (() => rig.Manager.WaitForState (TorrentState.Stopped), "Stopped");
                writer.FilesWithLength.Remove (rig.Manager.Files[0].FullPath);
            }
        }

        [Test]
        public async Task StartAsync_DoesNotCreateDoNotDownloadPriority ()
        {
            using var writer = new TestWriter ();
            var files = TorrentFile.Create (Constants.BlockSize * 4, 0, 1, 2, 3);
            using var accessor = TempDir.Create ();
            using var rig = TestRig.CreateMultiFile (files, Constants.BlockSize * 4, writer, baseDirectory: accessor.Path);

            foreach (var file in rig.Manager.Files)
                await rig.Manager.SetFilePriorityAsync (file, Priority.DoNotDownload);

            await rig.Manager.StartAsync ();

            foreach (var file in rig.Manager.Files)
                Assert.IsFalse (await writer.ExistsAsync (file));
        }

        [Test]
        public async Task StartAsync_CreatesAllImplicatedFiles ()
        {
            using var writer = new TestWriter ();
            var files = TorrentFile.Create (Constants.BlockSize * 4, 0, 1, Constants.BlockSize * 4, 3);
            using var accessor = TempDir.Create ();
            using var rig = TestRig.CreateMultiFile (files, Constants.BlockSize * 4, writer, baseDirectory: accessor.Path);

            foreach (var file in rig.Manager.Files)
                await rig.Manager.SetFilePriorityAsync (file, file.Length == 1 ? Priority.Normal : Priority.DoNotDownload);

            await rig.Manager.StartAsync ();
            await rig.Manager.WaitForState (TorrentState.Downloading);

            Assert.IsFalse (await writer.ExistsAsync (rig.Manager.Files[0]));
            Assert.IsTrue (await writer.ExistsAsync (rig.Manager.Files[1]));
            Assert.IsTrue (await writer.ExistsAsync (rig.Manager.Files[2]));
            Assert.IsFalse (await writer.ExistsAsync (rig.Manager.Files[3]));
        }

        [Test]
        public async Task StartAsync_SetPriorityCreatesAllImplicatedFiles ()
        {
            using var writer = new TestWriter ();
            var files = TorrentFile.Create (Constants.BlockSize * 4, 0, 1, Constants.BlockSize * 4, Constants.BlockSize * 4);
            using var accessor = TempDir.Create ();
            using var rig = TestRig.CreateMultiFile (files, Constants.BlockSize * 4, writer, baseDirectory: accessor.Path);

            foreach (var file in rig.Manager.Files)
                await rig.Manager.SetFilePriorityAsync (file, Priority.DoNotDownload);

            await rig.Manager.StartAsync ();

            await rig.Manager.SetFilePriorityAsync (rig.Manager.Files[0], Priority.Normal);
            Assert.IsTrue (await writer.ExistsAsync (rig.Manager.Files[0]));
            Assert.IsTrue (rig.Manager.Files[0].BitField.AllTrue);
            Assert.IsFalse (await writer.ExistsAsync (rig.Manager.Files[1]));

            await rig.Manager.SetFilePriorityAsync (rig.Manager.Files[1], Priority.Normal);
            Assert.IsTrue (await writer.ExistsAsync (rig.Manager.Files[1]));
            Assert.IsTrue (await writer.ExistsAsync (rig.Manager.Files[2]));
            Assert.IsFalse (await writer.ExistsAsync (rig.Manager.Files[3]));
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
