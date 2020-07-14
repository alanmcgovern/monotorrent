﻿//
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;
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
        PeerId Peer { get; set; }
        TestWriter PieceWriter { get; set; }
        EngineSettings Settings { get; set; }
        ManualTrackerManager TrackerManager { get; set; }

        [SetUp]
        public void Setup ()
        {
            conn = new ConnectionPair ().WithTimeout ();
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
            Peer = new PeerId (new Peer ("", new Uri ("ipv4://123.123.123.123:12345"), EncryptionTypes.All), conn.Outgoing, Manager.Bitfield?.Clone ().SetAll (false));
        }

        [TearDown]
        public void Teardown ()
        {
            conn.Dispose ();
            DiskManager.Dispose ();
        }

        [Test]
        public async Task AddPeers_TooMany ()
        {
            Manager.Settings.MaximumConnections = 100;
            var peers = new List<Peer> ();
            for (int i = 0; i < Manager.Settings.MaximumPeerDetails + 100; i++)
                peers.Add (new Peer ("", new Uri ($"ipv4://192.168.0.1:{i + 1000}")));
            var added = await Manager.AddPeersAsync (peers);
            Assert.AreEqual (added, Manager.Settings.MaximumPeerDetails, "#1");
            Assert.AreEqual (added, Manager.Peers.AvailablePeers.Count, "#2");
        }

        [Test]
        public async Task AddPeers_PeerExchangeMessage ()
        {
            var peer = new byte[] { 192, 168, 0, 1, 100, 0, 192, 168, 0, 2, 101, 0 };
            var dotF = new byte[] { 0, 1 << 1 }; // 0x2 means is a seeder
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
                Manager.Mode = mode;
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

            await TrackerManager.AddTrackerAsync (new Uri ("http://test.tracker"));
            TrackerManager.RaiseAnnounceComplete (TrackerManager.Tiers.Single ().ActiveTracker, true, new[] { new Peer ("One", new Uri ("ipv4://1.1.1.1:1111")), new Peer ("Two", new Uri ("ipv4://2.2.2.2:2222")) });

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

            // ConnectionManager should add the PeerId to the Connected list whenever
            // an outgoing connection is made, or an incoming one is received.
            Assert.IsFalse (Manager.Peers.ConnectedPeers.Contains (Peer), "#3");
        }

        [Test]
        public async Task AnnounceWhenComplete ()
        {
            await TrackerManager.AddTrackerAsync (new Uri ("http://1.1.1.1"));
            Manager.LoadFastResume (new FastResume (Manager.InfoHash, Manager.Bitfield.Clone ().SetAll (true), Manager.Bitfield.Clone ().SetAll (false)));

            Manager.Bitfield[0] = false;
            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;

            Assert.AreEqual (1, TrackerManager.Announces.Count, "#0");
            Assert.AreEqual (TorrentState.Downloading, Manager.State, "#0b");
            Assert.AreEqual (TorrentEvent.None, TrackerManager.Announces[0].Item2, "#0");

            Manager.Bitfield[0] = true;
            TrackerManager.Announces.Clear ();
            mode.Tick (0);
            Assert.AreEqual (TorrentState.Seeding, Manager.State, "#0c");

            Assert.AreEqual (2, TrackerManager.Announces.Count, "#1");
            Assert.AreEqual (null, TrackerManager.Announces[0].Item1, "#2");
            Assert.AreEqual (TorrentEvent.None, TrackerManager.Announces[0].Item2, "#3");
            Assert.AreEqual (null, TrackerManager.Announces[1].Item1, "#2");
            Assert.AreEqual (TorrentEvent.Completed, TrackerManager.Announces[1].Item2, "#4");
        }

        [Test]
        public void MismatchedInfoHash ()
        {
            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            var peer = PeerId.CreateNull (Manager.Bitfield.Length);
            var handshake = new HandshakeMessage (new InfoHash (Enumerable.Repeat ((byte)15, 20).ToArray ()), "peerid", VersionInfo.ProtocolStringV100);

            Assert.Throws<TorrentException> (() => Manager.Mode.HandleMessage (peer, handshake));
        }

        [Test]
        public void MismatchedProtocolString ()
        {
            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            var peerId = PeerId.CreateNull (Manager.Bitfield.Length);
            var handshake = new HandshakeMessage (Manager.InfoHash, "peerid", "bleurgh");

            Assert.Throws<ProtocolException> (() => Manager.Mode.HandleMessage (peerId, handshake));
        }

        [Test]
        public void EmptyPeerId_PrivateTorrent ()
        {
            var manager = TestRig.CreatePrivate ();
            manager.Mode = new DownloadMode (manager, DiskManager, ConnectionManager, Settings);
            var peer = PeerId.CreateNull (manager.Bitfield.Length);
            peer.Peer.PeerId = null;
            var handshake = new HandshakeMessage (manager.InfoHash, new BEncodedString (Enumerable.Repeat ('c', 20).ToArray ()), VersionInfo.ProtocolStringV100, false);

            manager.Mode.HandleMessage (peer, handshake);
            Assert.AreEqual (handshake.PeerId, peer.PeerID);
        }

        [Test]
        public void EmptyPeerId_PublicTorrent ()
        {
            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            var peer = PeerId.CreateNull (Manager.Bitfield.Length);
            peer.Peer.PeerId = null;
            var handshake = new HandshakeMessage (Manager.InfoHash, new BEncodedString (Enumerable.Repeat ('c', 20).ToArray ()), VersionInfo.ProtocolStringV100, false);

            Manager.Mode.HandleMessage (peer, handshake);
            Assert.AreEqual (handshake.PeerId, peer.PeerID);
        }

        [Test]
        public void MismatchedPeerId_PrivateTorrent ()
        {
            var manager = TestRig.CreatePrivate ();
            manager.Mode = new DownloadMode (manager, DiskManager, ConnectionManager, Settings);
            var peer = PeerId.CreateNull (manager.Bitfield.Length);
            var handshake = new HandshakeMessage (manager.InfoHash, new BEncodedString (Enumerable.Repeat ('c', 20).ToArray ()), VersionInfo.ProtocolStringV100, false);

            Assert.Throws<TorrentException> (() => manager.Mode.HandleMessage (peer, handshake));
        }

        [Test]
        public void MismatchedPeerId_PublicTorrent ()
        {
            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            var peer = PeerId.CreateNull (Manager.Bitfield.Length);
            var handshake = new HandshakeMessage (Manager.InfoHash, new BEncodedString (Enumerable.Repeat ('c', 20).ToArray ()), VersionInfo.ProtocolStringV100, false);

            Assert.DoesNotThrow (() => Manager.Mode.HandleMessage (peer, handshake));
            Assert.AreEqual (peer.PeerID, handshake.PeerId);
        }

        [Test]
        public async Task PauseDownloading ()
        {
            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);

            Assert.AreEqual (TorrentState.Downloading, Manager.State);
            await Manager.PauseAsync ();
            Assert.AreEqual (TorrentState.Paused, Manager.State);
        }

        [Test]
        public async Task PauseSeeding ()
        {
            Manager.LoadFastResume (new FastResume (Manager.InfoHash, Manager.Bitfield.Clone ().SetAll (true), Manager.Bitfield.Clone ().SetAll (false)));
            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);

            Assert.AreEqual (TorrentState.Seeding, Manager.State);
            await Manager.PauseAsync ();
            Assert.AreEqual (TorrentState.Paused, Manager.State);
        }

        [Test]
        public void PartialProgress_AllDownloaded_AllDownloadable ()
        {
            for (int i = 0; i < Manager.Torrent.Pieces.Count; i++)
                Manager.OnPieceHashed (i, true);

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();

            Assert.AreEqual (100.0, Manager.Progress, "#3");
            Assert.AreEqual (100.0, Manager.PartialProgress, "#4");
            Assert.AreEqual (TorrentState.Seeding, Manager.State, "#5");
        }

        [Test]
        public void PartialProgress_AllDownloaded_SomeDownloadable ()
        {
            for (int i = 0; i < Manager.Torrent.Pieces.Count; i++)
                Manager.OnPieceHashed (i, true);

            foreach (var file in Manager.Files)
                file.Priority = Priority.DoNotDownload;
            Manager.Files.Last ().Priority = Priority.Normal;

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();

            Assert.AreNotEqual (0, Manager.Progress, "#3");
            Assert.AreEqual (100.0, Manager.PartialProgress, "#4");
            Assert.AreEqual (TorrentState.Seeding, Manager.State, "#5");
        }

        [Test]
        public void PartialProgress_NoneDownloaded ()
        {
            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();

            Assert.AreEqual (0, Manager.Progress, "#1");
            Assert.AreEqual (0, Manager.PartialProgress, "#2");
            Assert.AreEqual (TorrentState.Downloading, Manager.State, "#3");
        }

        [Test]
        public void PartialProgress_NoneDownloaded_AllDoNotDownload ()
        {
            foreach (var file in Manager.Files)
                file.Priority = Priority.DoNotDownload;

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();

            Assert.AreEqual (0, Manager.Progress, "#1");
            Assert.AreEqual (0, Manager.PartialProgress, "#2");
            Assert.AreEqual (TorrentState.Downloading, Manager.State, "#3");
        }

        [Test]
        public void PartialProgress_RelatedDownloaded ()
        {
            Manager.OnPieceHashed (0, true);

            foreach (var file in Manager.Files)
                file.Priority = Priority.DoNotDownload;
            Manager.Files.First ().Priority = Priority.Normal;

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();

            Assert.That (Manager.Progress, Is.GreaterThan (0.0), "#3a");
            Assert.That (Manager.Progress, Is.LessThan (100.0), "#3b");

            Assert.That (Manager.PartialProgress, Is.EqualTo (100.0), "#4");
            Assert.AreEqual (TorrentState.Seeding, Manager.State, "#5");
        }

        [Test]
        public void PartialProgress_RelatedDownloaded2 ()
        {
            var lastFile = Manager.Files.Last ();
            Manager.OnPieceHashed (Manager.Torrent.Pieces.Count - 1, true);

            foreach (var file in Manager.Files)
                file.Priority = Priority.DoNotDownload;
            lastFile.Priority = Priority.Normal;

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();

            var totalPieces = lastFile.EndPieceIndex - lastFile.StartPieceIndex + 1;
            Assert.That (Manager.PartialProgress, Is.EqualTo (100.0 / totalPieces).Within (1).Percent, "#1");
            Assert.AreEqual (TorrentState.Downloading, Manager.State, "#2");
        }

        [Test]
        public async Task PartialProgress_RelatedDownloaded_FileAdded ()
        {
            Manager.OnPieceHashed (0, true);

            foreach (var file in Manager.Files)
                file.Priority = Priority.DoNotDownload;
            Manager.Files.First ().Priority = Priority.Normal;

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();
            Assert.AreEqual (TorrentState.Seeding, Manager.State, "#1");

            Manager.Files.Skip (1).First ().Priority = Priority.Normal;
            var oldStateTask = new TaskCompletionSource<TorrentState> ();
            var newStateTask = new TaskCompletionSource<TorrentState> ();
            Manager.TorrentStateChanged += (object sender, TorrentStateChangedEventArgs e) => {
                oldStateTask.SetResult (e.OldState);
                newStateTask.SetResult (e.NewState);
            };
            mode.UpdateSeedingDownloadingState ();

            var oldState = await oldStateTask.Task;
            var newState = await newStateTask.Task;

            Assert.That (Manager.Progress, Is.GreaterThan (0.0), "#3a");
            Assert.That (Manager.Progress, Is.LessThan (100.0), "#3b");

            Assert.That (Manager.PartialProgress, Is.LessThan (100.0), "#4");
            Assert.AreEqual (TorrentState.Downloading, Manager.State, "#5");

            Assert.AreEqual (TorrentState.Seeding, oldState, "#6");
            Assert.AreEqual (TorrentState.Downloading, newState, "#7");
        }

        [Test]
        public void PartialProgress_UnrelatedDownloaded_AllDoNotDownload ()
        {
            Manager.OnPieceHashed (0, true);

            foreach (var file in Manager.Files)
                file.Priority = Priority.DoNotDownload;

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();

            Assert.AreNotEqual (0, Manager.Progress, "#3");
            Assert.AreEqual (0, Manager.PartialProgress, "#4");
        }

        [Test]
        public void PartialProgress_UnrelatedDownloaded_SomeDoNotDownload ()
        {
            Manager.OnPieceHashed (0, true);

            foreach (var file in Manager.Files)
                file.Priority = Priority.DoNotDownload;
            Manager.Files.Last ().Priority = Priority.Normal;

            var mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.UpdateSeedingDownloadingState ();

            Assert.AreNotEqual (0, Manager.Progress, "#3");
            Assert.AreEqual (0, Manager.PartialProgress, "#4");
        }
    }
}
