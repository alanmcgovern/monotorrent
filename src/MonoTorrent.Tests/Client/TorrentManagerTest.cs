//
// TorrentManagerTest.cs
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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client
{

    [TestFixture]
    public class TorrentManagerTest
    {
        TestRig rig;
        ConnectionPair conn;

        [SetUp]
        public void Setup()
        {
            rig = TestRig.CreateMultiFile (new TestWriter());
            conn = new ConnectionPair().WithTimeout ();
        }
        [TearDown]
        public void Teardown()
        {
            rig.Dispose();
            conn.Dispose();
        }

        [Test]
        public async Task AddConnectionToStoppedManager()
        {
            MessageBundle bundle = new MessageBundle();

            // Create the handshake and bitfield message
            bundle.Messages.Add(new HandshakeMessage(rig.Manager.InfoHash, "11112222333344445555", VersionInfo.ProtocolStringV100));
            bundle.Messages.Add(new BitfieldMessage(rig.Torrent.Pieces.Count));
            byte[] data = bundle.Encode();

            // Add the 'incoming' connection to the engine and send our payload
            rig.Listener.Add(rig.Manager, conn.Incoming);
            await conn.Outgoing.SendAsync (data, 0, data.Length);

            try {
                var received = await conn.Outgoing.ReceiveAsync(data, 0, data.Length);
                Assert.AreEqual (received, 0);
            } catch {
                Assert.IsFalse(conn.Incoming.Connected, "#1");
            }
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

            dht.RaisePeersFound (manager.InfoHash, new [] { rig.CreatePeer (false).Peer });
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

            var manager = new TorrentManager(editor.ToTorrent (), "path", new TorrentSettings());
            await rig.Engine.Register (manager);

            var dht = new ManualDhtEngine ();
            await rig.Engine.RegisterDhtAsync (dht);

            var tcs = new TaskCompletionSource<DhtPeersAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is DhtPeersAdded args)
                    tcs.TrySetResult (args);
            };

            dht.RaisePeersFound (manager.InfoHash, new [] { rig.CreatePeer (false).Peer });
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

            var manager = new TorrentManager(editor.ToTorrent (), "path", new TorrentSettings());
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
        public async Task AddPeers_PeerExchangeMessage ()
        {
            var peer = new byte[] { 192, 168, 0, 1, 100, 0, 192, 168, 0, 2, 101, 0 };
            var dotF = new byte[] { 0, 1 << 1}; // 0x2 means is a seeder
            var id = rig.CreatePeer (true, true);
            id.SupportsLTMessages = true;

            var manager = rig.Manager;
            var mode = new DownloadMode (manager);

            var peersTask = new TaskCompletionSource<PeerExchangeAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is PeerExchangeAdded args)
                    peersTask.TrySetResult (args);
            };

            var exchangeMessage = new PeerExchangeMessage (13, peer, dotF, null);
            mode.HandleMessage (id, exchangeMessage);

            var addedArgs = await peersTask.Task.WithTimeout (5000);
            Assert.AreEqual (2, addedArgs.NewPeers, "#1");
            Assert.IsFalse (manager.Peers.AvailablePeers[0].IsSeeder, "#2");
            Assert.IsTrue (manager.Peers.AvailablePeers[1].IsSeeder, "#3");
        }

        [Test]
        public async Task AddPeers_PeerExchangeMessage_Private ()
        {
            var peer = new byte[] { 192, 168, 0, 1, 100, 0 };
            var dotF = new byte[] { 1 << 0 | 1 << 2 }; // 0x1 means supports encryption, 0x2 means is a seeder
            var id = rig.CreatePeer (true, true);
            id.SupportsLTMessages = true;

            var manager = TestRig.CreatePrivate ();
            var mode = new DownloadMode (manager);
            var peersTask = new TaskCompletionSource<PeerExchangeAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is PeerExchangeAdded args)
                    peersTask.TrySetResult (args);
            };

            var exchangeMessage = new PeerExchangeMessage (13, peer, dotF, null);
            mode.HandleMessage (id, exchangeMessage);

            var addedArgs = await peersTask.Task.WithTimeout (5000);
            Assert.AreEqual (0, addedArgs.NewPeers, "#1");
        }

        [Test]
        public async Task AddPeers_Tracker_Private ()
        {
            var manager = TestRig.CreatePrivate ();
            var tracker = new ManualTrackerManager ();
            manager.SetTrackerManager (tracker);

            var peersTask = new TaskCompletionSource<TrackerPeersAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is TrackerPeersAdded args)
                    peersTask.TrySetResult (args);
            };

            tracker.RaiseAnnounceComplete (tracker.CurrentTracker, true, new [] { rig.CreatePeer (false, true).Peer, rig.CreatePeer (false, true).Peer });

            var addedArgs = await peersTask.Task.WithTimeout (5000);
            Assert.AreEqual (2, addedArgs.NewPeers, "#1");
            Assert.AreEqual (2, manager.Peers.AvailablePeers.Count, "#2");
        }

        [Test]
        public async Task ReregisterManager()
        {
            var hashingTask = rig.Manager.WaitForState(TorrentState.Stopped);
            await rig.Manager.HashCheckAsync(false);
            await hashingTask;

            await rig.Engine.Unregister(rig.Manager);
            TestRig rig2 = TestRig.CreateMultiFile (new TestWriter());
            await rig2.Engine.Unregister(rig2.Manager);
            await rig.Engine.Register(rig2.Manager);

            hashingTask = rig2.Manager.WaitForState(TorrentState.Downloading);
            await rig2.Manager.HashCheckAsync(true);
            await hashingTask;
            await rig2.Manager.StopAsync();

            rig2.Dispose();
        }

        [Test]
        public async Task StopTest()
        {
            var hashingState = rig.Manager.WaitForState(TorrentState.Hashing);
            var stoppedState = rig.Manager.WaitForState(TorrentState.Stopped);

            await rig.Manager.StartAsync();
            Assert.IsTrue(hashingState.Wait(5000), "Started");
            await rig.Manager.StopAsync();
            Assert.IsTrue(stoppedState.Wait(5000), "Stopped");
        }

        [Test]
        public async Task NoAnnouncesTest()
        {
            rig.TorrentDict.Remove("announce-list");
            rig.TorrentDict.Remove("announce");
            Torrent t = Torrent.Load(rig.TorrentDict);
            await rig.Engine.Unregister(rig.Manager);
            TorrentManager manager = new TorrentManager(t, "", new TorrentSettings());
            await rig.Engine.Register(manager);

            AutoResetEvent handle = new AutoResetEvent(false);
            manager.TorrentStateChanged += delegate(object o, TorrentStateChangedEventArgs e) {
                if (e.NewState == TorrentState.Downloading || e.NewState == TorrentState.Stopped)
                    handle.Set();
            };
            await manager.StartAsync();
            handle.WaitOne();
            System.Threading.Thread.Sleep(1000);
            await manager.StopAsync();

            Assert.IsTrue(handle.WaitOne(10000, true), "#1");
            await manager.TrackerManager.Announce();
        }

        [Test]
        public void UnsupportedTrackers ()
        {
            RawTrackerTier tier = new RawTrackerTier {
                "fake://123.123.123.2:5665"
            };
            rig.Torrent.AnnounceUrls.Add (tier);
            TorrentManager manager = new TorrentManager (rig.Torrent, "", new TorrentSettings());
            foreach (MonoTorrent.Client.Tracker.TrackerTier t in manager.TrackerManager.Tiers)
            {
                Assert.IsTrue (t.Trackers.Count > 0, "#1");
            }
        }

        [Test]
        public async Task AnnounceWhenComplete()
        {
            // Check that the engine announces when the download starts, completes
            // and is stopped.
            AutoResetEvent handle = new AutoResetEvent(false);
            rig.Manager.TrackerManager.AnnounceComplete += delegate {
                handle.Set ();
            };

            var downloadingState = rig.Manager.WaitForState (TorrentState.Downloading);

            await rig.Manager.StartAsync();
            await downloadingState;

            Assert.IsTrue (handle.WaitOne(5000, false), "Announce on startup");
            Assert.AreEqual(1, rig.Tracker.AnnouncedAt.Count, "#2");
			Console.WriteLine ("Got start announce. State: {0}. Complete: {1}", rig.Manager.State, rig.Manager.Complete);

            rig.Manager.Bitfield.SetAll(true);
            Assert.IsTrue (handle.WaitOne (5000, false), "Announce when download completes");
            Assert.AreEqual(TorrentState.Seeding, rig.Manager.State, "#3");
            Assert.AreEqual(2, rig.Tracker.AnnouncedAt.Count, "#4");

            await rig.Manager.StopAsync();
            Assert.IsTrue (handle.WaitOne (5000, false), "Announce when torrent stops");
            Assert.AreEqual(3, rig.Tracker.AnnouncedAt.Count, "#6");
        }

        [Test]
        public async Task InvalidFastResume_NoneExist()
        {
            var handle = new ManualResetEvent (false);
            var bf = new BitField (rig.Pieces).Not ();
            rig.Manager.LoadFastResume (new FastResume (rig.Manager.InfoHash, bf));
            rig.Manager.TorrentStateChanged += (o, e) => {
                if (rig.Manager.State == TorrentState.Downloading)
                    handle.Set ();
            };
            await rig.Manager.StartAsync ();
            Assert.IsTrue(handle.WaitOne(), "#1");
            Assert.IsTrue(rig.Manager.Bitfield.AllFalse, "#2");
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.IsTrue(file.BitField.AllFalse, "#3." + file.Path);
        }

        [Test]
        public async Task InvalidFastResume_SomeExist()
        {
            rig.Writer.FilesThatExist.AddRange(new[]{
                rig.Manager.Torrent.Files [0],
                rig.Manager.Torrent.Files [2],
            });
            var handle = new ManualResetEvent(false);
            var bf = new BitField(rig.Pieces).Not();
            rig.Manager.LoadFastResume(new FastResume(rig.Manager.InfoHash, bf));
            rig.Manager.TorrentStateChanged += (o, e) =>
            {
                if (rig.Manager.State == TorrentState.Downloading)
                    handle.Set();
            };
            await rig.Manager.StartAsync();
            Assert.IsTrue(handle.WaitOne(), "#1");
            Assert.IsTrue(rig.Manager.Bitfield.AllFalse, "#2");
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.IsTrue(file.BitField.AllFalse, "#3." + file.Path);
        }

        [Test]
        public async Task HashTorrent_ReadZero()
        {
            rig.Writer.FilesThatExist.AddRange(new[]{
                rig.Manager.Torrent.Files [0],
                rig.Manager.Torrent.Files [2],
            });
            rig.Writer.DoNotReadFrom.AddRange(new[]{
                rig.Manager.Torrent.Files[0],
                rig.Manager.Torrent.Files[3],
            });

            var handle = new ManualResetEvent(false);
            var bf = new BitField(rig.Pieces).Not();
            rig.Manager.LoadFastResume(new FastResume(rig.Manager.InfoHash, bf));
            rig.Manager.TorrentStateChanged += (o, e) =>
            {
                if (rig.Manager.State == TorrentState.Downloading)
                    handle.Set();
            };
            await rig.Manager.StartAsync();
            Assert.IsTrue(handle.WaitOne(), "#1");
            Assert.IsTrue(rig.Manager.Bitfield.AllFalse, "#2");
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.IsTrue (file.BitField.AllFalse, "#3." + file.Path);
        }
    }
}
