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

namespace MonoTorrent.Client
{
    [TestFixture]
    public class DiskManagerTests
    {
        class TestTorrentData : ITorrentData
        {
            public byte [][] Data { get; set; }
            public TorrentFile [] Files { get; set; }
            public byte [][] Hashes { get; set; }
            public int PieceLength { get; set; }
            public long Size { get; set; }
        }

        class PieceWriter : IPieceWriter
        {
            public Dictionary<TorrentFile, byte[]> Data = new Dictionary<TorrentFile, byte[]> ();
            public List<Tuple<TorrentFile, long, int>> ReadData = new List<Tuple<TorrentFile, long, int>> ();
            public List<Tuple<TorrentFile, long, byte[]>> WrittenData = new List<Tuple<TorrentFile, long, byte[]>> ();

            public List<TorrentFile> ClosedFiles = new List<TorrentFile> ();
            public List<TorrentFile> ExistsFiles = new List<TorrentFile> ();

            public void Close (TorrentFile file)
            {
            }

            public void Dispose ()
            {
            }

            public bool Exists (TorrentFile file)
            {

                return true;
            }

            public void Flush (TorrentFile file)
            {
            }

            public void Move (TorrentFile file, string fullPath, bool overwrite)
            {
            }

            public int Read (TorrentFile file, long offset, byte [] buffer, int bufferOffset, int count)
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
                return count;
            }

            public void Write (TorrentFile file, long offset, byte [] buffer, int bufferOffset, int count)
            {
                var result = new byte [count];
                Buffer.BlockCopy (buffer, bufferOffset, result, 0, count);
                WrittenData.Add (Tuple.Create (file, offset, result));
            }
        }

        TestTorrentData fileData;
        DiskManager diskManager;
        EngineSettings settings;
        PieceWriter writer;

        [SetUp]
        public void Setup()
        {
            var random = new Random ();
            var files = new [] {
                new TorrentFile ("First",  Piece.BlockSize / 2),
                new TorrentFile ("Second", Piece.BlockSize),
                new TorrentFile ("Third",  Piece.BlockSize + Piece.BlockSize / 2),
                new TorrentFile ("Fourth", Piece.BlockSize * 6 + Piece.BlockSize / 2),
            };

            var fileBytes = files
                .Select (f => { var b = new byte [f.Length]; random.NextBytes (b); return b; })
                .ToArray ();


            int pieceLength = Piece.BlockSize * 3;
            // Turn all the files into one byte array. Group the byte array into bittorrent pieces. Hash that piece.
            var hashes = fileBytes
                .SelectMany (t => t)
                .Partition (pieceLength)
                .Select (t => SHA1.Create ().ComputeHash (t))
                .ToArray ();

            fileData = new TestTorrentData {
                Data = fileBytes,
                Files = files,
                Hashes = hashes,
                Size = files.Sum (f => f.Length),
                PieceLength = pieceLength
            };

            writer = new PieceWriter ();
            for (int i = 0; i < files.Length; i ++)
                writer.Data.Add (files[i], fileBytes[i]);

            settings = new EngineSettings ();
            diskManager = new DiskManager (settings, writer);
        }


        [Test]
        public async Task ExceedReadRate ()
        {
            // Ensure the read rate is smaller than a block
            settings.MaximumDiskReadRate = 1;
            await diskManager.Tick (1000);

            // Queue up 6 reads, none should process.
            var buffer = new byte[Piece.BlockSize];
            var tasks = new List<Task> ();
            for (int i = 0; i < 6; i ++)
                tasks.Add (diskManager.ReadAsync (fileData, 0, buffer, buffer.Length));

            // We should still process none.
            await diskManager.Tick (1000);
            Assert.IsTrue (tasks.All (t => t.IsCompleted == false), "#1");

            // Give a proper max read rate.
            settings.MaximumDiskReadRate = Piece.BlockSize * 2;
            for (int i = 0; i < 2; i ++) {
                await diskManager.Tick (1000);
                Assert.AreEqual (2, tasks.Count (t => t.IsCompleted), "#1");
                tasks.RemoveAll (t => t.IsCompleted);
            }

            // If we add more reads after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i ++)
                tasks.Add (diskManager.ReadAsync (fileData, 0, buffer, buffer.Length));

            while (tasks.Count > 0) {
                await diskManager.Tick (1000);
                Assert.AreEqual (2, tasks.Count (t => t.IsCompleted), "#1");
                tasks.RemoveAll (t => t.IsCompleted);
            }
        }

        [Test]
        public async Task ExceedWriteRate ()
        {
            // Ensure the read rate is smaller than a block
            settings.MaximumDiskWriteRate = 1;
            await diskManager.Tick (1000);

            // Queue up 6 reads, none should process.
            var buffer = new byte[Piece.BlockSize];
            var tasks = new List<Task> ();
            for (int i = 0; i < 6; i ++)
                tasks.Add (diskManager.WriteAsync (fileData, 0, buffer, buffer.Length));

            // We should still process none.
            await diskManager.Tick (1000);
            Assert.IsTrue (tasks.All (t => t.IsCompleted == false), "#1");

            // Give a proper max read rate.
            settings.MaximumDiskWriteRate = Piece.BlockSize * 2;
            for (int i = 0; i < 2; i ++) {
                await diskManager.Tick (1000);
                Assert.AreEqual (2, tasks.Count (t => t.IsCompleted), "#1");
                tasks.RemoveAll (t => t.IsCompleted);
            }

            // If we add more writes after we used up our allowance they still won't process.
            for (int i = 0; i < 2; i ++)
                tasks.Add (diskManager.WriteAsync (fileData, 0, buffer, buffer.Length));

            while (tasks.Count > 0) {
                await diskManager.Tick (1000);
                Assert.AreEqual (2, tasks.Count (t => t.IsCompleted), "#1");
                tasks.RemoveAll (t => t.IsCompleted);
            }
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
            Assert.ThrowsAsync<ArgumentOutOfRangeException> (() => diskManager.ReadAsync (fileData, Piece.BlockSize * 1000, buffer, buffer.Length), "#1");
        }

