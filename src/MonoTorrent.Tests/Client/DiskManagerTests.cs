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

using MonoTorrent.Client.PiecePicking;
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
        PieceWriter writer;

        [OneTimeSetUp]
        public void OnetimeSetup ()
        {
            var random = new Random ();
            var filePieces = new long[] {
                Piece.BlockSize / 2,
                Piece.BlockSize,
                Piece.BlockSize + Piece.BlockSize / 2,
                Piece.BlockSize * 10 + Piece.BlockSize / 2,
            };

            int pieceLength = Piece.BlockSize * 3;

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

            fileData = new TestTorrentData {
                Data = fileBytes,
                Files = files.ToArray (),
                Hashes = hashes,
                Size = files.Sum (f => f.Length),
                PieceLength = pieceLength
            };
        }

        [SetUp]
        public void Setup ()
        {
            writer = new PieceWriter ();
            for (int i = 0; i < fileData.Files.Count; i++)
                writer.Data.Add (fileData.Files[i], fileData.Data[i]);

            diskManager = new DiskManager (new EngineSettings (), writer);
        }

        [Test]
        public async Task ExceedReadRate ()
        {
            // Ensure the read rate is smaller than a block
            diskManager.UpdateSettings (new EngineSettingsBuilder { MaximumDiskReadRate = 1 }.ToSettings ());
            await diskManager.Tick (1000).WithTimeout ();

            // Queue up 6 reads, none should process.
            var buffer = new byte[Piece.BlockSize];
            int count = 6;
            var tasks = new List<Task> ();
            for (int i = 0; i < count; i++)
                tasks.Add (diskManager.ReadAsync (fileData, new BlockInfo (0, 0, buffer.Length), buffer).AsTask ());

            Assert.AreEqual (buffer.Length * count, diskManager.PendingReadBytes, "#1");

            // We should still process none.
            await diskManager.Tick (1000).WithTimeout ();
            Assert.AreEqual (buffer.Length * count, diskManager.PendingReadBytes, "#2");

            // Give a proper max read rate.
            diskManager.UpdateSettings (new EngineSettingsBuilder { MaximumDiskReadRate = Piece.BlockSize * 2 }.ToSettings ());
            for (int i = 0; i < 2; i++) {
                await diskManager.Tick (1000).WithTimeout ();

                await tasks[0].WithTimeout ();
                tasks.RemoveAt (0);

                await tasks[0].WithTimeout ();
                tasks.RemoveAt (0);

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

                await tasks[0].WithTimeout ();
                tasks.RemoveAt (0);

                await tasks[0].WithTimeout ();
                tasks.RemoveAt (0);

                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingReadBytes, "#5." + count);
            }
        }

        [Test]
        public async Task ExceedWriteRate ()
        {
            // Ensure the read rate is smaller than a block
            diskManager.UpdateSettings (new EngineSettingsBuilder { MaximumDiskWriteRate = 1, DiskCacheBytes = 0 }.ToSettings ());
            await diskManager.Tick (1000);

            // Queue up 6 reads, none should process.
            var buffer = new byte[Piece.BlockSize];
            int count = 6;
            var tasks = new List<Task> ();
            for (int i = 0; i < count; i++)
                tasks.Add (diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * i, Piece.BlockSize), buffer).AsTask ());

            Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#1");

            // We should still process none.
            await diskManager.Tick (1000);

            Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#2");

            // Give a proper max read rate.
            diskManager.UpdateSettings (new EngineSettingsBuilder { MaximumDiskWriteRate = Piece.BlockSize * 2, DiskCacheBytes = 0 }.ToSettings ());
            for (int i = 0; i < 2; i++) {
                await diskManager.Tick (1000);
                await tasks[0].WithTimeout ();
                tasks.RemoveAt (0);

                await tasks[0].WithTimeout ();
                tasks.RemoveAt (0);

                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#3." + i);
            }

            // If we add more writes after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i++) {
                count++;
                tasks.Add (diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * i, Piece.BlockSize), buffer).AsTask ());
            }
            Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#4");

            while (diskManager.PendingWriteBytes > 0) {
                await diskManager.Tick (1000);
                await tasks[0].WithTimeout ();
                tasks.RemoveAt (0);

                await tasks[0].WithTimeout ();
                tasks.RemoveAt (0);
                count -= 2;
                Assert.AreEqual (buffer.Length * count, diskManager.PendingWriteBytes, "#5." + diskManager.PendingWriteBytes);
            }
        }

        [Test]
        public void ReadPastTheEnd ()
        {
            var buffer = new byte[Piece.BlockSize];
            Assert.ThrowsAsync<ArgumentOutOfRangeException> (() => diskManager.ReadAsync (fileData, new BlockInfo (1000, 0, Piece.BlockSize), buffer).AsTask (), "#1");
        }

        [Test]
        public async Task ReadPieceOne ()
        {
            var buffer = new byte[Piece.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, new BlockInfo (0, 0, Piece.BlockSize), buffer), "#1");

            var data1 = fileData.Data[0];
            var data2 = fileData.Data[1];

            Assert.IsTrue (Toolbox.ByteMatch (buffer, 0, data1, 0, data1.Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (buffer, data1.Length, data2, 0, Piece.BlockSize - data1.Length), "#3");
        }

        [Test]
        public async Task ReadPieceTwo ()
        {
            var buffer = new byte[Piece.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, new BlockInfo (0, Piece.BlockSize, Piece.BlockSize), buffer), "#1");

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
            diskManager.UpdateSettings (new EngineSettingsBuilder { MaximumDiskReadRate = Piece.BlockSize }.ToSettings ());
            await diskManager.Tick (1000);

            var tasks = new List<Task> ();
            for (int i = 0; i < SpeedMonitor.DefaultAveragePeriod + 1; i++)
                tasks.Add (diskManager.ReadAsync (fileData, new BlockInfo (0, 0, Piece.BlockSize), buffer).AsTask ());

            while (diskManager.PendingReadBytes > 0)
                await diskManager.Tick (1000);

            // We should be reading at about 1 block per second.
            Assert.AreEqual (Piece.BlockSize, diskManager.ReadRate, "#1");
            Assert.AreEqual ((SpeedMonitor.DefaultAveragePeriod + 1) * Piece.BlockSize, diskManager.TotalBytesRead, "#2");
            await Task.WhenAll (tasks).WithTimeout ();
        }

        [Test]
        public async Task WriteAllData ()
        {
            var buffer = new byte[Piece.BlockSize];
            var allData = fileData.Data.SelectMany (t => t).Partition (Piece.BlockSize).ToArray ();
            int blocksPerPiece = fileData.PieceLength / Piece.BlockSize;
            for (int i = 0; i < allData.Length; i ++) {
                var pieceIndex = i / blocksPerPiece;
                var offset = (i % blocksPerPiece) * Piece.BlockSize;

                Buffer.BlockCopy (allData[i], 0, buffer, 0, allData[i].Length);
                await diskManager.WriteAsync (fileData, new BlockInfo (pieceIndex, offset, allData[i].Length), buffer);
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
                var buffer = new byte[Piece.BlockSize];
                Buffer.BlockCopy (block, 0, buffer, 0, block.Length);

                var request = new BlockInfo (offset / fileData.PieceLength, offset % fileData.PieceLength, block.Length);
                await Task.WhenAll (
                    diskManager.WriteAsync (fileData, request, buffer).AsTask (),
                    // Attempt to 'overwrite' the data from the primary torrent by writing the same block
                    // or the subsequent block
                    diskManager.WriteAsync (otherData, request, buffer).AsTask ()
                );
                offset += block.Length;
            }

            for (int i = 0; i < fileData.Hashes.Length; i++) {
                // Check twice because the first check should give us the result from the incremental hash.
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[i], await diskManager.GetHashAsync (fileData, i)), "#2." + i);
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes[i], await diskManager.GetHashAsync (fileData, i)), "#3." + i);
            }
            Assert.AreEqual (fileData.Size + otherData.Size, diskManager.TotalBytesWritten, "#4");
        }

        [Test]
        public async Task WriteBlock_SpanTwoFiles ()
        {
            var buffer = fileData.Data[0].Concat (fileData.Data[1]).Take (Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, new BlockInfo (0, 0, Piece.BlockSize), buffer);

            Assert.AreEqual (2, writer.WrittenData.Count, "#1");
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Data[0], 0, buffer, 0, fileData.Data[0].Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Data[1], fileData.Data[0].Length, buffer, 0, Piece.BlockSize - fileData.Data[1].Length), "#3");
        }

        [Test]
        public async Task WritePiece_FirstTwoSwapped ()
        {
            writer.Data = null;

            var blocks = fileData.Data
                .SelectMany (t => t)
                .Partition (Piece.BlockSize)
                .Take (fileData.PieceLength / Piece.BlockSize)
                .ToArray ();

            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 1, Piece.BlockSize), blocks[1]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 0, Piece.BlockSize), blocks[0]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 2, Piece.BlockSize), blocks[2]);

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

            var blocks = fileData.Data
                .SelectMany (t => t).Partition (Piece.BlockSize)
                .Take (fileData.PieceLength / Piece.BlockSize)
                .ToArray ();

            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 0, Piece.BlockSize), blocks[0]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 1, Piece.BlockSize), blocks[1]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 2, Piece.BlockSize), blocks[2]);

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

            var blocks = fileData.Data
                .SelectMany (t => t)
                .Partition (Piece.BlockSize)
                .Take (fileData.PieceLength / Piece.BlockSize)
                .ToArray ();

            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 0, Piece.BlockSize), blocks[0]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 2, Piece.BlockSize), blocks[2]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 1, Piece.BlockSize), blocks[1]);

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

            var blocks = fileData.Data
                .SelectMany (t => t)
                .Partition (Piece.BlockSize)
                .Take (fileData.PieceLength / Piece.BlockSize)
                .ToArray ();

            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 2, Piece.BlockSize), blocks[2]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 1, Piece.BlockSize), blocks[1]);
            await diskManager.WriteAsync (fileData, new BlockInfo (0, Piece.BlockSize * 0, Piece.BlockSize), blocks[0]);

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
            diskManager.UpdateSettings (new EngineSettingsBuilder { MaximumDiskWriteRate = Piece.BlockSize }.ToSettings ());
            await diskManager.Tick (1000);

            var writes = new List<Task> ();
            for (int i = 0; i < SpeedMonitor.DefaultAveragePeriod + 1; i++)
                writes.Add (diskManager.WriteAsync (fileData, new BlockInfo (i / (fileData.PieceLength / Piece.BlockSize), i, Piece.BlockSize), buffer).AsTask ());
            while (diskManager.PendingWriteBytes > 0)
                await diskManager.Tick (1000).WithTimeout ();

            // We should be writing at about 1 block per second.
            Assert.AreEqual (Piece.BlockSize, diskManager.WriteRate, "#1");
            Assert.AreEqual ((SpeedMonitor.DefaultAveragePeriod + 1) * Piece.BlockSize, diskManager.TotalBytesWritten, "#2");
            await Task.WhenAll (writes).WithTimeout ();
        }
    }
}
