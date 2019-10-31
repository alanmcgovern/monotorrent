//
// HashingModeTests.cs
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
using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Client.Modes
{
    [TestFixture]
    public class HashingModeTests
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
            Peer = new PeerId (new Peer ("", new Uri ("ipv4://123.123.123.123"), EncryptionTypes.All), conn.Outgoing, Manager.Bitfield?.Clone ().SetAll (true)) {
                ProcessingQueue = true,
                IsChoking = false,
                AmInterested = true,
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
            Manager.Mode = new HashingMode (Manager, DiskManager, ConnectionManager, Settings);

            Assert.IsTrue (Peer.Connection.Connected, "#1");
            Manager.HandlePeerConnected (Peer);
            Assert.IsFalse (Peer.Connection.Connected, "#2");
            Assert.IsFalse (Manager.Peers.ConnectedPeers.Contains (Peer), "#3");
        }

        [Test]
        public void CancelHashing ()
        {
            var mode = new HashingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.Pause ();

            var hashingTask = mode.WaitForHashingToComplete ();
            var stoppedMode = new StoppedMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = stoppedMode;

            // Ensure the hashing mode ends and does not throw exceptions.
            Assert.ThrowsAsync<TaskCanceledException> (() => hashingTask, "#1");
            Assert.AreSame (stoppedMode, Manager.Mode, "#2");
        }

        [Test]
        public async Task PauseResumeHashingMode ()
        {
            var pieceHashed = new TaskCompletionSource<object> ();
            Manager.PieceHashed += (o, e) => pieceHashed.TrySetResult (null);

            var mode = new HashingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.Pause ();
            Assert.AreEqual (TorrentState.HashingPaused, mode.State, "#a");

            var hashingTask = mode.WaitForHashingToComplete ();
            await Task.Delay (50);
            Assert.IsFalse (pieceHashed.Task.IsCompleted, "#1");

            mode.Resume ();
            Assert.AreEqual (pieceHashed.Task, await Task.WhenAny (pieceHashed.Task, Task.Delay (1000)), "#2");
            Assert.AreEqual (TorrentState.Hashing, mode.State, "#b");
        }

        [Test]
        public void SaveLoadFastResume ()
        {
            Manager.Bitfield.SetAll (true).Set (0, false);
            Manager.UnhashedPieces.SetAll (false).Set (0, true);
            Manager.HashChecked = true;

            var origUnhashed = Manager.UnhashedPieces.Clone ();
            var origBitfield = Manager.Bitfield.Clone ();
            Manager.LoadFastResume (Manager.SaveFastResume ());

            Assert.AreEqual (origUnhashed, Manager.UnhashedPieces, "#3");
            Assert.AreEqual (origBitfield, Manager.Bitfield, "#4");
        }

        [Test]
        public async Task DoNotDownload_All ()
        {
            Manager.Bitfield.SetAll (true);

            foreach (var f in Manager.Torrent.Files) {
                PieceWriter.FilesThatExist.Add (f);
                f.Priority = Priority.DoNotDownload;
            }

            var hashingMode = new HashingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = hashingMode;
            await hashingMode.WaitForHashingToComplete ();

            Manager.PieceManager.AddPieceRequests (Peer);
            Assert.AreEqual (0, Peer.AmRequestingPiecesCount, "#1");

            // No piece should be marked as available, and no pieces should actually be hashchecked.
            Assert.IsTrue (Manager.Bitfield.AllFalse, "#2");
            Assert.AreEqual (Manager.UnhashedPieces.TrueCount, Manager.UnhashedPieces.Length, "#3");
        }

        [Test]
        public async Task DoNotDownload_ThenDownload ()
        {
            DiskManager.GetHashAsyncOverride = (manager, index) => {
                if (index >= 0 && index <= 4)
                    return Manager.Torrent.Pieces.ReadHash (index);

                return Enumerable.Repeat ((byte)255, 20).ToArray ();
            };
            Manager.Bitfield.SetAll (true);

            foreach (var f in Manager.Torrent.Files) {
                PieceWriter.FilesThatExist.Add (f);
                f.Priority = Priority.DoNotDownload;
            }

            var hashingMode = new HashingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = hashingMode;
            await hashingMode.WaitForHashingToComplete ();
            Assert.IsTrue (Manager.UnhashedPieces.AllTrue, "#1");

            // Nothing should be available to download.
            Manager.PieceManager.AddPieceRequests (Peer);
            Assert.AreEqual (0, Peer.AmRequestingPiecesCount, "#1b");

            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            foreach (var file in Manager.Torrent.Files) {
                file.Priority = Priority.Normal;
                await Manager.Mode.TryHashPendingFilesAsync ();
                for (int i = file.StartPieceIndex; i <= file.EndPieceIndex; i ++)
                    Assert.IsFalse (Manager.UnhashedPieces [i], "#2." + i);
            }

            // No piece should be marked as available, and no pieces should actually be hashchecked.
            Assert.IsTrue (Manager.UnhashedPieces.AllFalse, "#3");

            // These pieces should now be available for download
            Manager.PieceManager.AddPieceRequests (Peer);
            Assert.AreNotEqual (0, Peer.AmRequestingPiecesCount, "#4");

            Assert.AreEqual (5, Manager.finishedPieces.Count, "#5");
        }

        [Test]
        public void StopWhileHashingPendingFiles ()
        {
            var pieceHashCount = 0;
            DiskManager.GetHashAsyncOverride = (manager, index) => {
                pieceHashCount ++;
                if (pieceHashCount == 3)
                    Manager.StopAsync ().Wait ();

                return Enumerable.Repeat ((byte)0, 20).ToArray ();
            };

            Manager.Bitfield.SetAll (true);

            foreach (var f in Manager.Torrent.Files)
                f.Priority = Priority.DoNotDownload;

            Manager.Mode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            foreach (var file in Manager.Torrent.Files)
                file.Priority = Priority.Normal;

            Assert.ThrowsAsync<TaskCanceledException> (() => Manager.Mode.TryHashPendingFilesAsync (), "#1");
            Assert.AreEqual (3, pieceHashCount, "#2");
        }

        [Test]
        public async Task DoNotDownload_OneFile ()
        {
            Manager.Bitfield.SetAll (true);

            foreach (var f in Manager.Torrent.Files.Skip (1)) {
                PieceWriter.FilesThatExist.Add (f);
                f.Priority = Priority.DoNotDownload;
            }

            var hashingMode = new HashingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = hashingMode;
            await hashingMode.WaitForHashingToComplete ();

            // No piece should be marked as available
            Assert.IsTrue (Manager.Bitfield.AllFalse, "#1");

            // Only one piece should actually have been hash checked.
            Assert.AreEqual (1, Manager.UnhashedPieces.Length - Manager.UnhashedPieces.TrueCount, "#2");
            Assert.IsFalse (Manager.UnhashedPieces[0], "#3");

            Manager.PieceManager.AddPieceRequests (Peer);
            Assert.AreNotEqual (0, Peer.AmRequestingPiecesCount, "#4");
        }

        [Test]
        public async Task ReadZeroFromDisk ()
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
            Manager.LoadFastResume(new FastResume(Manager.InfoHash, bf, Manager.UnhashedPieces.SetAll (false)));

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

        [Test]
        public async Task StopWhileHashing ()
        {
            var mode = new HashingMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = mode;
            mode.Pause ();

            var hashingTask = mode.WaitForHashingToComplete ();
            await Manager.StopAsync ();

            Assert.ThrowsAsync<TaskCanceledException> (() => hashingTask, "#1");
            Assert.AreEqual (Manager.State, TorrentState.Stopped, "#2");
        }
    }
}
