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

using MonoTorrent.Trackers;

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
        public void Setup ()
        {
            conn = new ConnectionPair ().DisposeAfterTimeout ();
            Settings = new EngineSettings ();
            PieceWriter = new TestWriter ();
            DiskManager = new DiskManager (Settings, Factories.Default, PieceWriter);
            ConnectionManager = new ConnectionManager ("LocalPeerId", Settings, Factories.Default, DiskManager);
            TrackerManager = new ManualTrackerManager ();

            long[] fileSizes = {
                Constants.BlockSize / 2,
                Constants.BlockSize * 32,
                Constants.BlockSize * 2,
                Constants.BlockSize * 13,
            };
            Manager = TestRig.CreateMultiFileManager (fileSizes, Constants.BlockSize * 2);
            Manager.SetTrackerManager (TrackerManager);
        }

        [TearDown]
        public void Teardown ()
        {
            conn.Dispose ();
            DiskManager.Dispose ();
        }

        [Test]
        public async Task Announce ()
        {
            await TrackerManager.AddTrackerAsync (Factories.Default.CreateTracker (new Uri ("http://1.1.1.1")));

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();

            // Technically this is not what we want. However the logic to avoid announcing to quickly
            // is now inside TrackerManager so a mocked TrackerManager will double announce.
            Assert.AreEqual (2, TrackerManager.Announces.Count, "#1");
            Assert.AreEqual (null, TrackerManager.Announces[0].Item1, "#2");
            Assert.AreEqual (null, TrackerManager.Announces[1].Item1, "#2");
            Assert.AreEqual (TorrentEvent.Started, TrackerManager.Announces[0].Item2, "#3");
            Assert.AreEqual (TorrentEvent.None, TrackerManager.Announces[1].Item2, "#3");
        }

        [Test]
        public async Task Announce_NoTrackers ()
        {
            Assert.AreEqual (0, Manager.TrackerManager.Tiers.Count, "#1");
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
            var modeChanged = new List<IMode> ();
            Manager.ModeChanged += (oldMode, newMode) => modeChanged.Add (newMode);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, new BitField (Manager.Torrent.PieceCount ()), new BitField (Manager.Torrent.PieceCount ())));
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();

            Assert.AreEqual (2, modeChanged.Count, "#1");
            Assert.IsInstanceOf<StartingMode> (modeChanged[0], "#2");
            Assert.IsInstanceOf<DownloadMode> (modeChanged[1], "#2");
        }

        [Test]
        public async Task StateChanges_NeedsHashing ()
        {
            var modeChanged = new List<IMode> ();
            Manager.ModeChanged += (oldMode, newMode) => modeChanged.Add (newMode);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Assert.IsFalse (Manager.HashChecked);
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();

            Assert.AreEqual (3, modeChanged.Count, "#1");
            Assert.IsInstanceOf<StartingMode> (modeChanged[0], "#2");
            Assert.IsInstanceOf<HashingMode> (modeChanged[1], "#3");
            Assert.IsInstanceOf<DownloadMode> (modeChanged[2], "#4");
        }

        [Test]
        public async Task FastResume_NoneExist ()
        {
            var bf = new BitField (Manager.Torrent.PieceCount ()).SetAll (true);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, bf, Manager.UnhashedPieces.SetAll (false)));

            Assert.IsTrue (Manager.Bitfield.AllTrue, "#1");
            foreach (var file in Manager.Files)
                Assert.IsTrue (file.BitField.AllTrue, "#2." + file.Path);

            var startingMode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = startingMode;
            await startingMode.WaitForStartingToComplete ();

            Assert.IsTrue (Manager.Bitfield.AllFalse, "#3");
            foreach (var file in Manager.Files)
                Assert.IsTrue (file.BitField.AllFalse, "#4." + file.Path);
        }

        [Test]
        public async Task FastResume_SomeExist ()
        {
            await PieceWriter.CreateAsync (new[]{
                Manager.Files [0],
                Manager.Files [2],
            });
            var bf = new BitField (Manager.Torrent.PieceCount ()).SetAll (true);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, bf, Manager.UnhashedPieces.SetAll (false)));

            Assert.IsTrue (Manager.Bitfield.AllTrue, "#1");
            foreach (var file in Manager.Files)
                Assert.IsTrue (file.BitField.AllTrue, "#2." + file.Path);

            var mode = new StartingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            await mode.WaitForStartingToComplete ();

            Assert.IsTrue (Manager.Bitfield.AllFalse, "#3");
            foreach (var file in Manager.Files)
                Assert.IsTrue (file.BitField.AllFalse, "#4." + file.Path);
        }
    }
}