        [Test]
        public async Task ReadPieceOne ()
        {
            var buffer = new byte[Piece.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, 0, buffer, buffer.Length), "#1");

            var data1 = fileData.Data [0];
            var data2 = fileData.Data [1];

            Assert.IsTrue (Toolbox.ByteMatch (buffer, 0, data1, 0, data1.Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (buffer, data1.Length, data2, 0, Piece.BlockSize - data1.Length), "#3");
        }

        [Test]
        public async Task ReadPieceTwo ()
        {
            var buffer = new byte[Piece.BlockSize];
            Assert.IsTrue (await diskManager.ReadAsync (fileData, Piece.BlockSize, buffer, buffer.Length), "#1");

            var data0 = fileData.Data [0];
            var data1 = fileData.Data [1];
            var data2 = fileData.Data [2];

            Assert.IsTrue (Toolbox.ByteMatch (buffer, 0, data1, data0.Length, data1.Length - data0.Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (buffer, data1.Length - data0.Length, data2, 0, Piece.BlockSize - (data1.Length - data0.Length)), "#3");
        }

        [Test]
        public async Task ReadRate ()
        {
            var buffer = new byte [Piece.BlockSize];
            settings.MaximumDiskReadRate = Piece.BlockSize;
            await diskManager.Tick (1000);

            for (int i = 0; i < SpeedMonitor.DefaultAveragePeriod * 2; i ++)
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

            for (int i = 0; i < fileData.Hashes.Length; i ++) {
                // Check twice because the first check should give us the result from the incremental hash.
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [i], await diskManager.GetHashAsync (fileData, i)), "#2." + i);
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [i], await diskManager.GetHashAsync (fileData, i)), "#3." + i);
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

            for (int i = 0; i < fileData.Hashes.Length; i ++) {
                // Check twice because the first check should give us the result from the incremental hash.
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [i], await diskManager.GetHashAsync (fileData, i)), "#2." + i);
                Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [i], await diskManager.GetHashAsync (fileData, i)), "#3." + i);
            }
        }

        [Test]
        public async Task WriteBlock_SpanTwoFiles ()
        {
            var buffer = fileData.Data[0].Concat (fileData.Data[1]).Take (Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, 0, buffer, buffer.Length);

            Assert.AreEqual (2, writer.WrittenData.Count, "#1");
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Data [0], 0, buffer, 0, fileData.Data[0].Length), "#2");
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Data [1], fileData.Data[0].Length, buffer, 0, Piece.BlockSize - fileData.Data[1].Length), "#3");
        }

        [Test]
        public async Task WritePiece_FirstTwoSwapped()
        {
            writer.Data = null;

            var blocks = fileData.Data.SelectMany (t => t).Partition (Piece.BlockSize).Take (fileData.PieceLength / Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, Piece.BlockSize, blocks[1], blocks[1].Length);
            await diskManager.WriteAsync (fileData, 0, blocks[0], blocks[0].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize * 2, blocks[2], blocks[2].Length);

            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [0], await diskManager.GetHashAsync (fileData, 0)), "#1");
            Assert.AreEqual (Piece.BlockSize * 2, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [0], await diskManager.GetHashAsync (fileData, 0)), "#3");
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

            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [0], await diskManager.GetHashAsync (fileData, 0)), "#1");
            Assert.AreEqual (0, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [0], await diskManager.GetHashAsync (fileData, 0)), "#3");
            Assert.AreEqual (Piece.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WritePiece_LastTwoSwapped()
        {
            writer.Data = null;

            var blocks = fileData.Data.SelectMany (t => t).Partition (Piece.BlockSize).Take (fileData.PieceLength / Piece.BlockSize).ToArray ();
            await diskManager.WriteAsync (fileData, 0, blocks[0], blocks[0].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize * 2, blocks[2], blocks[2].Length);
            await diskManager.WriteAsync (fileData, Piece.BlockSize, blocks[1], blocks[1].Length);

            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [0], await diskManager.GetHashAsync (fileData, 0)), "#1");
            Assert.AreEqual (Piece.BlockSize, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [0], await diskManager.GetHashAsync (fileData, 0)), "#3");
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

            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [0], await diskManager.GetHashAsync (fileData, 0)), "#1");
            Assert.AreEqual (Piece.BlockSize * 2, writer.ReadData.Sum (t => t.Item3), "#2");

            writer.ReadData.Clear ();
            Assert.IsTrue (Toolbox.ByteMatch (fileData.Hashes [0], await diskManager.GetHashAsync (fileData, 0)), "#3");
            Assert.AreEqual (Piece.BlockSize * 3, writer.ReadData.Sum (t => t.Item3), "#4");
        }

        [Test]
        public async Task WriteRate ()
        {
            var buffer = new byte [Piece.BlockSize];
            settings.MaximumDiskWriteRate = Piece.BlockSize;
            await diskManager.Tick (1000);

            for (int i = 0; i < SpeedMonitor.DefaultAveragePeriod * 2; i ++)
                _ = diskManager.WriteAsync (fileData, 0, buffer, buffer.Length);

            while (diskManager.PendingWrites > 0)
                await diskManager.Tick (1000);

            // We should be writing at about 1 block per second.
            Assert.IsTrue (Math.Abs (Piece.BlockSize - diskManager.WriteRate) < (Piece.BlockSize * 0.1), "#1");
        }
    }
}
