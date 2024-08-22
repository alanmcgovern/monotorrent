//
// DiskManagerTests.cs
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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.PieceWriter;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class DiskManagerTests
    {
        class TestTorrentData : TestTorrentManagerInfo
        {
            public byte[][] Data { get; set; }
            public byte[][] Hashes { get; set; }
        }

        class PieceWriter : IPieceWriter
        {
            public Dictionary<ITorrentManagerFile, byte[]> Data = new Dictionary<ITorrentManagerFile, byte[]> ();
            public readonly List<Tuple<ITorrentManagerFile, long, int>> ReadData = new List<Tuple<ITorrentManagerFile, long, int>> ();
            public readonly List<Tuple<ITorrentManagerFile, long, byte[]>> WrittenData = new List<Tuple<ITorrentManagerFile, long, byte[]>> ();

            public List<ITorrentManagerFile> ClosedFiles = new List<ITorrentManagerFile> ();
            public List<ITorrentManagerFile> ExistsFiles = new List<ITorrentManagerFile> ();

            public int OpenFiles => 0;
            public int MaximumOpenFiles { get; }

            public ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
            {
                ReadData.Add (Tuple.Create (file, offset, buffer.Length));

                if (Data == null) {
                    var fileData = WrittenData
                        .Where (t => t.Item1 == file)
                        .OrderBy (t => t.Item2)
                        .SelectMany (t => t.Item3)
                        .ToArray ();
                    fileData.AsSpan ((int)offset, buffer.Length).CopyTo (buffer.Span);
                } else {
                    var data = Data[file];
                    data.AsSpan ((int) offset, buffer.Length).CopyTo (buffer.Span);
                }
                return ReusableTask.FromResult (buffer.Length);
            }

            public ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
            {
                var result = new byte[buffer.Length];
                buffer.CopyTo (result.AsMemory ());
                WrittenData.Add (Tuple.Create (file, offset, result));
                return ReusableTask.CompletedTask;
            }

            public ReusableTask CloseAsync (ITorrentManagerFile file)
            {
                return ReusableTask.CompletedTask;
            }

            public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
            {
                return ReusableTask.FromResult (true);
            }

            public ReusableTask FlushAsync (ITorrentManagerFile file)
            {
                return ReusableTask.CompletedTask;
            }

            public ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite)
            {
                return ReusableTask.CompletedTask;
            }

            public void Dispose ()
            {
            }

            public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
            {
                return ReusableTask.CompletedTask;
            }

            public ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions options)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length)
            {
                throw new NotImplementedException ();
            }
        }

        TestTorrentData fileData;
        DiskManager diskManager;
        PieceWriter writer;

        [OneTimeSetUp]
        public void OnetimeSetup ()
        {
            var random = new Random ();
            var filePieces = new long[] {
                Constants.BlockSize / 2,
                Constants.BlockSize,
                Constants.BlockSize + Constants.BlockSize / 2,
                Constants.BlockSize * 10 + Constants.BlockSize / 2,
            };

            int pieceLength = Constants.BlockSize * 3;

            var files = TorrentFileInfo.Create (pieceLength, filePieces);
            long total = files.Sum (f => f.Length);
            var fileBytes = files
                .Select (f => { var b = new byte[f.Length]; random.NextBytes (b); return b; })
                .ToArray ();

            // Turn all the files into one byte array. Group the byte array into bittorrent pieces. Hash that piece.
            var hashes = fileBytes
                .SelectMany (t => t)
                .Partition (pieceLength)
                .Select (t => SHA1.Create ().ComputeHash (t))
                .ToArray ();

            fileData = TestTorrentData.Create<TestTorrentData> (
                    files: files.ToArray (),
                    size: files.Sum (f => f.Length),
                    pieceLength: pieceLength
            );
            fileData.Data = fileBytes;
            fileData.Hashes = hashes;
        }

        [SetUp]
        public void Setup ()
        {
            writer = new PieceWriter ();
            for (int i = 0; i < fileData.Files.Count; i++)
                writer.Data.Add (fileData.Files[i], fileData.Data[i]);

            diskManager = new DiskManager (new EngineSettingsBuilder { DiskCacheBytes = 0 }.ToSettings (), Factories.Default, writer);
        }

        [Test]
        public async Task ExceedReadRate ()
        {
            // Ensure the read rate is smaller than a block
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskReadRate = 1 }.ToSettings ());
            await diskManager.Tick (1000).WithTimeout ();

            // Queue up 7 reads, 1 should process.
            var buffer = new byte[Constants.BlockSize];
            var tasks = new List<Task> ();
            for (int i = 0; i < 7 + 1; i++)
                tasks.Add (diskManager.ReadAsync (fileData, new BlockInfo (0, 0, buffer.Length), buffer).AsTask ());

            // Wait for the first task to complete.
            var doneTask = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (doneTask);
            await doneTask;

            Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingReadBytes, "#1");

            // This should process one too.
            await diskManager.Tick (1000).WithTimeout ();
            doneTask = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (doneTask);
            await doneTask;

            Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingReadBytes, "#2");

            // Give a max read rate which allows at least 2 blocks to read.
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskReadRate = (int)(Constants.BlockSize * 1.8) }.ToSettings ());
            for (int i = 0; i < 2; i++) {
                await diskManager.Tick (1000).WithTimeout ();

                for (int t = 0; t < 2; t++) {
                    var completed = await Task.WhenAny (tasks).WithTimeout ();
                    await completed;
                    tasks.Remove (completed);
                }
                Assert.IsFalse (tasks.Any (t => t.IsCompleted));

                Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingReadBytes, "#3." + i);
            }

            // If we add more reads after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i++) {
                tasks.Add (diskManager.ReadAsync (fileData, new BlockInfo (0, 0, buffer.Length), buffer).AsTask ());
            }
            Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingReadBytes, "#4");
            while (tasks.Count > 0) {
                await diskManager.Tick (1000).WithTimeout ();

                for (int t = 0; t < 2; t++) {
                    var completed = await Task.WhenAny (tasks).WithTimeout ();
                    await completed;
                    tasks.Remove (completed);
                }
                Assert.IsFalse (tasks.Any (t => t.IsCompleted));

                Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingReadBytes, "#5");
            }
        }

        [Test]
        public async Task ExceedWriteRate ()
        {
            // Ensure the read rate is smaller than a block
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskWriteRate = 1, DiskCacheBytes = 0 }.ToSettings ());
            await diskManager.Tick (1000);

            // Queue up 6 reads, 1 should process.
            var buffer = new byte[Constants.BlockSize];
            var tasks = new List<Task> ();
            for (int i = 0; i < 8; i++)
                tasks.Add (diskManager.WriteAsync (fileData, new BlockInfo (i / 3, Constants.BlockSize * (i % 3), Constants.BlockSize), buffer).AsTask ());

            // Wait for the first task to complete.
            var doneTask = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (doneTask);
            await doneTask;

            Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingWriteBytes, "#1");

            // We should still process one.
            await diskManager.Tick (1000);
            doneTask = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (doneTask);
            await doneTask;

            Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingWriteBytes, "#2");

            // Give a proper max read rate.
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskWriteRate = Constants.BlockSize * 2, DiskCacheBytes = 0 }.ToSettings ());
            for (int i = 0; i < 2; i++) {
                await diskManager.Tick (1000);

                for (int t = 0; t < 2; t++) {
                    var completed = await Task.WhenAny (tasks).WithTimeout ();
                    await completed;
                    tasks.Remove (completed);
                }
                Assert.IsFalse (tasks.Any (t => t.IsCompleted));

                Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingWriteBytes, "#3." + i);
            }

            // If we add more writes after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i++) {
                tasks.Add (diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * i, Constants.BlockSize), buffer).AsTask ());
            }
            Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingWriteBytes, "#4");

            while (diskManager.PendingWriteBytes > 0) {
                await diskManager.Tick (1000);
                for (int t = 0; t < 2; t++) {
                    var completed = await Task.WhenAny (tasks).WithTimeout ();
                    await completed;
                    tasks.Remove (completed);
                }
                Assert.IsFalse (tasks.Any (t => t.IsCompleted));

                Assert.AreEqual (buffer.Length * tasks.Count, diskManager.PendingWriteBytes, "#5." + diskManager.PendingWriteBytes);
            }
        }

        [Test]
        public async Task MoveFile_ConvertsToFullPath ()
        {
            using var writer = new TestWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            var file = TorrentFileInfo.Create (Constants.BlockSize, 123456).Single ();
            Assert.IsFalse (writer.FilesWithLength.ContainsKey (file.FullPath));

            var newFullPath = Path.GetFullPath ("NewFullPath");
            await manager.MoveFileAsync (file, (newFullPath, newFullPath, newFullPath));
            Assert.AreEqual (newFullPath, file.FullPath);
            Assert.AreEqual (newFullPath, file.DownloadCompleteFullPath);
            Assert.AreEqual (newFullPath, file.DownloadIncompleteFullPath);
            Assert.IsFalse (writer.FilesWithLength.ContainsKey (file.FullPath));
        }

        [Test]
        public async Task MoveFile_SamePath ()
        {
            var file = TorrentFileInfo.Create (Constants.BlockSize, ("file.txt", 123456, Path.Combine ("foo", "bar", "orig.txt"))).Single ();

            using var writer = new TestWriter ();
            writer.FilesWithLength[file.FullPath] = 123456;

            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            await manager.MoveFileAsync (file, (file.FullPath, file.FullPath, file.FullPath + TorrentFileInfo.IncompleteFileSuffix));
            Assert.IsTrue (writer.FilesWithLength.ContainsKey (file.FullPath));
        }

        [Test]
        public async Task MoveFile_TargetDirectoryDoesNotExist ()
        {
            using var tmp = TempDir.Create ();
            var file = TorrentFileInfo.Create (Constants.BlockSize, ("file.txt", 123456, Path.Combine (tmp.Path, "orig.txt"))).Single ();

            using var writer = new TestWriter ();
            writer.FilesWithLength[file.FullPath] = 0;

            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            var fullPath = Path.Combine (tmp.Path, "New", "Path", "file.txt");
            await manager.MoveFileAsync (file, (fullPath, fullPath, fullPath + TorrentFileInfo.IncompleteFileSuffix));
            Assert.AreEqual (fullPath, file.FullPath);
        }

        [Test]
        public async Task MoveFiles_DoNotOverwrite ()
        {
            using var writer = new TestWriter ();
            var file = TorrentFileInfo.Create (Constants.BlockSize, ("file.txt", 123456, Path.GetFullPath (Path.Combine ("foo", "bar", "sub_dir", "orig.txt")))).Single ();
            writer.FilesWithLength[file.FullPath] = 0;

            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            var newRoot = Path.GetFullPath ("baz");
            await manager.MoveFilesAsync (new[] { file }, newRoot, false);
            Assert.AreEqual (Path.Combine (newRoot, file.Path), file.FullPath);
            Assert.IsTrue (writer.FilesWithLength.ContainsKey (file.FullPath));
            Assert.AreEqual (1, writer.FilesWithLength.Count);
        }

        [Test]
        public async Task MoveFiles_Overwrite ()
        {
            using var writer = new TestWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            var file = TorrentFileInfo.Create (Constants.BlockSize, ("file.txt", 123456, Path.GetFullPath (Path.Combine ("blarp", "sub_dir", "orig.txt")))).Single ();
            await writer.CreateAsync (file, FileCreationOptions.PreferSparse);

            var newRoot = Path.GetFullPath ("foo");
            await manager.MoveFilesAsync (new[] { file }, newRoot, true);
            Assert.AreEqual (Path.Combine (newRoot, file.Path), file.FullPath);
            Assert.IsTrue (writer.FilesWithLength.ContainsKey (file.FullPath));
            Assert.AreEqual (1, writer.FilesWithLength.Count);
        }

        [Test]
        public async Task MoveFiles_Overwrite_SameDir ()
        {
            using var writer = new TestWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            var root = Path.GetFullPath ("foo");
            var file = TorrentFileInfo.Create (Constants.BlockSize, (Path.Combine ("sub_dir", "orig.txt"), 123456, Path.Combine (root, "sub_dir", "orig.txt"))).Single ();
            await writer.CreateAsync (file, FileCreationOptions.PreferSparse);

            await manager.MoveFilesAsync (new[] { file }, root, true);
            Assert.AreEqual (Path.Combine (root, file.Path), file.FullPath);
            Assert.IsTrue (writer.FilesWithLength.ContainsKey (file.FullPath));
            Assert.AreEqual (1, writer.FilesWithLength.Count);
        }


        class AsyncReader : TestWriter
        {
            public override async ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
            {
                DiskManager.IOLoop.CheckThread ();
                await new ThreadSwitcher ();
                return buffer.Length;
            }
        }

        [Test]
        public void ReadIsThreadSafe ()
        {
            using var tmp = TempDir.Create ();
            var fileData = new TestTorrentData {
                TorrentInfo = new TestTorrentInfo {
                    InfoHashes = this.fileData.InfoHashes,
                    Name = "name",
                    Size = 2 * 100,
                    PieceLength = Constants.BlockSize * 2,
                    Files = TorrentFileInfo.Create (Constants.BlockSize * 2, Enumerable.Repeat (2L, 100).ToArray ()).ToArray ()
                }
            };

            using var writer = new AsyncReader ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            Assert.DoesNotThrowAsync (() => manager.ReadAsync (fileData, new BlockInfo (0, 0, 200), new byte[200]).AsTask ());
        }

        class AsyncWriter : TestWriter
        {
            public override async ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
            {
                DiskManager.IOLoop.CheckThread ();
                await new ThreadSwitcher ();
            }
        }

        [Test]
        public void WriteIsThreadSafe ()
        {
            using var tmp = TempDir.Create ();
            var fileData = new TestTorrentData {
                TorrentInfo = new TestTorrentInfo {
                    InfoHashes = this.fileData.InfoHashes,
                    Name = "name",
                    Size = 2 * 100,
                    PieceLength = Constants.BlockSize * 2,
                    Files = TorrentFileInfo.Create (Constants.BlockSize * 2, Enumerable.Repeat (2L, 100).ToArray ()).ToArray ()
                }
            };

            using var writer = new AsyncWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            Assert.DoesNotThrowAsync(() => manager.WriteAsync (fileData, new BlockInfo (0, 0, 200), new byte[200]).AsTask ());
        }

        [Test]
        public void ReadPastTheEnd ()
        {
            var buffer = new byte[Constants.BlockSize];
            Assert.ThrowsAsync<ArgumentOutOfRangeException> (() => diskManager.ReadAsync (fileData, new BlockInfo (1000, 0, Constants.BlockSize), buffer).AsTask (), "#1");
        }

        [Test]
        public async Task ReadPieceOne ()
        {
            var buffer = new byte[Constants.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, new BlockInfo (0, 0, Constants.BlockSize), buffer), "#1");

            var data1 = fileData.Data[0];
            var data2 = fileData.Data[1];

            Assert.IsTrue (buffer.AsSpan (0, data1.Length).SequenceEqual (data1.AsSpan (0, data1.Length)), "#2");
            Assert.IsTrue (buffer.AsSpan (data1.Length, Constants.BlockSize - data1.Length).SequenceEqual (data2.AsSpan (0, Constants.BlockSize - data1.Length)), "#3");
        }

        [Test]
        public async Task ReadPieceTwo ()
        {
            var buffer = new byte[Constants.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, new BlockInfo (0, Constants.BlockSize, Constants.BlockSize), buffer), "#1");

            var data0 = fileData.Data[0];
            var data1 = fileData.Data[1];
            var data2 = fileData.Data[2];

            Assert.IsTrue (buffer.AsSpan (0, data1.Length - data0.Length).SequenceEqual (data1.AsSpan (data0.Length, data1.Length - data0.Length)), "#2");
            Assert.IsTrue (buffer.AsSpan (data1.Length - data0.Length, Constants.BlockSize - (data1.Length - data0.Length)).SequenceEqual (data2.AsSpan (0, Constants.BlockSize - (data1.Length - data0.Length))), "#3");
        }

        [Test]
        public async Task ReadRate ()
        {
            var buffer = new byte[Constants.BlockSize];
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskReadRate = 1, DiskCacheBytes = 0 }.ToSettings ());
            await diskManager.Tick (1000);

            var tasks = new List<Task> ();
            for (int i = 0; i < SpeedMonitor.DefaultAveragePeriod + 1; i++)
                tasks.Add (diskManager.ReadAsync (fileData, new BlockInfo (0, 0, Constants.BlockSize), buffer).AsTask ());

            while (diskManager.PendingReadBytes > 0) {
                await diskManager.Tick (1000);
                var done = await Task.WhenAny (tasks).WithTimeout ();
                await done.WithTimeout ();
                tasks.Remove (done);
            }

            await Task.WhenAll (tasks).WithTimeout ();

            // We should be reading at about 1 block per second.
            Assert.AreEqual (Constants.BlockSize, diskManager.ReadRate, "#1");
            Assert.AreEqual ((SpeedMonitor.DefaultAveragePeriod + 1) * Constants.BlockSize, diskManager.TotalBytesRead, "#2");
        }

        [Test]
        public async Task WriteAllData ()
        {
            var buffer = new byte[Constants.BlockSize];
            var allData = fileData.Data.SelectMany (t => t).Partition (Constants.BlockSize).ToArray ();
            int blocksPerPiece = fileData.TorrentInfo.PieceLength / Constants.BlockSize;
            for (int i = 0; i < allData.Length; i++) {
                var pieceIndex = i / blocksPerPiece;
                var offset = (i % blocksPerPiece) * Constants.BlockSize;

                Buffer.BlockCopy (allData[i], 0, buffer, 0, allData[i].Length);
                await diskManager.WriteAsync (fileData, new BlockInfo (pieceIndex, offset, allData[i].Length), buffer);
            }

            using var _ = MemoryPool.Default.Rent (20, out Memory<byte> hashMemory);
            var hashes = new PieceHash (hashMemory);
            for (int i = 0; i < fileData.Hashes.Length; i++) {
                // Check twice because the first check should give us the result from the incremental hash.
                hashes.V1Hash.Span.Clear ();
                Assert.IsTrue (await diskManager.GetHashAsync (fileData, i, hashes));
                Assert.IsTrue (fileData.Hashes[i].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#2." + i);

                hashes.V1Hash.Span.Fill (0);
                Assert.IsTrue (await diskManager.GetHashAsync (fileData, i, hashes));
                Assert.IsTrue (fileData.Hashes[i].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#3." + i);
            }
        }

        [Test]
        public async Task WriteDataFromTwoTorrentsConcurrently ()
        {
            // Data from the primary torrent
            var allData = fileData.Data.SelectMany (t => t).ToArray ();

            // Data from a different torrent which hits the same pieces.
            var emptyBytes = new byte[Constants.BlockSize];
            var otherData = new TestTorrentData {
                Data = fileData.Data,
                Hashes = fileData.Hashes,
                TorrentInfo = fileData.TorrentInfo
            };

            int offset = 0;
            foreach (var block in allData.Partition (Constants.BlockSize)) {
                var buffer = new byte[Constants.BlockSize];
                Buffer.BlockCopy (block, 0, buffer, 0, block.Length);

                var request = new BlockInfo (offset / fileData.TorrentInfo.PieceLength, offset % fileData.TorrentInfo.PieceLength, block.Length);
                await Task.WhenAll (
                    diskManager.WriteAsync (fileData, request, buffer).AsTask (),
                    // Attempt to 'overwrite' the data from the primary torrent by writing the same block
                    // or the subsequent block
                    diskManager.WriteAsync (otherData, request, buffer).AsTask ()
                );
                offset += block.Length;
            }

            using var _ = MemoryPool.Default.Rent (20, out Memory<byte> hashMemory);
            var hashes = new PieceHash (hashMemory);
            for (int i = 0; i < fileData.Hashes.Length; i++) {
                // Check twice because the first check should give us the result from the incremental hash.
                hashes.V1Hash.Span.Fill (0);
                Assert.IsTrue (await diskManager.GetHashAsync (fileData, i, hashes));
                Assert.IsTrue (fileData.Hashes[i].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#2." + i);

                hashes.V1Hash.Span.Fill (0);
                Assert.IsTrue (await diskManager.GetHashAsync (fileData, i, hashes));
                Assert.IsTrue (fileData.Hashes[i].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#3." + i);
            }
            Assert.AreEqual (fileData.TorrentInfo.Size + otherData.TorrentInfo.Size, diskManager.TotalBytesWritten, "#4");
        }

        [Test]
        public async Task WriteBlock_SpanTwoFiles ()
        {
            var buffer = fileData.Data[0].Concat (fileData.Data[1]).Take (Constants.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, new BlockInfo (0, 0, Constants.BlockSize), buffer);

            Assert.AreEqual (2, writer.WrittenData.Count, "#1");
            Assert.IsTrue (fileData.Data[0].AsSpan (0, fileData.Data[0].Length).SequenceEqual (buffer.AsSpan (0, fileData.Data[0].Length)), "#2");
            Assert.IsTrue (fileData.Data[1].AsSpan (fileData.Data[0].Length, Constants.BlockSize - fileData.Data[1].Length).SequenceEqual (buffer.AsSpan (0, Constants.BlockSize - fileData.Data[1].Length)), "#3");
        }

        [Test]
        public async Task WritePiece_FirstTwoSwapped ([Values (0, Constants.BlockSize, Constants.BlockSize * 3)] int cacheSize)
        {
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { DiskCacheBytes = cacheSize }.ToSettings ());
            writer.Data = null;

            var blocks = fileData.Data
                .SelectMany (t => t)
                .Partition (Constants.BlockSize)
                .Take (fileData.TorrentInfo.PieceLength / Constants.BlockSize)
                .ToArray ();

            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 1, Constants.BlockSize), blocks[1]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 0, Constants.BlockSize), blocks[0]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 2, Constants.BlockSize), blocks[2]);

            using var _ = MemoryPool.Default.Rent (20, out Memory<byte> hashMemory);
            var hashes = new PieceHash (hashMemory);
            hashes.V1Hash.Span.Fill (0);
            Assert.IsTrue (await diskManager.GetHashAsync (fileData, 0, hashes));
            Assert.IsTrue (fileData.Hashes[0].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#1");
            // If we have at least Constants.BlockSize in the disk cache we'll need to read nothing from disk
            if (cacheSize < Constants.BlockSize)
                Assert.AreEqual (Constants.BlockSize * 2, writer.ReadData.Sum (t => t.Item3), "#2");
            else
                Assert.AreEqual (0, writer.ReadData.Sum (t => t.Item3), "#2");


            writer.ReadData.Clear ();
            hashes.V1Hash.Span.Fill (0);
            Assert.IsTrue (await diskManager.GetHashAsync (fileData, 0, hashes));
            Assert.IsTrue (fileData.Hashes[0].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#3");
            Assert.AreEqual (Constants.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WritePiece_InOrder ()
        {
            writer.Data = null;

            var blocks = fileData.Data
                .SelectMany (t => t).Partition (Constants.BlockSize)
                .Take (fileData.TorrentInfo.PieceLength / Constants.BlockSize)
                .ToArray ();

            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 0, Constants.BlockSize), blocks[0]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 1, Constants.BlockSize), blocks[1]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 2, Constants.BlockSize), blocks[2]);

            using var _ = MemoryPool.Default.Rent (20, out Memory<byte> hashMemory);
            var hashes = new PieceHash (hashMemory);
            hashes.V1Hash.Span.Fill (0);
            Assert.IsTrue (await diskManager.GetHashAsync (fileData, 0, hashes));
            Assert.IsTrue (fileData.Hashes[0].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#1");
            Assert.AreEqual (0, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            hashes.V1Hash.Span.Fill (0);
            Assert.IsTrue (await diskManager.GetHashAsync (fileData, 0, hashes));
            Assert.IsTrue (fileData.Hashes[0].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#3");
            Assert.AreEqual (Constants.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WritePiece_LastTwoSwapped ()
        {
            writer.Data = null;

            var blocks = fileData.Data
                .SelectMany (t => t)
                .Partition (Constants.BlockSize)
                .Take (fileData.TorrentInfo.PieceLength / Constants.BlockSize)
                .ToArray ();

            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 0, Constants.BlockSize), blocks[0]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 2, Constants.BlockSize), blocks[2]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 1, Constants.BlockSize), blocks[1]);

            using var _ = MemoryPool.Default.Rent (20, out Memory<byte> hashMemory);
            var hashes = new PieceHash (hashMemory);
            hashes.V1Hash.Span.Fill (0);
            Assert.IsTrue (await diskManager.GetHashAsync (fileData, 0, hashes));
            Assert.IsTrue (fileData.Hashes[0].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#1");
            Assert.AreEqual (Constants.BlockSize, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            hashes.V1Hash.Span.Fill (0);
            Assert.IsTrue (await diskManager.GetHashAsync (fileData, 0, hashes));
            Assert.IsTrue (fileData.Hashes[0].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#3");
            Assert.AreEqual (Constants.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WritePiece_ReverseOrder ()
        {
            writer.Data = null;

            var blocks = fileData.Data
                .SelectMany (t => t)
                .Partition (Constants.BlockSize)
                .Take (fileData.TorrentInfo.PieceLength / Constants.BlockSize)
                .ToArray ();

            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 2, Constants.BlockSize), blocks[2]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 1, Constants.BlockSize), blocks[1]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * 0, Constants.BlockSize), blocks[0]);

            using var _ = MemoryPool.Default.Rent (20, out Memory<byte> hashMemory);
            var hashes = new PieceHash (hashMemory);
            hashes.V1Hash.Span.Fill (0);
            Assert.IsTrue (await diskManager.GetHashAsync (fileData, 0, hashes));
            Assert.IsTrue (fileData.Hashes[0].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#1");
            Assert.AreEqual (Constants.BlockSize * 2, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            hashes.V1Hash.Span.Fill (0);
            Assert.IsTrue (await diskManager.GetHashAsync (fileData, 0, hashes));
            Assert.IsTrue (fileData.Hashes[0].AsSpan ().SequenceEqual (hashes.V1Hash.Span), "#3");
            Assert.AreEqual (Constants.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WriteRate ()
        {
            var buffer = new byte[Constants.BlockSize];
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskWriteRate = 1, DiskCacheBytes = 0 }.ToSettings ());
            await diskManager.Tick (1000);

            var tasks = new List<Task> ();
            for (int i = 0; i < SpeedMonitor.DefaultAveragePeriod + 1; i++)
                tasks.Add (diskManager.WriteAsync (fileData, new BlockInfo (i / (fileData.TorrentInfo.PieceLength / Constants.BlockSize), i, Constants.BlockSize), buffer).AsTask ());
            while (diskManager.PendingWriteBytes > 0) {
                await diskManager.Tick (1000).WithTimeout ();
                var done = await Task.WhenAny (tasks).WithTimeout ();
                await done.WithTimeout ();
                tasks.Remove (done);
            }

            await Task.WhenAll (tasks).WithTimeout ();

            // We should be writing at about 1 block per second.
            Assert.AreEqual (Constants.BlockSize, diskManager.WriteRate, "#1");
            Assert.AreEqual ((SpeedMonitor.DefaultAveragePeriod + 1) * Constants.BlockSize, diskManager.TotalBytesWritten, "#2");
        }
    }
}
