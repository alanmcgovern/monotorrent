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
using System.Threading.Tasks;

using MonoTorrent.PieceWriter;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.Client
{
    class TestPieceWriter : IPieceWriter
    {
        public int MaximumOpenFiles { get; }

        public ReusableTask CloseAsync (ITorrentManagerFile file)
        {
            return ReusableTask.CompletedTask;
        }

        public void Dispose ()
        {
        }

        public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
        {
            return ReusableTask.FromResult (false);
        }

        public ReusableTask FlushAsync (ITorrentManagerFile file)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
        {
            return ReusableTask.FromResult (0);
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            return ReusableTask.CompletedTask;
        }
    }

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

            // Queue up 6 reads, none should process.
            var buffer = new byte[Constants.BlockSize];
            int count = 6;
            var tasks = new List<Task> ();
            for (int i = 0; i < count; i++)
                tasks.Add (diskManager.ReadAsync (fileData, new BlockInfo (0, 0, buffer.Length), buffer).AsTask ());

            Assert.AreEqual (buffer.Length * count, diskManager.PendingReadBytes, "#1");

            // We should still process none.
            await diskManager.Tick (1000).WithTimeout ();
            Assert.AreEqual (buffer.Length * count, diskManager.PendingReadBytes, "#2");

            // Give a proper max read rate.
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskReadRate = Constants.BlockSize * 2 }.ToSettings ());
            for (int i = 0; i < 2; i++) {
                await diskManager.Tick (1000).WithTimeout ();

                for (int t = 0; t < 2; t++) {
                    var completed = await Task.WhenAny (tasks).WithTimeout ();
                    await completed;
                    tasks.Remove (completed);
                }
                Assert.IsFalse (tasks.Any (t => t.IsCompleted));

                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingReadBytes, "#3." + i);
            }

            // If we add more reads after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i++) {
                count++;
                tasks.Add (diskManager.ReadAsync (fileData, new BlockInfo (0, 0, buffer.Length), buffer).AsTask ());
            }
            Assert.AreEqual (buffer.Length * count, diskManager.PendingReadBytes, "#4." + count);
            while (count > 0) {
                await diskManager.Tick (1000).WithTimeout ();

                for (int t = 0; t < 2; t++) {
                    var completed = await Task.WhenAny (tasks).WithTimeout ();
                    await completed;
                    tasks.Remove (completed);
                }
                Assert.IsFalse (tasks.Any (t => t.IsCompleted));

                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingReadBytes, "#5." + count);
            }
        }

        [Test]
        public async Task ExceedWriteRate ()
        {
            // Ensure the read rate is smaller than a block
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskWriteRate = 1, DiskCacheBytes = 0 }.ToSettings ());
            await diskManager.Tick (1000);

            // Queue up 6 reads, none should process.
            var buffer = new byte[Constants.BlockSize];
            int count = 6;
            var tasks = new List<Task> ();
            for (int i = 0; i < count; i++)
                tasks.Add (diskManager.WriteAsync (fileData, new BlockInfo (i / 3, Constants.BlockSize * (i % 3), Constants.BlockSize), buffer).AsTask ());

            Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#1");

            // We should still process none.
            await diskManager.Tick (1000);

            Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#2");

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

                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#3." + i);
            }

            // If we add more writes after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i++) {
                count++;
                tasks.Add (diskManager.WriteAsync (fileData, new BlockInfo (0, Constants.BlockSize * i, Constants.BlockSize), buffer).AsTask ());
            }
            Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#4");

            while (diskManager.PendingWriteBytes > 0) {
                await diskManager.Tick (1000);
                for (int t = 0; t < 2; t++) {
                    var completed = await Task.WhenAny (tasks).WithTimeout ();
                    await completed;
                    tasks.Remove (completed);
                }
                Assert.IsFalse (tasks.Any (t => t.IsCompleted));

                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#5." + diskManager.PendingWriteBytes);
            }
        }

        [Test]
        public async Task MoveFile_ConvertsToFullPath ()
        {

            using var writer = new TestPieceWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            var file = TorrentFileInfo.Create (Constants.BlockSize, 123456).Single ();
            Assert.IsFalse (File.Exists (file.FullPath));

            await manager.MoveFileAsync (file, "NewPath");
            Assert.AreEqual (Path.GetFullPath ("NewPath"), file.FullPath);
            Assert.IsFalse (File.Exists (file.FullPath));
        }

        [Test]
        public async Task MoveFile_SamePath ()
        {
            using var tmp = TempDir.Create ();
            var file = TorrentFileInfo.Create (Constants.BlockSize, ("file.txt", 123456, Path.Combine (tmp.Path, "orig.txt"))).Single ();
            File.OpenWrite (file.FullPath).Close ();

            using var writer = new TestPieceWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            await manager.MoveFileAsync (file, file.FullPath);
            Assert.IsTrue (File.Exists (file.FullPath));
        }

        [Test]
        public async Task MoveFile_TargetDirectoryDoesNotExist ()
        {
            using var tmp = TempDir.Create ();
            var file = TorrentFileInfo.Create (Constants.BlockSize, ("file.txt", 123456, Path.Combine (tmp.Path, "orig.txt"))).Single ();
            File.OpenWrite (file.FullPath).Close ();

            using var writer = new TestPieceWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            var fullPath = Path.Combine (tmp.Path, "New", "Path", "file.txt");
            await manager.MoveFileAsync (file, fullPath);
            Assert.AreEqual (fullPath, file.FullPath);
        }

        [Test]
        public async Task MoveFiles_DoNotOverwrite ()
        {
            using var tmp = TempDir.Create ();
            using var newRoot = TempDir.Create ();

            var file = TorrentFileInfo.Create (Constants.BlockSize, ("file.txt", 123456, Path.Combine (tmp.Path, "sub_dir", "orig.txt"))).Single ();
            Directory.CreateDirectory (Path.GetDirectoryName (file.FullPath));
            File.OpenWrite (file.FullPath).Close ();

            using var writer = new TestPieceWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            await manager.MoveFilesAsync (new[] { file }, newRoot.Path, false);
            Assert.AreEqual (Path.Combine (newRoot.Path, file.Path), file.FullPath);
            //Assert.IsTrue (File.Exists (file.FullPath));
        }

        [Test]
        public async Task MoveFiles_Overwrite ()
        {
            using var tmp = TempDir.Create ();
            using var newRoot = TempDir.Create ();

            var file = TorrentFileInfo.Create (Constants.BlockSize, ("file.txt", 123456, Path.Combine (tmp.Path, "sub_dir", "orig.txt"))).Single ();
            Directory.CreateDirectory (Path.GetDirectoryName (file.FullPath));
            File.OpenWrite (file.FullPath).Close ();

            using var writer = new TestPieceWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            await manager.MoveFilesAsync (new[] { file }, newRoot.Path, true);
            Assert.AreEqual (Path.Combine (newRoot.Path, file.Path), file.FullPath);
            //Assert.IsTrue (File.Exists (file.FullPath));
        }

        [Test]
        public async Task MoveFiles_Overwrite_SameDir ()
        {
            using var tmp = TempDir.Create ();

            var file = TorrentFileInfo.Create (Constants.BlockSize, (Path.Combine ("sub_dir", "orig.txt"), 123456, Path.Combine (tmp.Path, "sub_dir", "orig.txt"))).Single ();
            Directory.CreateDirectory (Path.GetDirectoryName (file.FullPath));
            File.OpenWrite (file.FullPath).Close ();

            using var writer = new TestPieceWriter ();
            using var manager = new DiskManager (new EngineSettings (), Factories.Default, writer);

            await manager.MoveFilesAsync (new[] { file }, tmp.Path, true);
            Assert.AreEqual (Path.Combine (tmp.Path, file.Path), file.FullPath);
            Assert.IsTrue (File.Exists (file.FullPath));
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

            Assert.IsTrue (Toolbox.ByteMatch (buffer, 0, data1, 0, data1.Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (buffer, data1.Length, data2, 0, Constants.BlockSize - data1.Length), "#3");
        }

        [Test]
        public async Task ReadPieceTwo ()
        {
            var buffer = new byte[Constants.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, new BlockInfo (0, Constants.BlockSize, Constants.BlockSize), buffer), "#1");

            var data0 = fileData.Data[0];
            var data1 = fileData.Data[1];
            var data2 = fileData.Data[2];

            Assert.IsTrue (Toolbox.ByteMatch (buffer, 0, data1, data0.Length, data1.Length - data0.Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (buffer, data1.Length - data0.Length, data2, 0, Constants.BlockSize - (data1.Length - data0.Length)), "#3");
        }

        [Test]
        public async Task ReadRate ()
        {
            var buffer = new byte[Constants.BlockSize];
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskReadRate = Constants.BlockSize, DiskCacheBytes = 0 }.ToSettings ());
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
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Data[0], 0, buffer, 0, fileData.Data[0].Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Data[1], fileData.Data[0].Length, buffer, 0, Constants.BlockSize - fileData.Data[1].Length), "#3");
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
            await diskManager.UpdateSettingsAsync (new EngineSettingsBuilder { MaximumDiskWriteRate = Constants.BlockSize, DiskCacheBytes = 0 }.ToSettings ());
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
