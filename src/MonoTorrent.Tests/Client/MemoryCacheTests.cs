//
// MemoryWriterTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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


using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ReusableTasks;

using NUnit.Framework;
using System;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Client.PieceWriters
{
    class MemoryWriter : IPieceWriter
    {
        public List<ITorrentFileInfo> Closes = new List<ITorrentFileInfo> ();
        public List<ITorrentFileInfo> Exists = new List<ITorrentFileInfo> ();
        public List<ITorrentFileInfo> Flushes = new List<ITorrentFileInfo> ();
        public List<(ITorrentFileInfo file, string fullPath, bool overwrite)> Moves = new List<(ITorrentFileInfo file, string fullPath, bool overwrite)> ();
        public List<(ITorrentFileInfo file, long offset, byte[] buffer)> Reads = new List<(ITorrentFileInfo file, long offset, byte[] buffer)> ();
        public List<(ITorrentFileInfo file, long offset, byte[] buffer)> Writes = new List<(ITorrentFileInfo file, long offset, byte[] buffer)> ();

        public ReusableTask CloseAsync (ITorrentFileInfo file)
        {
            Closes.Add (file);
            return ReusableTask.CompletedTask;
        }

        public void Dispose ()
        {
        }

        public ReusableTask<bool> ExistsAsync (ITorrentFileInfo file)
        {
            Exists.Add (file);
            return ReusableTask.FromResult (false);
        }

        public ReusableTask FlushAsync (ITorrentFileInfo file)
        {
            Flushes.Add (file);
            return ReusableTask.CompletedTask;
        }

        public ReusableTask MoveAsync (ITorrentFileInfo file, string fullPath, bool overwrite)
        {
            Moves.Add ((file, fullPath, overwrite));
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<int> ReadAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            foreach (var write in Writes) {
                if (write.offset == offset && write.file == file && write.buffer.Length == count) {
                    var data = new byte[count];
                    Buffer.BlockCopy (write.buffer, 0, buffer, bufferOffset, count);
                    Buffer.BlockCopy (write.buffer, 0, data, 0, count);
                    Reads.Add ((file, offset, data));
                    return ReusableTask.FromResult (count);
                }
            }
            Reads.Add ((file, offset, null));
            return ReusableTask.FromResult (0);
        }

        public ReusableTask WriteAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            var actualData = new byte[count];
            Buffer.BlockCopy (buffer, bufferOffset, actualData, 0, count);
            Writes.Add ((file, offset, actualData));
            return ReusableTask.CompletedTask;
        }
    }

    class TorrentData : ITorrentData
    {
        public IList<ITorrentFileInfo> Files { get; set; }
        public int PieceLength { get; set; }
        public long Size { get; set; }
    }

    public class MemoryCacheTests
    {
        MemoryCache cache;
        ITorrentData torrent;
        MemoryWriter writer;

        [SetUp]
        public void Setup ()
        {
            var pieceLength = Piece.BlockSize * 8;
            var files = TorrentFileInfo.Create (pieceLength, ("Relative/Path.txt", Piece.BlockSize * 5, "Full/Path/Relative/Path.txt"));
            torrent = new TorrentData {
                Files = files,
                PieceLength = pieceLength,
                Size = files.Single ().Length,
            };

            writer = new MemoryWriter ();
            cache = new MemoryCache (Piece.BlockSize * 4, writer);
        }

        [Test]
        public async Task FillBuffer ()
        {
            // Write 4 blocks to the stream and then verify they can all be read
            for (int i = 0; i < 4; i++) {
                var buffer = Enumerable.Repeat ((byte) (i + 1), Piece.BlockSize).ToArray ();
                await cache.WriteAsync (torrent, new BlockInfo (0, Piece.BlockSize * i, Piece.BlockSize), buffer, false);
            }

            Assert.AreEqual (Piece.BlockSize * 4, cache.CacheUsed, "#1a");
            Assert.AreEqual (0, writer.Writes.Count, "#1b");
            Assert.AreEqual (0, writer.Reads.Count, "#1c");

            // Read them all back out and verify them
            for (int i = 0; i < 4; i++) {
                var buffer = new byte[Piece.BlockSize];
                await cache.ReadAsync (torrent, new BlockInfo (0, Piece.BlockSize * i, Piece.BlockSize), buffer);
                Assert.That (buffer, Is.All.EqualTo ((byte) (i + 1)), "#2." + i);
            }
            Assert.AreEqual (Piece.BlockSize * 4, cache.CacheHits, "#3");
            Assert.AreEqual (0, cache.CacheMisses, "#4");
            Assert.AreEqual (4, writer.Writes.Count, "#5");
        }

        [Test]
        public async Task OverFillBuffer ()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (Piece.BlockSize, writer);

            // Write 4 blocks to the stream and then verify they can all be read
            for (int i = 0; i < 4; i++) {
                var buffer = Enumerable.Repeat ((byte) (i + 1), Piece.BlockSize).ToArray ();
                await cache.WriteAsync (torrent, new BlockInfo (0, Piece.BlockSize * i, Piece.BlockSize), buffer, false);
                Assert.AreEqual (Piece.BlockSize, cache.CacheUsed, "#0");
            }

            Assert.AreEqual (3, writer.Writes.Count, "#1b");
            Assert.AreEqual (0, writer.Reads.Count, "#1c");

            // Read them all back out and verify them
            for (int i = 0; i < 4; i++) {
                var buffer = new byte[Piece.BlockSize];
                await cache.ReadAsync (torrent, new BlockInfo (0, Piece.BlockSize * i, Piece.BlockSize), buffer);
                Assert.That (buffer, Is.All.EqualTo ((byte) (i + 1)), "#2." + i);
            }
            Assert.AreEqual (Piece.BlockSize, cache.CacheHits, "#3");
            Assert.AreEqual (Piece.BlockSize * 3, cache.CacheMisses, "#4");
            Assert.AreEqual (4, writer.Writes.Count, "#5");
            Assert.AreEqual (0, cache.CacheUsed, "#6");
        }

        [Test]
        public async Task WriteSameBlockTwice ()
        {
            var data1 = new byte[] { 1, 1, 1 };
            var data2 = new byte[] { 2, 2, 2 };
            var data3 = new byte[] { 3, 3, 3 };

            var memory = new MemoryCache (1024, new NullWriter ());
            await memory.WriteAsync (torrent, new BlockInfo (0, 0, 3), data1, false);
            await memory.WriteAsync (torrent, new BlockInfo (0, 3, 3), data2, false);
            await memory.WriteAsync (torrent, new BlockInfo (0, 3, 3), data3, false);

            Assert.AreEqual (6, memory.CacheUsed);

            var result = new byte[3];
            Assert.AreEqual (3, await memory.ReadAsync (torrent, new BlockInfo (0, 0, 3), result));
            CollectionAssert.AreEqual (data1, result);
            Assert.AreEqual (3, memory.CacheUsed);

            Assert.AreEqual (3, await memory.ReadAsync (torrent, new BlockInfo (0, 3, 3), result));
            CollectionAssert.AreEqual (data3, result);
            Assert.AreEqual (0, memory.CacheUsed);
        }

        [Test]
        public async Task WritePrefersSkippingCache ()
        {
            var data = new byte[] { 1, 1, 1 };

            var writer = new MemoryWriter ();
            var memory = new MemoryCache (1024, writer);
            await memory.WriteAsync (torrent, new BlockInfo (0, 0, 3), data, true);
            Assert.AreEqual (0, memory.CacheUsed);
            Assert.AreEqual (torrent.Files[0], writer.Writes.Single ().file);
            CollectionAssert.AreEqual (data, writer.Writes.Single ().buffer);
        }

        [Test]
        public async Task ReadWriteBlock ()
        {
            var buffer = Enumerable.Repeat ((byte) 55, Piece.BlockSize).ToArray ();
            await cache.WriteAsync (torrent, new BlockInfo (0, 0, Piece.BlockSize), buffer, false);

            buffer = new byte[Piece.BlockSize];
            await cache.ReadAsync (torrent, new BlockInfo (0, 0, Piece.BlockSize), buffer);
            Assert.That (buffer, Is.All.EqualTo (55));
        }

        [Test]
        public async Task ReadWriteBlockChangeOriginal ()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (Piece.BlockSize, writer);

            var buffer = Enumerable.Repeat ((byte) 5, Piece.BlockSize).ToArray ();
            await cache.WriteAsync (torrent, new BlockInfo (0, 0, Piece.BlockSize), buffer, false);
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0;

            buffer = new byte[Piece.BlockSize];
            await cache.ReadAsync (torrent, new BlockInfo (0, 0, Piece.BlockSize), buffer);
            Assert.That (buffer, Is.All.EqualTo ((byte) 5), "#2");
            Assert.That (writer.Writes.Single ().buffer, Is.All.EqualTo ((byte) 5), "#3");
        }

        [Test]
        public async Task MemoryWriter_ZeroCapacity_Write()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (0, writer);

            await cache.WriteAsync (torrent, new BlockInfo (0, 0, 1), new byte[] { 7 }, false);
            Assert.AreEqual (1, writer.Writes.Count);
            Assert.AreEqual (0, cache.CacheUsed);

            var data = new byte[1];
            Assert.AreEqual (1, await cache.ReadAsync (torrent, new BlockInfo (0, 0, 1), data));
            Assert.AreEqual (7, data[0]);
        }
    }
}
