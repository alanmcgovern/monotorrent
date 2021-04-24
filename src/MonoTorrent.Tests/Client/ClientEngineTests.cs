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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Dht;

using NUnit.Framework;

namespace MonoTorrent.Client
{

    [TestFixture]
    public class ClientEngineTests
    {
        TestRig rig;
        ConnectionPair conn;

        [SetUp]
        public void Setup ()
        {
            DhtEngineFactory.Creator = listener => new ManualDhtEngine ();

            rig = TestRig.CreateMultiFile (new TestWriter ());
            conn = new ConnectionPair ().WithTimeout ();
        }

        [TearDown]
        public void Teardown ()
        {
            DhtEngineFactory.Creator = listener => new DhtEngine (listener);

            rig.Dispose ();
            conn.Dispose ();
        }

        [Test]
        public async Task AddPeers_Dht ()
        {
            var dht = (ManualDhtEngine) rig.Engine.DhtEngine;

            var tcs = new TaskCompletionSource<DhtPeersAdded> ();
            var manager = rig.Engine.Torrents[0];
            manager.PeersFound += (o, e) => {
                if (e is DhtPeersAdded args)
                    tcs.TrySetResult (args);
            };

            dht.RaisePeersFound (manager.InfoHash, new[] { rig.CreatePeer (false).Peer });
            var result = await tcs.Task.WithTimeout (TimeSpan.FromSeconds (5));
            Assert.AreEqual (1, result.NewPeers, "#2");
            Assert.AreEqual (0, result.ExistingPeers, "#3");
            Assert.AreEqual (1, manager.Peers.AvailablePeers.Count, "#4");
        }

        [Test]
        public async Task AddPeers_Dht_Private ()
        {
            // You can't manually add peers to private torrents
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

            dht.RaisePeersFound (manager.InfoHash, new[] { rig.CreatePeer (false).Peer });
            var result = await tcs.Task.WithTimeout (TimeSpan.FromSeconds (5));
            Assert.AreEqual (0, result.NewPeers, "#2");
            Assert.AreEqual (0, result.ExistingPeers, "#3");
            Assert.AreEqual (0, manager.Peers.AvailablePeers.Count, "#4");
        }

        [Test]
        public async Task AddPeers_LocalPeerDiscovery ()
        {
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
        public void CacheDirectory_IsFile_Constructor()
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
        public void DownloadMetadata_Cancelled ()
        {
            var cts = new CancellationTokenSource ();
            var task = rig.Engine.DownloadMetadataAsync (new MagnetLink (new InfoHash (new byte[20])), cts.Token);
            cts.Cancel ();
            Assert.ThrowsAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public async Task StopTest ()
        {
            var hashingState = rig.Manager.WaitForState (TorrentState.Hashing);
            var stoppedState = rig.Manager.WaitForState (TorrentState.Stopped);

            await rig.Manager.StartAsync ();
            Assert.IsTrue (hashingState.Wait (5000), "Started");
            await rig.Manager.StopAsync ();
            Assert.IsTrue (stoppedState.Wait (5000), "Stopped");
        }
    }
}
