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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


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
            rig = TestRig.CreateMultiFile (new TestWriter ());
            conn = new ConnectionPair ().WithTimeout ();
        }

        [TearDown]
        public void Teardown ()
        {
            rig.Dispose ();
            conn.Dispose ();
        }

        [Test]
        public async Task AddPeers_Dht ()
        {
            var dht = new ManualDhtEngine ();
            await rig.Engine.RegisterDhtAsync (dht);

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
            var editor = new TorrentEditor (rig.Torrent) {
                CanEditSecureMetadata = true,
                Private = true
            };

            var manager = new TorrentManager (editor.ToTorrent (), "path", new TorrentSettings ());
            await rig.Engine.Register (manager);

            var dht = new ManualDhtEngine ();
            await rig.Engine.RegisterDhtAsync (dht);

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
            var localPeer = new ManualLocalPeerListener ();
            await rig.Engine.RegisterLocalPeerDiscoveryAsync (localPeer);

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

            var manager = new TorrentManager (editor.ToTorrent (), "path", new TorrentSettings ());
            await rig.Engine.Register (manager);

            var localPeer = new ManualLocalPeerListener ();
            await rig.Engine.RegisterLocalPeerDiscoveryAsync (localPeer);

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
        public void DownloadMetadata_Cancelled ()
        {
            var cts = new CancellationTokenSource ();
            var task = rig.Engine.DownloadMetadataAsync (new MagnetLink (new InfoHash (new byte[20])), cts.Token);
            cts.Cancel ();
            Assert.ThrowsAsync<OperationCanceledException> (() => task);
        }

        [Test]
        public async Task ReregisterManager ()
        {
            var downloadLimiters = rig.Manager.DownloadLimiters.ToArray ();
            var uploadLimiters = rig.Manager.UploadLimiters.ToArray ();

            await rig.Engine.Unregister (rig.Manager);
            Assert.IsNull (rig.Manager.Engine, "#1");
            CollectionAssert.AreNotEquivalent (downloadLimiters, rig.Manager.DownloadLimiters, "#2");
            CollectionAssert.AreNotEquivalent (uploadLimiters, rig.Manager.UploadLimiters, "#3");
            Assert.IsFalse (rig.Engine.ConnectionManager.Contains (rig.Manager), "#4");

            await rig.Engine.Register (rig.Manager);
            Assert.AreEqual (rig.Engine, rig.Manager.Engine, "#5");
            CollectionAssert.AreEquivalent (downloadLimiters, rig.Manager.DownloadLimiters, "#6");
            CollectionAssert.AreEquivalent (uploadLimiters, rig.Manager.UploadLimiters, "#7");
            Assert.IsTrue (rig.Engine.ConnectionManager.Contains (rig.Manager), "#8");
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
