//
// DownloadModeTests.cs
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
using System.Threading.Tasks;

using MonoTorrent.Client.Messages.Libtorrent;

using NUnit.Framework;

namespace MonoTorrent.Client.Modes
{
    [TestFixture]
    public class DownloadModeTests
    {
        ConnectionPair conn;
        ConnectionManager ConnectionManager { get; set; }
        DiskManager DiskManager { get; set; }
        TorrentManager Manager { get; set; }
        PeerId Peer { get; set ; }
        TestWriter PieceWriter { get; set; }
        EngineSettings Settings { get; set; }
        ManualTrackerManager TrackerManager { get; set; }

        [SetUp]
        public void Setup()
        {
            conn = new ConnectionPair().WithTimeout ();
            Settings = new EngineSettings ();
            PieceWriter = new TestWriter ();
            DiskManager = new DiskManager (Settings, PieceWriter);
            ConnectionManager = new ConnectionManager ("LocalPeerId", Settings, DiskManager);
            TrackerManager = new ManualTrackerManager ();

            int[] fileSizes = {
                Piece.BlockSize / 2,
                Piece.BlockSize * 32,
                Piece.BlockSize * 2,
                Piece.BlockSize * 13,
            };
            Manager = TestRig.CreateMultiFileManager (fileSizes, Piece.BlockSize * 2);
            Manager.SetTrackerManager (TrackerManager);
            Peer = new PeerId (new Peer ("", new Uri ("ipv4://123.123.123.123"), Encryption.EncryptionTypes.All), conn.Outgoing, Manager.Bitfield?.Clone ().SetAll (false)) {
                ProcessingQueue = true
            };
        }

        [TearDown]
        public void Teardown()
        {
            conn.Dispose();
            DiskManager.Dispose ();
        }

        [Test]
        public async Task AddPeers_PeerExchangeMessage ()
        {
            var peer = new byte[] { 192, 168, 0, 1, 100, 0, 192, 168, 0, 2, 101, 0 };
            var dotF = new byte[] { 0, 1 << 1}; // 0x2 means is a seeder
            var id = PeerId.CreateNull (40);
            id.SupportsFastPeer = true;
            id.SupportsLTMessages = true;

            Mode[] modes = {
                new DownloadMode (Manager, DiskManager, ConnectionManager, Settings),
                new MetadataMode (Manager, DiskManager, ConnectionManager, Settings, "")
            };

            foreach (var mode in modes) {
               var peersTask = new TaskCompletionSource<PeerExchangePeersAdded> ();
                Manager.PeersFound += (o, e) => {
                    if (e is PeerExchangePeersAdded args)
                        peersTask.TrySetResult (args);
                };

                Manager.Peers.ClearAll ();
                var exchangeMessage = new PeerExchangeMessage (13, peer, dotF, null);
                Manager.Mode.HandleMessage (id, exchangeMessage);

                var addedArgs = await peersTask.Task.WithTimeout ();
                Assert.AreEqual (2, addedArgs.NewPeers, "#1");
                Assert.IsFalse (Manager.Peers.AvailablePeers[0].IsSeeder, "#2");
                Assert.IsTrue (Manager.Peers.AvailablePeers[1].IsSeeder, "#3");
            }
        }

        [Test]
        public async Task AddPeers_PeerExchangeMessage_Private ()
        {
            var peer = new byte[] { 192, 168, 0, 1, 100, 0 };
            var dotF = new byte[] { 1 << 0 | 1 << 2 }; // 0x1 means supports encryption, 0x2 means is a seeder
            var id = PeerId.CreateNull (40);
            id.SupportsFastPeer = true;
            id.SupportsLTMessages = true;

            var manager = TestRig.CreatePrivate ();
            manager.Mode = new DownloadMode (manager, DiskManager, ConnectionManager, Settings);
            var peersTask = new TaskCompletionSource<PeerExchangePeersAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is PeerExchangePeersAdded args)
                    peersTask.TrySetResult (args);
            };

            var exchangeMessage = new PeerExchangeMessage (13, peer, dotF, null);
            manager.Mode.HandleMessage (id, exchangeMessage);

            var addedArgs = await peersTask.Task.WithTimeout ();
            Assert.AreEqual (0, addedArgs.NewPeers, "#1");
        }

        [Test]
        public async Task AddPeers_Tracker_Private ()
        {
            var manager = TestRig.CreatePrivate ();
            manager.SetTrackerManager (TrackerManager);

            var peersTask = new TaskCompletionSource<TrackerPeersAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is TrackerPeersAdded args)
                    peersTask.TrySetResult (args);
            };

            TrackerManager.AddTracker ("http://test.tracker");
            TrackerManager.RaiseAnnounceComplete (TrackerManager.CurrentTracker, true, new [] { new Peer ("One", new Uri ("ipv4://1.1.1.1")), new Peer ("Two", new Uri ("ipv4://2.2.2.2")) });

            var addedArgs = await peersTask.Task.WithTimeout ();
            Assert.AreEqual (2, addedArgs.NewPeers, "#1");
            Assert.AreEqual (2, manager.Peers.AvailablePeers.Count, "#2");
        }

        [Test]
        public void AddConnection ()
        {
            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);

            Assert.IsTrue (Peer.Connection.Connected, "#1");
            Manager.HandlePeerConnected (Peer);
            Assert.IsTrue (Peer.Connection.Connected, "#2");
            Assert.IsTrue (Manager.Peers.ConnectedPeers.Contains (Peer), "#3");
        }

        [Test]
        public void AnnounceWhenComplete ()
        {
            TrackerManager.AddTracker ("http://1.1.1.1");

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;

            Assert.AreEqual (0, TrackerManager.Announces.Count, "#1");
            Manager.Bitfield.SetAll (true);
            mode.Tick (0);

            Assert.AreEqual (1, TrackerManager.Announces.Count, "#1");
            Assert.AreEqual (TrackerManager.CurrentTracker, TrackerManager.Announces[0].Item1, "#2");
            Assert.AreEqual (TorrentEvent.Completed, TrackerManager.Announces[0].Item2, "#3");
        }
    }
}
