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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PieceWriters;
using NUnit.Framework;

namespace MonoTorrent.Client
{

    [TestFixture]
    public class TorrentManagerTest
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
            Peer = new PeerId (new Peer ("", new Uri ("ipv4://123.123.123.123"), Encryption.EncryptionTypes.All), Manager, conn.Outgoing);
        }

        [TearDown]
        public void Teardown()
        {
            conn.Dispose();
            DiskManager.Dispose ();
        }

        [Test]
        public void AddConnectionToStoppedManager()
        {
            Manager.Mode = new StoppedMode (Manager, DiskManager, ConnectionManager, Settings);

            Assert.IsTrue (Peer.Connection.Connected, "#1");
            Manager.HandlePeerConnected (Peer);
            Assert.IsFalse (Peer.Connection.Connected, "#2");
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
               var peersTask = new TaskCompletionSource<PeerExchangeAdded> ();
                Manager.PeersFound += (o, e) => {
                    if (e is PeerExchangeAdded args)
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
            var peersTask = new TaskCompletionSource<PeerExchangeAdded> ();
            manager.PeersFound += (o, e) => {
                if (e is PeerExchangeAdded args)
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
        public async Task StartingMode_NoTrackers ()
        {
            Assert.IsNull (Manager.TrackerManager.CurrentTracker, "#1");
            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.StartingTask;
            Assert.IsInstanceOf<DownloadMode> (Manager.Mode, "#2");
        }

        [Test]
        public async Task StartingMode_Announce ()
        {
            TrackerManager.AddTracker ("http://1.1.1.1");

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.StartingTask;

            Assert.AreEqual (1, TrackerManager.Announces.Count, "#1");
            Assert.AreEqual (TrackerManager.CurrentTracker, TrackerManager.Announces[0].Item1, "#2");
            Assert.AreEqual (TorrentEvent.Started, TrackerManager.Announces[0].Item2, "#3");
        }

        [Test]
        public async Task StartingMode_StateChanges_AlreadyHashed ()
        {
            var modeChanged = new List<Mode> ();
            Manager.ModeChanged += (oldMode, newMode) => modeChanged.Add (newMode);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.HashChecked = true;
            Manager.Mode = mode;
            await mode.StartingTask;

            Assert.AreEqual (2, modeChanged.Count, "#1");
            Assert.IsInstanceOf<StartingMode> (modeChanged[0], "#2");
            Assert.IsInstanceOf<DownloadMode> (modeChanged[1], "#2");
        }

        [Test]
        public async Task StartingMode_StateChanges_NeedsHashing ()
        {
            var modeChanged = new List<Mode> ();
            Manager.ModeChanged += (oldMode, newMode) => modeChanged.Add (newMode);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.HashChecked = false;
            Manager.Mode = mode;
            await mode.StartingTask;

            Assert.AreEqual (3, modeChanged.Count, "#1");
            Assert.IsInstanceOf<StartingMode> (modeChanged[0], "#2");
            Assert.IsInstanceOf<HashingMode> (modeChanged[1], "#3");
            Assert.IsInstanceOf<DownloadMode> (modeChanged[2], "#4");
        }

        [Test]
        public async Task StartingMode_FastResume_NoneExist()
        {
            var bf = Manager.Bitfield.Clone ().SetAll (true);
            Manager.LoadFastResume (new FastResume (Manager.InfoHash, bf));

            Assert.IsTrue (Manager.Bitfield.AllTrue, "#1");
            foreach (TorrentFile file in Manager.Torrent.Files)
                Assert.IsTrue (file.BitField.AllTrue, "#2." + file.Path);

            var startingMode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = startingMode;
            await startingMode.StartingTask;

            Assert.IsTrue(Manager.Bitfield.AllFalse, "#3");
            foreach (var file in Manager.Torrent.Files)
                Assert.IsTrue(file.BitField.AllFalse, "#4." + file.Path);
        }

        [Test]
        public async Task StartingMode_FastResume_SomeExist()
        {
            PieceWriter.FilesThatExist.AddRange(new[]{
                Manager.Torrent.Files [0],
                Manager.Torrent.Files [2],
            });
            var bf = Manager.Bitfield.Clone ().SetAll (true);
            Manager.LoadFastResume(new FastResume(Manager.InfoHash, bf));

            Assert.IsTrue (Manager.Bitfield.AllTrue, "#1");
            foreach (TorrentFile file in Manager.Torrent.Files)
                Assert.IsTrue (file.BitField.AllTrue, "#2." + file.Path);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.StartingTask;

            Assert.IsTrue(Manager.Bitfield.AllFalse, "#3");
            foreach (TorrentFile file in Manager.Torrent.Files)
                Assert.IsTrue(file.BitField.AllFalse, "#4." + file.Path);
        }

        [Test]
        public async Task StoppingMode_Announce ()
        {
            TrackerManager.AddTracker ("http://1.1.1.1");

            var mode = new StoppingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.WaitForStoppingToComplete ();

            Assert.AreEqual (1, TrackerManager.Announces.Count, "#1");
            Assert.AreEqual (TrackerManager.CurrentTracker, TrackerManager.Announces[0].Item1, "#2");
            Assert.AreEqual (TorrentEvent.Stopped, TrackerManager.Announces[0].Item2, "#3");
        }

        [Test]
        public void DownloadMode_AnnounceWhenComplete ()
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

        [Test]
        public async Task HashingMode_ReadZero()
        {
            PieceWriter.FilesThatExist.AddRange(new[]{
                Manager.Torrent.Files [0],
                Manager.Torrent.Files [2],
            });

            PieceWriter.DoNotReadFrom.AddRange(new[]{
                Manager.Torrent.Files[0],
                Manager.Torrent.Files[3],
            });

            var bf = Manager.Bitfield.Clone ().SetAll (true);
            Manager.LoadFastResume(new FastResume(Manager.InfoHash, bf));

            Assert.IsTrue (Manager.Bitfield.AllTrue, "#1");
            foreach (TorrentFile file in Manager.Torrent.Files)
                Assert.IsTrue (file.BitField.AllTrue, "#2." + file.Path);

            var mode = new HashingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.WaitForHashingToComplete ();

            Assert.IsTrue(Manager.Bitfield.AllFalse, "#3");
            foreach (TorrentFile file in Manager.Torrent.Files)
                Assert.IsTrue (file.BitField.AllFalse, "#4." + file.Path);
        }
    }
}
