//
// StoppingModeTests.cs
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

using MonoTorrent.Client.PieceWriters;

using NUnit.Framework;

namespace MonoTorrent.Client.Modes
{
    [TestFixture]
    public class StoppingModeTests
    {
        ConnectionPair conn;
        ConnectionManager ConnectionManager { get; set; }
        DiskManager DiskManager { get; set; }
        TorrentManager Manager { get; set; }
        PeerId Peer { get; set ; }
        EngineSettings Settings { get; set; }
        ManualTrackerManager TrackerManager { get; set; }

        [SetUp]
        public void Setup()
        {
            conn = new ConnectionPair().WithTimeout ();
            Settings = new EngineSettings ();
            DiskManager = new DiskManager (Settings, new NullWriter ());
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
            Peer = new PeerId (new Peer ("", new Uri ("ipv4://123.123.123.123:5555"), EncryptionTypes.All), conn.Outgoing, Manager.Bitfield?.Clone ().SetAll (false)) {
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
        public void AddConnection ()
        {
            Manager.Mode = new StoppingMode (Manager, DiskManager, ConnectionManager, Settings);

            Assert.IsTrue (Peer.Connection.Connected, "#1");
            Manager.HandlePeerConnected (Peer);
            Assert.IsFalse (Peer.Connection.Connected, "#2");
            Assert.IsFalse (Manager.Peers.ConnectedPeers.Contains (Peer), "#3");
        }

        [Test]
        public async Task Announce_StoppedEvent ()
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
        public async Task DisposeActiveConnections ()
        {
            Manager.Peers.ConnectedPeers.Add(Peer);
            Assert.IsFalse (Peer.Disposed, "#1");

            var mode = new StoppingMode(Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.WaitForStoppingToComplete();

            Assert.IsTrue (Peer.Disposed, "#2");
            Assert.AreEqual (0, Manager.Peers.AvailablePeers.Count, "#3");
        }
    }
}
