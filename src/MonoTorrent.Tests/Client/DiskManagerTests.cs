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
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MonoTorrent.Client.PieceWriters;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class DiskManagerTests
    {
        class TestTorrentData : ITorrentData
        {
            public byte[][] Data { get; set; }
            public IList<ITorrentFileInfo> Files { get; set; }
            public byte[][] Hashes { get; set; }
            public int PieceLength { get; set; }
            public long Size { get; set; }
        }

        class PieceWriter : IPieceWriter
        {
            public Dictionary<ITorrentFileInfo, byte[]> Data = new Dictionary<ITorrentFileInfo, byte[]> ();
            public readonly List<Tuple<ITorrentFileInfo, long, int>> ReadData = new List<Tuple<ITorrentFileInfo, long, int>> ();
            public readonly List<Tuple<ITorrentFileInfo, long, byte[]>> WrittenData = new List<Tuple<ITorrentFileInfo, long, byte[]>> ();

            public List<ITorrentFileInfo> ClosedFiles = new List<ITorrentFileInfo> ();
            public List<ITorrentFileInfo> ExistsFiles = new List<ITorrentFileInfo> ();

            public ReusableTask<int> ReadAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count)
            {
                ReadData.Add (Tuple.Create (file, offset, count));

                if (Data == null) {
                    var fileData = WrittenData
                        .Where (t => t.Item1 == file)
                        .OrderBy (t => t.Item2)
                        .SelectMany (t => t.Item3)
                        .ToArray ();
                    Buffer.BlockCopy (fileData, (int) offset, buffer, bufferOffset, count);
                } else {
                    var data = Data[file];
                    Buffer.BlockCopy (data, (int) offset, buffer, bufferOffset, count);
                }
                return ReusableTask.FromResult (count);
            }

            public ReusableTask WriteAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count)
            {
                var result = new byte[count];
                Buffer.BlockCopy (buffer, bufferOffset, result, 0, count);
                WrittenData.Add (Tuple.Create (file, offset, result));
                return ReusableTask.CompletedTask;
            }

            public ReusableTask CloseAsync (ITorrentFileInfo file)
            {
                return ReusableTask.CompletedTask;
            }

            public ReusableTask<bool> ExistsAsync (ITorrentFileInfo file)
            {
                return ReusableTask.FromResult (true);
            }

            public ReusableTask FlushAsync (ITorrentFileInfo file)
            {
                return ReusableTask.CompletedTask;
            }

            public ReusableTask MoveAsync (ITorrentFileInfo file, string fullPath, bool overwrite)
            {
                return ReusableTask.CompletedTask;
            }

            public void Dispose ()
            {
            }
        }

        TestTorrentData fileData;
        DiskManager diskManager;
        EngineSettings settings;
        PieceWriter writer;

        [SetUp]
        public void Setup ()
        {
            var random = new Random ();
            var filePieces = new[] {
                ("First",  Piece.BlockSize / 2),
                ("Second", Piece.BlockSize),
                ("Third",  Piece.BlockSize + Piece.BlockSize / 2),
                ("Fourth", Piece.BlockSize * 6 + Piece.BlockSize / 2),
            };

            int pieceLength = Piece.BlockSize * 3;

            var files = new List<ITorrentFileInfo> ();
            long total = 0;
            foreach ((string name, long length) in filePieces) {
                var file = new TorrentFileInfo (new TorrentFile (name, length, (int)( total / pieceLength), (int)((total + length) / pieceLength), (int)(total % pieceLength), null, null, null));
                total += file.Length;
                files.Add (file);
            }

            var fileBytes = files
                .Select (f => { var b = new byte[f.Length]; random.NextBytes (b); return b; })
                .ToArray ();


            // Turn all the files into one byte array. Group the byte array into bittorrent pieces. Hash that piece.
            var hashes = fileBytes
                .SelectMany (t => t)
                .Partition (pieceLength)
                .Select (t => SHA1.Create ().ComputeHash (t))
                .ToArray ();

            fileData = new TestTorrentData {
                Data = fileBytes,
                Files = files.ToArray (),
                Hashes = hashes,
                Size = files.Sum (f => f.Length),
                PieceLength = pieceLength
            };

            writer = new PieceWriter ();
            for (int i = 0; i < files.Count; i++)
                writer.Data.Add (files[i], fileBytes[i]);

            settings = new EngineSettings ();
            diskManager = new DiskManager (settings, writer);
        }

        [Test]
        public async Task ExceedReadRate ()
        {
            // Ensure the read rate is smaller than a block
            diskManager.Settings = new EngineSettingsBuilder { MaximumDiskReadRate = 1 }.ToSettings ();
            await diskManager.Tick (1000);

            // Queue up 6 reads, none should process.
            var buffer = new byte[Piece.BlockSize];
            int count = 6;
            var tasks = new List<Task> ();
            for (int i = 0; i < count; i++)
                tasks.Add (diskManager.ReadAsync (fileData, 0, buffer, buffer.Length).AsTask ());

            Assert.AreEqual (buffer.Length * count, diskManager.PendingReads, "#1");

            // We should still process none.
            await diskManager.Tick (1000);
            Assert.AreEqual (buffer.Length * count, diskManager.PendingReads, "#2");

            // Give a proper max read rate.
            diskManager.Settings = new EngineSettingsBuilder { MaximumDiskReadRate = Piece.BlockSize * 2 }.ToSettings ();
            for (int i = 0; i < 2; i++) {
                await diskManager.Tick (1000);
                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingReads, "#3." + i);
            }

            // If we add more reads after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i++) {
                count++;
                tasks.Add (diskManager.ReadAsync (fileData, 0, buffer, buffer.Length).AsTask ());
            }
            Assert.AreEqual (buffer.Length * count, diskManager.PendingReads, "#4." + count);

            while (count > 0) {
                await diskManager.Tick (1000);
                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingReads, "#5." + count);
            }

            foreach (var v in tasks)
                Assert.DoesNotThrowAsync (async () => await v.WithTimeout (1000), "#6");
        }

        [Test]
        public async Task ExceedWriteRate ()
        {
            // Ensure the read rate is smaller than a block
            diskManager.Settings = new EngineSettingsBuilder { MaximumDiskWriteRate = 1 }.ToSettings ();
            await diskManager.Tick (1000);

            // Queue up 6 reads, none should process.
            var buffer = new byte[Piece.BlockSize];
            int count = 6;
            var tasks = new List<ReusableTask> ();
            for (int i = 0; i < count; i++)
                tasks.Add (diskManager.WriteAsync (fileData, Piece.BlockSize * i, buffer, buffer.Length));

            Assert.AreEqual (buffer.Length * count, diskManager.PendingWrites, "#1");

            // We should still process none.
            await diskManager.Tick (1000);
            Assert.AreEqual (buffer.Length * count, diskManager.PendingWrites, "#2");

            // Give a proper max read rate.
            diskManager.Settings = new EngineSettingsBuilder { MaximumDiskWriteRate = Piece.BlockSize * 2 }.ToSettings ();
            for (int i = 0; i < 2; i++) {
                await diskManager.Tick (1000);
                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingWrites, "#3." + i);
            }

            // If we add more writes after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i++) {
                count++;
                tasks.Add (diskManager.WriteAsync (fileData, Piece.BlockSize * i, buffer, buffer.Length));
            }
            Assert.AreEqual (buffer.Length * count, diskManager.PendingWrites, "#4");

            while (diskManager.PendingWrites > 0) {
                await diskManager.Tick (1000);
                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingWrites, "#5." + diskManager.PendingWrites);
            }

            foreach (var v in tasks)
                Assert.DoesNotThrowAsync (async () => await v.WithTimeout (1000), "#6");
        }

        [Test]
        public void FindFile_FirstFile ()
        {
            var file = fileData.Files[0];
            Assert.AreEqual (0, DiskManager.FindFileIndex (fileData.Files, 0, fileData.PieceLength));
            Assert.AreEqual (0, DiskManager.FindFileIndex (fileData.Files, 1, fileData.PieceLength));
            Assert.AreEqual (0, DiskManager.FindFileIndex (fileData.Files, file.Length - 1, fileData.PieceLength));
        }

        [Test]
        public void FindFile_SecondFile ()
        {
            Assert.AreEqual (1, DiskManager.FindFileIndex (fileData.Files, fileData.Files[0].Length, fileData.PieceLength));
        }

        [Test]
        public void FindFile_LastFile ()
        {
            Assert.AreEqual (fileData.Files.Count - 1, DiskManager.FindFileIndex (fileData.Files, fileData.Files.Last ().Length - 1, fileData.PieceLength));
        }

        [Test]
        public void FindFile_InvalidOffset ()
        {
            var totalSize = fileData.Files.Sum (t => t.Length);
            Assert.Negative (DiskManager.FindFileIndex (fileData.Files, totalSize, fileData.PieceLength));
            Assert.Negative (DiskManager.FindFileIndex (fileData.Files, -1, fileData.PieceLength));
        }

        [Test]
        public async Task ReadAllData ()
        {
            var buffer = new byte[fileData.Size];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, 0, buffer, buffer.Length), "#1");

            int offset = 0;
            foreach (var data in fileData.Data) {
                Assert.IsTrue (Toolbox.ByteMatch (buffer, offset, data, 0, data.Length), "#2");
                offset += data.Length;
            }
        }

        [Test]
        public void ReadPastTheEnd ()
        {
            var buffer = new byte[Piece.BlockSize];
            Assert.ThrowsAsync<ArgumentOutOfRangeException> (() => diskManager.ReadAsync (fileData, Piece.BlockSize * 1000, buffer, buffer.Length).AsTask (), "#1");
        }

        [Test]
        public async Task ReadPieceOne ()
        {
            var buffer = new byte[Piece.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, 0, buffer, buffer.Length), "#1");

            var data1 = fileData.Data[0];
            var data2 = fileData.Data[1];

            Assert.IsTrue (Toolbox.ByteMatch (buffer, 0, data1, 0, data1.Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (buffer, data1.Length, data2, 0, Piece.BlockSize - data1.Length), "#3");
        }

        [Test]
        public async Task ReadPieceTwo ()
        {
            var buffer = new byte[Piece.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, Piece.BlockSize, buffer, buffer.Length), "#1");

            var data0 = fileData.Data[0];
            var data1 = fileData.Data[1];
            var data2 = fileData.Data[2];

            Assert.IsTrue (Toolbox.ByteMatch (buffer, 0, data1, data0.Length, data1.Length - data0.Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (buffer, data1.Length - data0.Length, data2, 0, Piece.BlockSize - (data1.Length - data0.Length)), "#3");
        }

        [Test]
        public async Task ReadRate ()
        {
            var buffer = new byte[Piece.BlockSize];
            diskManager.Settings = new EngineSettingsBuilder { MaximumDiskReadRate = Piece.BlockSize }.ToSettings ();
            await diskManager.Tick (1000);

            for (int i = 0; i < SpeedMonitor.DefaultAveragePeriod * 2; i++)
                _ = diskManager.ReadAsync (fileData, 0, buffer, buffer.Length);

            while (diskManager.PendingReads > 0)
                await diskManager.Tick (1000);

            // We should be reading at about 1 block per second.
            Assert.IsTrue (Math.Abs (Piece.BlockSize - diskManager.ReadRate) < (Piece.BlockSize * 0.1), "#1");
        }

        [Test]
        public async Task WriteAllData ()
        {
            var allData = fileData.Data.SelectMany (t => t).ToArray ();
            await diskManager.WriteAsync (fileData, 0, allData, allData.Length);

            var offset = 0;
            foreach (var data in fileData.Data) {
                Assert.IsTrue (Toolbox.ByteMatch (allData, offset, data, 0, data.Length), "#1");
                offset += data.Length;
            }

            for (int i = 0; i < fileData.Hashes.Length; i++) {
                // Check twice because the first check should give us the result from the incremental hash.
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[i], await diskManager.GetHashAsync (fileData, i)), "#2." + i);
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[i], await diskManager.GetHashAsync (fileData, i)), "#3." + i);
            }
        }

        [Test]
        public async Task WriteAllData_ByBlock ()
        {
            var allData = fileData.Data.SelectMany (t => t).ToArray ();

            int offset = 0;
            foreach (var block in allData.Partition (Piece.BlockSize)) {
                await diskManager.WriteAsync (fileData, offset, block, block.Length);
                offset += block.Length;
            }

            offset = 0;
            foreach (var data in fileData.Data) {
                Assert.IsTrue (Toolbox.ByteMatch (allData, offset, data, 0, data.Length), "#1");
                offset += data.Length;
            }

            for (int i = 0; i < fileData.Hashes.Length; i++) {
                // Check twice because the first check should give us the result from the incremental hash.
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[i], await diskManager.GetHashAsync (fileData, i)), "#2." + i);
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[i], await diskManager.GetHashAsync (fileData, i)), "#3." + i);
            }
        }

        [Test]
        public async Task WriteDataFromTwoTorrentsConcurrently ()
        {
            // Data from the primary torrent
            var allData = fileData.Data.SelectMany (t => t).ToArray ();

            // Data from a different torrent which hits the same pieces.
            var emptyBytes = new byte[Piece.BlockSize];
            var otherData = new TestTorrentData {
                Data = fileData.Data,
                Files = fileData.Files,
                Hashes = fileData.Hashes,
                PieceLength = fileData.PieceLength,
                Size = fileData.Size
            };

            int offset = 0;
            foreach (var block in allData.Partition (Piece.BlockSize)) {
                await diskManager.WriteAsync (fileData, offset, block, block.Length);

                // Attempt to 'overwrite' the data from the primary torrent by writing the same block
                // or the subsequent block
                await diskManager.WriteAsync (otherData, offset, emptyBytes, block.Length);

                // Don't accidentally write beyond the end of the data
                if (offset + Piece.BlockSize < otherData.Size) {
                    var count = offset + Piece.BlockSize + block.Length > otherData.Size ? otherData.Size % otherData.PieceLength : Piece.BlockSize;
                    await diskManager.WriteAsync (otherData, offset + Piece.BlockSize, emptyBytes, (int) count);
                }
                offset += block.Length;
            }

            offset = 0;
            foreach (var data in fileData.Data) {
                Assert.IsTrue (Toolbox.ByteMatch (allData, offset, data, 0, data.Length), "#1");
                offset += data.Length;
            }

            for (int i = 0; i < fileData.Hashes.Length; i++) {
                // Check twice because the first check should give us the result from the incremental hash.
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[i], await diskManager.GetHashAsync (fileData, i)), "#2." + i);
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[i], await diskManager.GetHashAsync (fileData, i)), "#3." + i);
            }
        }

        [Test]
        public async Task WriteBlock_SpanTwoFiles ()
        {
            var buffer = fileData.Data[0].Concat (fileData.Data[1]).Take (Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, 0, buffer, buffer.Length);

            Assert.AreEqual (2, writer.WrittenData.Count, "#1");
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Data[0], 0, buffer, 0, fileData.Data[0].Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Data[1], fileData.Data[0].Length, buffer, 0, Piece.BlockSize - fileData.Data[1].Length), "#3");
        }

        [Test]
        public async Task WritePiece_FirstTwoSwapped ()
        {
            writer.Data = null;

            var blocks = fileData.Data.SelectMany (t => t).Partition (Piece.BlockSize).Take (fileData.PieceLength / Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, Piece.BlockSize, blocks[1], blocks[1].Length);
            await diskManager.WriteAsync (fileData, 0, blocks[0], blocks[0].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize * 2, blocks[2], blocks[2].Length);

            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[0], await diskManager.GetHashAsync (fileData, 0)), "#1");
            Assert.AreEqual (Piece.BlockSize * 2, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[0], await diskManager.GetHashAsync (fileData, 0)), "#3");
            Assert.AreEqual (Piece.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WritePiece_InOrder ()
        {
            writer.Data = null;

            var blocks = fileData.Data.SelectMany (t => t).Partition (Piece.BlockSize).Take (fileData.PieceLength / Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, 0, blocks[0], blocks[0].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize, blocks[1], blocks[1].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize * 2, blocks[2], blocks[2].Length);

            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[0], await diskManager.GetHashAsync (fileData, 0)), "#1");
            Assert.AreEqual (0, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[0], await diskManager.GetHashAsync (fileData, 0)), "#3");
            Assert.AreEqual (Piece.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WritePiece_LastTwoSwapped ()
        {
            writer.Data = null;

            var blocks = fileData.Data.SelectMany (t => t).Partition (Piece.BlockSize).Take (fileData.PieceLength / Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, 0, blocks[0], blocks[0].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize * 2, blocks[2], blocks[2].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize, blocks[1], blocks[1].Length);

            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[0], await diskManager.GetHashAsync (fileData, 0)), "#1");
            Assert.AreEqual (Piece.BlockSize, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[0], await diskManager.GetHashAsync (fileData, 0)), "#3");
            Assert.AreEqual (Piece.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WritePiece_ReverseOrder ()
        {
            writer.Data = null;

            var blocks = fileData.Data.SelectMany (t => t).Partition (Piece.BlockSize).Take (fileData.PieceLength / Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, Piece.BlockSize * 2, blocks[2], blocks[2].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize, blocks[1], blocks[1].Length);
            await diskManager.WriteAsync (fileData, 0, blocks[0], blocks[0].Length);

            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[0], await diskManager.GetHashAsync (fileData, 0)), "#1");
            Assert.AreEqual (Piece.BlockSize * 2, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[0], await diskManager.GetHashAsync (fileData, 0)), "#3");
            Assert.AreEqual (Piece.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WriteRate ()
        {
            var buffer = new byte[Piece.BlockSize];
            diskManager.Settings = new EngineSettingsBuilder { MaximumDiskReadRate = Piece.BlockSize }.ToSettings ();
            await diskManager.Tick (1000);

            for (int i = 0; i < SpeedMonitor.DefaultAveragePeriod * 2; i++)
                _ = diskManager.WriteAsync (fileData, Piece.BlockSize * i, buffer, buffer.Length);

            while (diskManager.PendingWrites > 0)
                await diskManager.Tick (1000);

            // We should be writing at about 1 block per second.
            Assert.IsTrue (Math.Abs (Piece.BlockSize - diskManager.WriteRate) < (Piece.BlockSize * 0.1), "#1");
        }
    }
}
