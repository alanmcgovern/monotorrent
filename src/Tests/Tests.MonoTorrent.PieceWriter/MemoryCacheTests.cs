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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.Client;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.PieceWriter
{
    class MemoryWriter : IPieceWriter
    {
        public List<ITorrentManagerFile> Closes = new List<ITorrentManagerFile> ();
        public List<ITorrentManagerFile> Exists = new List<ITorrentManagerFile> ();
        public List<ITorrentManagerFile> Flushes = new List<ITorrentManagerFile> ();
        public List<(ITorrentManagerFile file, string fullPath, bool overwrite)> Moves = new List<(ITorrentManagerFile file, string fullPath, bool overwrite)> ();
        public List<(ITorrentManagerFile file, long offset, byte[] buffer)> Reads = new List<(ITorrentManagerFile file, long offset, byte[] buffer)> ();
        public List<(ITorrentManagerFile file, long offset, byte[] buffer)> Writes = new List<(ITorrentManagerFile file, long offset, byte[] buffer)> ();

        public int OpenFiles { get; }
        public int MaximumOpenFiles { get; }

        public ReusableTask CloseAsync (ITorrentManagerFile file)
        {
            Closes.Add (file);
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions options)
        {
            throw new NotImplementedException ();
        }

        public void Dispose ()
        {
        }

        public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
        {
            Exists.Add (file);
            return ReusableTask.FromResult (false);
        }

        public ReusableTask FlushAsync (ITorrentManagerFile file)
        {
            Flushes.Add (file);
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file)
        {
            throw new NotSupportedException ();
        }

        public ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite)
        {
            Moves.Add ((file, fullPath, overwrite));
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
        {
            foreach (var write in Writes) {
                if (write.offset == offset && write.file == file && write.buffer.Length == buffer.Length) {
                    var data = new byte[buffer.Length];
                    write.buffer.CopyTo (buffer);
                    write.buffer.CopyTo (data.AsMemory ());
                    Reads.Add ((file, offset, data));
                    return ReusableTask.FromResult (buffer.Length);
                }
            }
            Reads.Add ((file, offset, null));
            return ReusableTask.FromResult (0);
        }

        public ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length)
        {
            throw new NotSupportedException ();
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            return ReusableTask.CompletedTask;
        }

        public virtual ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            var actualData = new byte[buffer.Length];
            buffer.CopyTo (actualData.AsMemory ());
            Writes.Add ((file, offset, actualData));
            return ReusableTask.CompletedTask;
        }
    }

    class BlockingMemoryWriter : MemoryWriter
    {
        public new List<ReusableTaskCompletionSource<object>> Writes { get; } = new List<ReusableTaskCompletionSource<object>> ();

        public override async ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Writes.Add (tcs);
            await tcs.Task;
        }
    }

    public class MemoryCacheTests
    {
        ITorrentManagerInfo torrent;

        [SetUp]
        public void Setup ()
        {
            var pieceLength = Constants.BlockSize * 8;
            var files = TorrentFileInfo.Create (pieceLength, ("Relative/Path.txt", Constants.BlockSize * 5, "Full/Path/Relative/Path.txt"));
            torrent = TestTorrentManagerInfo.Create (pieceLength: pieceLength, size: files.Single ().Length, files: files);
        }

        [Test]
        public async Task FillBuffer ()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (new MemoryPool (), Constants.BlockSize * 4, CachePolicy.WritesOnly, writer);
            // Write 4 blocks to the stream and then verify they can all be read
            for (int i = 0; i < 4; i++) {
                var buffer = Enumerable.Repeat ((byte) (i + 1), Constants.BlockSize).ToArray ();
                await cache.WriteAsync (torrent, new BlockInfo (0, Constants.BlockSize * i, Constants.BlockSize), buffer, false);
            }

            Assert.AreEqual (Constants.BlockSize * 4, cache.CacheUsed, "#1a");
            Assert.AreEqual (0, writer.Writes.Count, "#1b");
            Assert.AreEqual (0, writer.Reads.Count, "#1c");

            // Read them all back out and verify them
            for (int i = 0; i < 4; i++) {
                var buffer = new byte[Constants.BlockSize];
                await cache.ReadAsync (torrent, new BlockInfo (0, Constants.BlockSize * i, Constants.BlockSize), buffer);
                Assert.IsTrue (buffer.All (t => t == (byte) (i + 1)), "#2." + i);
            }
            Assert.AreEqual (Constants.BlockSize * 4, cache.CacheHits, "#3");
            Assert.AreEqual (0, cache.CacheMisses, "#4");
            Assert.AreEqual (4, writer.Writes.Count, "#5");
        }

        [Test]
        public async Task OverFillBuffer ()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (new MemoryPool (), Constants.BlockSize, CachePolicy.WritesOnly, writer);

            // Write 4 blocks to the stream and then verify they can all be read
            for (int i = 0; i < 4; i++) {
                var buffer = Enumerable.Repeat ((byte) (i + 1), Constants.BlockSize).ToArray ();
                await cache.WriteAsync (torrent, new BlockInfo (0, Constants.BlockSize * i, Constants.BlockSize), buffer, false);
                Assert.AreEqual (Constants.BlockSize, cache.CacheUsed, "#0");
            }

            Assert.AreEqual (3, writer.Writes.Count, "#1b");
            Assert.AreEqual (0, writer.Reads.Count, "#1c");

            // Read them all back out and verify them
            for (int i = 0; i < 4; i++) {
                var buffer = new byte[Constants.BlockSize];
                await cache.ReadAsync (torrent, new BlockInfo (0, Constants.BlockSize * i, Constants.BlockSize), buffer);
                Assert.IsTrue (buffer.All (t => t == (byte) (i + 1)), "#2." + i);
            }
            Assert.AreEqual (Constants.BlockSize, cache.CacheHits, "#3");
            Assert.AreEqual (Constants.BlockSize * 3, cache.CacheMisses, "#4");
            Assert.AreEqual (4, writer.Writes.Count, "#5");
            Assert.AreEqual (0, cache.CacheUsed, "#6");
        }

        [Test]
        public async Task OverFillBuffer_CacheUsed ()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (new MemoryPool (), 4, CachePolicy.WritesOnly, writer);

            await cache.WriteAsync (torrent, new BlockInfo (0, 0, 3), Enumerable.Repeat ((byte) 1, 3).ToArray (), false);
            await cache.WriteAsync (torrent, new BlockInfo (0, 3, 4), Enumerable.Repeat ((byte) 2, 4).ToArray (), false);

            Assert.AreEqual (4, cache.CacheUsed);
        }

        [Test]
        public async Task ReadBlockWhileWriting ()
        {
            var writer = new BlockingMemoryWriter ();
            var cache = new MemoryCache (new MemoryPool (), 3, CachePolicy.WritesOnly, writer);
            await cache.WriteAsync (torrent, new BlockInfo (0, 0, 3), Enumerable.Repeat ((byte) 1, 3).ToArray (), false).WithTimeout ();

            var write = cache.WriteAsync (torrent, new BlockInfo (0, 3, 3), Enumerable.Repeat ((byte) 2, 3).ToArray (), false);
            Assert.IsFalse (write.IsCompleted);
            Assert.AreEqual (1, writer.Writes.Count);
            Assert.AreEqual (3, cache.CacheUsed);

            // verify the original block is still accessible. Note: Reading a block implicitly flushes it, but we skip the
            // flush as the block was already marked as flushing by the 'Write' invocation.
            var result = new byte[3];
            Assert.IsTrue (await cache.ReadAsync (torrent, new BlockInfo (0, 0, 3), result).WithTimeout ());
            Assert.AreEqual (1, writer.Writes.Count);
            Assert.AreEqual (3, cache.CacheHits);

            CollectionAssert.AreEqual (Enumerable.Repeat ((byte) 1, 3).ToArray (), result);
            Assert.IsFalse (write.IsCompleted);

            writer.Writes[0].SetResult (null);
            await write;
            Assert.AreEqual (3, cache.CacheUsed);
        }

        [Test]
        public async Task ReadCacheEnabled ()
        {
            var dest = new byte[Constants.BlockSize];
            var source = new byte[Constants.BlockSize];
            source.AsSpan ().Fill (1);

            var writer = new MemoryWriter ();
            var cache = new MemoryCache (new MemoryPool (), 4, CachePolicy.WritesOnly, writer);

            writer.Writes.Add ((torrent.Files[0], 0, source));

            Assert.IsTrue (await cache.ReadAsync (torrent, new BlockInfo (0, 0, Constants.BlockSize), dest));
            Assert.AreEqual (0, cache.CacheUsed);
            Assert.IsTrue (source.AsSpan ().SequenceEqual (dest.AsSpan ()));

            dest.AsSpan ().Fill (0);
            await cache.SetPolicyAsync (CachePolicy.ReadsAndWrites);
            Assert.IsTrue (await cache.ReadAsync (torrent, new BlockInfo (0, 0, Constants.BlockSize), dest));
            Assert.AreEqual (Constants.BlockSize, cache.CacheUsed);
            Assert.IsTrue (source.AsSpan ().SequenceEqual (dest.AsSpan ()));

            Assert.IsTrue (await cache.ReadAsync (torrent, new BlockInfo (0, 0, Constants.BlockSize), dest));
            Assert.AreEqual (0, cache.CacheUsed);
            Assert.IsTrue (source.AsSpan ().SequenceEqual (dest.AsSpan ()));

            Assert.AreEqual (0, cache.CacheUsed);
            Assert.AreEqual (1, writer.Writes.Count);
        }

        [Test]
        public async Task WriteSameBlockDifferentTorrents ()
        {
            var data1 = new byte[] { 1, 1, 1 };
            var data2 = new byte[] { 2, 2, 2 };

            var torrent2 = TestTorrentManagerInfo.Create (
                files: torrent.TorrentInfo.Files,
                pieceLength: torrent.TorrentInfo.PieceLength,
                size: torrent.TorrentInfo.Size
            );

            var memory = new MemoryCache (new MemoryPool (), 1024, CachePolicy.WritesOnly, new NullWriter ());
            await memory.WriteAsync (torrent, new BlockInfo (0, 0, 3), data1, false);
            await memory.WriteAsync (torrent2, new BlockInfo (0, 0, 3), data2, false);

            var readBuffer = new byte[3];
            await memory.ReadAsync (torrent, new BlockInfo (0, 0, 3), readBuffer);
            CollectionAssert.AreEqual (data1, readBuffer);

            await memory.ReadAsync (torrent2, new BlockInfo (0, 0, 3), readBuffer);
            CollectionAssert.AreEqual (data2, readBuffer);
        }

        [Test]
        public async Task WriteSameBlockTwice ()
        {
            var data1 = new byte[] { 1, 1, 1 };
            var data2 = new byte[] { 2, 2, 2 };
            var data3 = new byte[] { 3, 3, 3 };

            var memory = new MemoryCache (new MemoryPool (), 1024, CachePolicy.WritesOnly, new NullWriter ());
            await memory.WriteAsync (torrent, new BlockInfo (0, 0, 3), data1, false);
            await memory.WriteAsync (torrent, new BlockInfo (0, 3, 3), data2, false);
            await memory.WriteAsync (torrent, new BlockInfo (0, 3, 3), data3, false);

            Assert.AreEqual (6, memory.CacheUsed);

            var result = new byte[3];
            Assert.IsTrue (await memory.ReadAsync (torrent, new BlockInfo (0, 0, 3), result));
            CollectionAssert.AreEqual (data1, result);
            Assert.AreEqual (3, memory.CacheUsed);

            Assert.IsTrue (await memory.ReadAsync (torrent, new BlockInfo (0, 3, 3), result));
            CollectionAssert.AreEqual (data3, result);
            Assert.AreEqual (0, memory.CacheUsed);
        }

        [Test]
        public async Task WritePrefersSkippingCache ()
        {
            var data = new byte[] { 1, 1, 1 };

            var writer = new MemoryWriter ();
            var memory = new MemoryCache (new MemoryPool (), 1024, CachePolicy.WritesOnly, writer);
            await memory.WriteAsync (torrent, new BlockInfo (0, 0, 3), data, true);
            Assert.AreEqual (0, memory.CacheUsed);
            Assert.AreEqual (torrent.Files[0], writer.Writes.Single ().file);
            CollectionAssert.AreEqual (data, writer.Writes.Single ().buffer);
        }

        [Test]
        public async Task ReadWriteBlock ()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (new MemoryPool (), Constants.BlockSize * 4, CachePolicy.WritesOnly, writer);

            var buffer = Enumerable.Repeat ((byte) 55, Constants.BlockSize).ToArray ();
            await cache.WriteAsync (torrent, new BlockInfo (0, 0, Constants.BlockSize), buffer, false);

            buffer = new byte[Constants.BlockSize];
            await cache.ReadAsync (torrent, new BlockInfo (0, 0, Constants.BlockSize), buffer);
            Assert.IsTrue (buffer.All (t => t == 55));
        }

        [Test]
        public async Task ReadWriteBlockChangeOriginal ()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (new MemoryPool (), Constants.BlockSize, CachePolicy.WritesOnly, writer);

            var buffer = Enumerable.Repeat ((byte) 5, Constants.BlockSize).ToArray ();
            await cache.WriteAsync (torrent, new BlockInfo (0, 0, Constants.BlockSize), buffer, false);
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = 0;

            buffer = new byte[Constants.BlockSize];
            await cache.ReadAsync (torrent, new BlockInfo (0, 0, Constants.BlockSize), buffer);
            Assert.IsTrue (buffer.All (t => t == 5), "#2");
            Assert.IsTrue (writer.Writes.Single ().buffer.All (t => t == 5), "#3");
        }

        [Test]
        public async Task MemoryWriter_ZeroCapacity_Write ()
        {
            var writer = new MemoryWriter ();
            var cache = new MemoryCache (new MemoryPool (), 0, CachePolicy.WritesOnly, writer);

            await cache.WriteAsync (torrent, new BlockInfo (0, 0, 1), new byte[] { 7 }, false);
            Assert.AreEqual (1, writer.Writes.Count);
            Assert.AreEqual (0, cache.CacheUsed);

            var data = new byte[1];
            Assert.IsTrue (await cache.ReadAsync (torrent, new BlockInfo (0, 0, 1), data));
            Assert.AreEqual (7, data[0]);
        }
    }
}
