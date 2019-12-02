//
// StartingModeTests.cs
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
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Client.Modes
{

    [TestFixture]
    public class StartingModeTests
    {
        ConnectionPair conn;
        ConnectionManager ConnectionManager { get; set; }
        DiskManager DiskManager { get; set; }
        TorrentManager Manager { get; set; }
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
        }

        [TearDown]
        public void Teardown()
        {
            conn.Dispose();
            DiskManager.Dispose ();
        }

        [Test]
        public async Task Announce ()
        {
            TrackerManager.AddTracker ("http://1.1.1.1");

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();

            Assert.AreEqual (1, TrackerManager.Announces.Count, "#1");
            Assert.AreEqual (TrackerManager.CurrentTracker, TrackerManager.Announces[0].Item1, "#2");
            Assert.AreEqual (TorrentEvent.Started, TrackerManager.Announces[0].Item2, "#3");
        }

        [Test]
        public async Task Announce_NoTrackers ()
        {
            Assert.IsNull (Manager.TrackerManager.CurrentTracker, "#1");
            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();
            Assert.IsInstanceOf<DownloadMode> (Manager.Mode, "#2");
        }

        [Test]
        public void StartTwiceTest ()
        {
            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            Assert.ThrowsAsync<TorrentException> (() => Manager.StartAsync (), "#1");
        }

        [Test]
        public async Task StateChanges_AlreadyHashed ()
        {
            var modeChanged = new List<Mode> ();
            Manager.ModeChanged += (oldMode, newMode) => modeChanged.Add (newMode);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.HashChecked = true;
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();

            Assert.AreEqual (2, modeChanged.Count, "#1");
            Assert.IsInstanceOf<StartingMode> (modeChanged[0], "#2");
            Assert.IsInstanceOf<DownloadMode> (modeChanged[1], "#2");
        }

        [Test]
        public async Task StateChanges_NeedsHashing ()
        {
            var modeChanged = new List<Mode> ();
            Manager.ModeChanged += (oldMode, newMode) => modeChanged.Add (newMode);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.HashChecked = false;
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();

            Assert.AreEqual (3, modeChanged.Count, "#1");
            Assert.IsInstanceOf<StartingMode> (modeChanged[0], "#2");
            Assert.IsInstanceOf<HashingMode> (modeChanged[1], "#3");
            Assert.IsInstanceOf<DownloadMode> (modeChanged[2], "#4");
        }

        [Test]
        public async Task FastResume_NoneExist()
        {
            var bf = Manager.Bitfield.Clone ().SetAll (true);
            Manager.LoadFastResume (new FastResume (Manager.InfoHash, bf, Manager.UnhashedPieces.SetAll (false)));

            Assert.IsTrue (Manager.Bitfield.AllTrue, "#1");
            foreach (TorrentFile file in Manager.Torrent.Files)
                Assert.IsTrue (file.BitField.AllTrue, "#2." + file.Path);

            var startingMode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = startingMode;
            await startingMode.WaitForStartingToComplete ();

            Assert.IsTrue(Manager.Bitfield.AllFalse, "#3");
            foreach (var file in Manager.Torrent.Files)
                Assert.IsTrue(file.BitField.AllFalse, "#4." + file.Path);
        }

        [Test]
        public async Task FastResume_SomeExist()
        {
            PieceWriter.FilesThatExist.AddRange(new[]{
                Manager.Torrent.Files [0],
                Manager.Torrent.Files [2],
            });
            var bf = Manager.Bitfield.Clone ().SetAll (true);
            Manager.LoadFastResume(new FastResume(Manager.InfoHash, bf, Manager.UnhashedPieces.SetAll (false)));

            Assert.IsTrue (Manager.Bitfield.AllTrue, "#1");
            foreach (TorrentFile file in Manager.Torrent.Files)
                Assert.IsTrue (file.BitField.AllTrue, "#2." + file.Path);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();

            Assert.IsTrue(Manager.Bitfield.AllFalse, "#3");
            foreach (TorrentFile file in Manager.Torrent.Files)
                Assert.IsTrue(file.BitField.AllFalse, "#4." + file.Path);
        }
    }
}
