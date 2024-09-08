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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Trackers;

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
        PeerId Peer { get; set; }
        TestWriter PieceWriter { get; set; }
        EngineSettings Settings { get; set; }
        ManualTrackerManager TrackerManager { get; set; }
        TempDir.Releaser TempDirectory { get; set; }

        [SetUp]
        public void Setup ()
        {
            TempDirectory = TempDir.Create ();
            conn = new ConnectionPair ().DisposeAfterTimeout ();
            PieceWriter = new TestWriter ();
            TrackerManager = new ManualTrackerManager ();

            long[] fileSizes = {
                Constants.BlockSize / 2,
                Constants.BlockSize * 32,
                Constants.BlockSize * 2,
                Constants.BlockSize * 13,
                0,
                0,
            };
            Manager = TestRig.CreateMultiFileManager (fileSizes, Constants.BlockSize * 2, writer: PieceWriter, baseDirectory: TempDirectory.Path);
            Manager.SetTrackerManager (TrackerManager);

            Settings = Manager.Engine.Settings;
            DiskManager = Manager.Engine.DiskManager;
            ConnectionManager = Manager.Engine.ConnectionManager;

            Peer = new PeerId (new Peer (new PeerInfo (new Uri ("ipv4://123.123.123.123:12345"))), conn.Outgoing, new BitField (Manager.Bitfield.Length).SetAll (true), Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, Software.Synthetic) {
                IsChoking = false,
                AmInterested = true,
            };
        }

        [TearDown]
        public void Teardown ()
        {
            TempDirectory.Dispose ();
            conn.Dispose ();
            DiskManager.Dispose ();
        }

        [Test]
        public void AddConnection ()
        {
            Manager.Mode = new HashingMode (Manager, DiskManager);

            Assert.IsFalse (Peer.Connection.Disposed, "#1");
            Assert.Throws<NotSupportedException> (() => Manager.Mode.HandlePeerConnected (Peer));
        }

        [Test]
        public void CancelHashing ()
        {
            var mode = new HashingMode (Manager, DiskManager);
            Manager.Mode = mode;
            mode.Pause ();

            var hashingTask = mode.WaitForHashingToComplete ();
            var stoppedMode = new StoppedMode ();
            Manager.Mode = stoppedMode;

            // Ensure the hashing mode ends and does not throw exceptions.
            Assert.ThrowsAsync<TaskCanceledException> (() => hashingTask, "#1");
            Assert.AreSame (stoppedMode, Manager.Mode, "#2");
        }

        [Test]
        public async Task HashCheckAsync_Autostart ()
        {
            await Manager.HashCheckAsync (true);
            await Manager.WaitForState (TorrentState.Downloading);
        }

        [Test]
        public async Task HashCheckAsync_DoNotAutostart ()
        {
            await Manager.HashCheckAsync (false);
            Assert.AreEqual (TorrentState.Stopped, Manager.State, "#1");
        }

        [Test]
        public async Task HashCheckAsync_DoNotTruncate ()
        {
            var file = Manager.Files.First (t => t.Length != 0);
            PieceWriter.FilesWithLength[file.FullPath] = file.Length + 1;
            await Manager.HashCheckAsync (false);
            Assert.AreEqual (TorrentState.Stopped, Manager.State, "#1");
            Assert.AreEqual (file.Length + 1, PieceWriter.FilesWithLength[file.FullPath]);
        }

        [Test]
        public async Task HashCheckAsync_FilesAreCorrectLength ()
        {
            var mode = new HashingMode (Manager, DiskManager);
            Manager.Mode = mode;

            // All files are 1 byte too long
            foreach (var file in Manager.Files) {
                PieceWriter.FilesWithLength[file.FullPath] = file.Length + 1;
            }

            await mode.WaitForHashingToComplete ();
            Assert.IsFalse (Manager.AllFilesCorrectLength);

            // All files are correctly sized and exist.
            foreach (var file in Manager.Files) {
                PieceWriter.FilesWithLength[file.FullPath] = file.Length;
            }

            await mode.WaitForHashingToComplete ();
            Assert.IsTrue (Manager.AllFilesCorrectLength);

            // One zero length file is missing
            var zeroLengthFile = Manager.Files.First (t => t.Length == 0);
            PieceWriter.FilesWithLength.Remove (zeroLengthFile.FullPath);

            await mode.WaitForHashingToComplete ();
            Assert.IsFalse (Manager.AllFilesCorrectLength);

            // One file is too large
            PieceWriter.FilesWithLength[zeroLengthFile.FullPath] = 1;
            await mode.WaitForHashingToComplete ();
            Assert.IsFalse (Manager.AllFilesCorrectLength);

            // Back to normal!
            PieceWriter.FilesWithLength[zeroLengthFile.FullPath] = 0;
            await mode.WaitForHashingToComplete ();
            Assert.IsTrue (Manager.AllFilesCorrectLength);
        }

        [Test]
        public async Task PauseResumeHashingMode ()
        {
            var pieceTryHash = new TaskCompletionSource<object> ();
            var pieceHashed = new TaskCompletionSource<byte[]> ();
            var secondPieceHashed = new TaskCompletionSource<byte[]> ();

            await PieceWriter.CreateAsync (Manager.Files);

            Manager.Engine.DiskManager.GetHashAsyncOverride = async (torrentdata, pieceIndex, dest) => {
                pieceTryHash.TrySetResult (null);

                if (!pieceHashed.Task.IsCompleted) {
                    var data = await pieceHashed.Task.WithTimeout ();
                    data.CopyTo (dest.V1Hash);
                    return true;
                }
                if (!secondPieceHashed.Task.IsCompleted) {
                    var data = await secondPieceHashed.Task.WithTimeout ();
                    data.CopyTo (dest.V1Hash);
                    return true;
                }
                new byte[20].CopyTo (dest.V1Hash);
                return true;
            };

            var hashCheckTask = Manager.HashCheckAsync (false);
            await pieceTryHash.Task.WithTimeout ();

            var pausedEvent = Manager.WaitForState (TorrentState.HashingPaused);
            await Manager.PauseAsync ();
            await pausedEvent.WithTimeout ("#pause");
            Assert.AreEqual (TorrentState.HashingPaused, Manager.State, "#a");
            pieceHashed.TrySetResult (new byte[20]);

            var resumeEvent = Manager.WaitForState (TorrentState.Hashing);
            await Manager.StartAsync ();
            await resumeEvent.WithTimeout ("#resume");
            Assert.AreEqual (TorrentState.Hashing, Manager.State, "#b");
            secondPieceHashed.TrySetResult (new byte[20]);

            await hashCheckTask.WithTimeout ();
        }

        [Test]
        public async Task ProgressWhileHashing ()
        {
            var tcs = new TaskCompletionSource<bool> ();
            var args = new List<PieceHashedEventArgs> ();
            Manager.PieceHashed += (o, e) => {
                lock (args) {
                    args.Add (e);
                    if (args.Count == Manager.Torrent.PieceCount)
                        tcs.SetResult (true);
                }
            };

            await Manager.HashCheckAsync (false);
            await tcs.Task.WithTimeout ("hashing");

            args.Sort ((l, r) => l.PieceIndex.CompareTo (r.PieceIndex));
            for (int i = 1; i < args.Count; i++)
                Assert.Greater (args[i].Progress, args[i - 1].Progress, "#1." + i);
            Assert.Greater (args.First ().Progress, 0, "#2");
            Assert.AreEqual (1, args.Last ().Progress, "#3");
        }

        [Test]
        public async Task SaveLoadFastResume ()
        {
            await Manager.HashCheckAsync (false);
            var bf = new BitField (Manager.Bitfield).SetAll (true).Set (0, false);
            var unhashed = new BitField (bf).SetAll (false).Set (0, true);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, bf, unhashed));

            var origUnhashed = new ReadOnlyBitField (Manager.UnhashedPieces);
            var origBitfield = new ReadOnlyBitField (Manager.Bitfield);
            await Manager.LoadFastResumeAsync (await Manager.SaveFastResumeAsync ());

            Assert.IsTrue (origUnhashed.SequenceEqual (Manager.UnhashedPieces), "#3");
            Assert.IsTrue (origBitfield.SequenceEqual (Manager.Bitfield), "#4");
        }

        [Test]
        public async Task DoNotDownload_All ()
        {
            // No files exist!
            Assert.AreEqual (0, PieceWriter.FilesWithLength.Count);

            var bf = new BitField (Manager.Bitfield).SetAll (true);
            var unhashed = new BitField (bf).SetAll (false);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, bf, unhashed));

            foreach (var f in Manager.Files) {
                if (f.Length > 0)
                    await PieceWriter.CreateAsync (f, MonoTorrent.PieceWriter.FileCreationOptions.PreferPreallocation);
                await Manager.SetFilePriorityAsync (f, Priority.DoNotDownload);
                ((TorrentFileInfo) f).BitField.SetAll (true);
            }

            var hashingMode = new HashingMode (Manager, DiskManager);
            Manager.Mode = hashingMode;
            await hashingMode.WaitForHashingToComplete ().WithTimeout ();

            Manager.PieceManager.AddPieceRequests (Peer);
            Assert.AreEqual (0, Peer.AmRequestingPiecesCount, "#1");

            // No piece should be marked as available, and no pieces should actually be hashchecked.
            Assert.IsTrue (Manager.Bitfield.AllFalse, "#2");
            Assert.AreEqual (Manager.UnhashedPieces.TrueCount, Manager.UnhashedPieces.Length, "#3");

            // Empty files will always have their bitfield set to true if they exist on disk. None should exist though
            foreach (var f in Manager.Files)
                Assert.IsTrue (f.BitField.AllFalse, "#4." + f.Path);
        }

        [Test]
        public async Task DoNotDownload_ThenDownload ()
        {
            DiskManager.GetHashAsyncOverride = (manager, index, dest) => {
                if (index >= 0 && index <= 4) {
                    Manager.Torrent.CreatePieceHashes ().GetHash (index).V1Hash.Span.CopyTo (dest.V1Hash.Span);
                } else {
                    Enumerable.Repeat ((byte) 255, 20).ToArray ().CopyTo (dest.V1Hash.Span);
                }
                return Task.FromResult (true);
            };

            var bf = new BitField (Manager.Bitfield).SetAll (true);
            var unhashed = new BitField (bf).SetAll (false);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, bf, unhashed));

            foreach (var f in Manager.Files) {
                await PieceWriter.CreateAsync (f, MonoTorrent.PieceWriter.FileCreationOptions.PreferPreallocation);
                await Manager.SetFilePriorityAsync (f, Priority.DoNotDownload);
            }

            var hashingMode = new HashingMode (Manager, DiskManager);
            Manager.Mode = hashingMode;
            await hashingMode.WaitForHashingToComplete ();
            Assert.IsTrue (Manager.UnhashedPieces.AllTrue, "#1");

            // Nothing should be available to download.
            Manager.PieceManager.AddPieceRequests (Peer);
            Assert.AreEqual (0, Peer.AmRequestingPiecesCount, "#1b");

            var downloadMode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = downloadMode;
            foreach (var file in Manager.Files) {
                await Manager.SetFilePriorityAsync (file, Priority.Normal);
                await downloadMode.TryHashPendingFilesAsync ();
                for (int i = file.StartPieceIndex; i <= file.EndPieceIndex; i++)
                    Assert.IsFalse (Manager.UnhashedPieces[i], "#2." + i);
            }

            // No piece should be marked as available, and no pieces should actually be hashchecked.
            Assert.IsTrue (Manager.UnhashedPieces.AllFalse, "#3");

            // These pieces should now be available for download
            Manager.PieceManager.AddPieceRequests (Peer);
            Assert.AreNotEqual (0, Peer.AmRequestingPiecesCount, "#4");

            Assert.AreEqual (5, Manager.finishedPieces.Count, "#5");
        }

        [Test]
        public async Task StopWhileHashingPendingFiles ()
        {
            var pieceHashCount = 0;
            DiskManager.GetHashAsyncOverride = (manager, index, dest) => {
                pieceHashCount++;
                if (pieceHashCount == 3)
                    Manager.StopAsync ().Wait ();

                Enumerable.Repeat ((byte) 0, 20).ToArray ().CopyTo (dest.V1Hash);
                return Task.FromResult (true);
            };

            var bf = new BitField (Manager.Bitfield).SetAll (false);
            var unhashed = new BitField (bf).SetAll (true);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, bf, unhashed));

            foreach (var f in Manager.Files)
                await Manager.SetFilePriorityAsync (f, Priority.DoNotDownload);

            var downloadMode = new DownloadMode (Manager, DiskManager, ConnectionManager, Settings);
            Manager.Mode = downloadMode;
            foreach (var file in Manager.Files)
                await Manager.SetFilePriorityAsync (file, Priority.Normal);

            Assert.ThrowsAsync<OperationCanceledException> (async () => await downloadMode.TryHashPendingFilesAsync (), "#1");
            Assert.AreEqual (3, pieceHashCount, "#2");
        }

        [Test]
        public async Task StopWhileHashingPaused ()
        {
            await PieceWriter.CreateAsync (Manager.Files);

            int getHashCount = 0;
            DiskManager.GetHashAsyncOverride = (manager, index, dest) => {
                getHashCount++;
                if (getHashCount == 2)
                    Manager.PauseAsync ().Wait ();
                Enumerable.Repeat ((byte) 0, 20).ToArray ().CopyTo (dest.V1Hash);
                return Task.FromResult (true);
            };

            var pausedState = Manager.WaitForState (TorrentState.HashingPaused);

            // Start hashing and wait until we pause
            var hashing = Manager.HashCheckAsync (false);
            await pausedState;
            Assert.AreEqual (2, getHashCount, "#1");

            // Now make sure there are no more reads
            await Manager.StopAsync ().WithTimeout ("#2");
            await hashing.WithTimeout ("#3");
            Assert.AreEqual (2, getHashCount, "#4");
        }

        [Test]
        public async Task DoNotDownload_OneFile ()
        {
            var bf = new BitField (Manager.Bitfield).SetAll (true);
            var unhashed = new BitField (bf).SetAll (false);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, bf, unhashed));

            foreach (var f in Manager.Files.Skip (1)) {
                await PieceWriter.CreateAsync (f, MonoTorrent.PieceWriter.FileCreationOptions.PreferPreallocation);
                await Manager.SetFilePriorityAsync (f, Priority.DoNotDownload);
            }

            var hashingMode = new HashingMode (Manager, DiskManager);
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
            var emptyFiles = Manager.Files.Where (t => t.Length == 0).ToArray ();
            var nonEmptyFiles = Manager.Files.Where (t => t.Length != 0).ToArray ();
            await PieceWriter.CreateAsync (new[] { nonEmptyFiles[0], nonEmptyFiles[2] });
            await PieceWriter.CreateAsync (emptyFiles);

            PieceWriter.DoNotReadFrom.AddRange (new[]{
                nonEmptyFiles [0],
                nonEmptyFiles [3],
            });

            var bf = new BitField (Manager.Torrent.PieceCount ()).SetAll (true);
            await Manager.LoadFastResumeAsync (new FastResume (Manager.InfoHashes, bf, Manager.UnhashedPieces.SetAll (false)));

            Assert.IsTrue (Manager.Bitfield.AllTrue, "#1");
            foreach (var file in Manager.Files)
                Assert.IsTrue (file.BitField.AllTrue, "#2." + file.Path);

            // Remove zero length files so they no longer exist
            foreach (var v in emptyFiles)
                PieceWriter.FilesWithLength.Remove (v.FullPath);
            var mode = new HashingMode (Manager, DiskManager);
            Manager.Mode = mode;
            await mode.WaitForHashingToComplete ();

            Assert.IsTrue (Manager.Bitfield.AllFalse, "#3");
            foreach (var file in Manager.Files)
                Assert.IsTrue (file.BitField.AllFalse, "#4." + file.Path);
        }

        [Test]
        public async Task StopWhileHashing ()
        {
            var mode = new HashingMode (Manager, DiskManager);
            Manager.Mode = mode;
            mode.Pause ();

            var hashingTask = mode.WaitForHashingToComplete ();
            await Manager.StopAsync ();

            Assert.ThrowsAsync<TaskCanceledException> (() => hashingTask, "#1");
            Assert.AreEqual (Manager.State, TorrentState.Stopped, "#2");
        }
    }
}
