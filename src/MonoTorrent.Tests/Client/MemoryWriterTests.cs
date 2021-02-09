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


using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoTorrent.Client.PieceWriters
{
    public class MemoryWriterTests
    {
        MemoryWriter level1;
        MemoryWriter level2;

        ITorrentFileInfo file;

        [SetUp]
        public void Setup ()
        {
            file = new TorrentFileInfo (new TorrentFile ("Relative/Path.txt", Piece.BlockSize * 5));

            level2 = new MemoryWriter (new NullWriter (), Piece.BlockSize * 3);
            level1 = new MemoryWriter (level2, Piece.BlockSize * 3);
        }

        [Test]
        public async Task FillFirstBuffer ()
        {
            // Write 4 blocks to the stream and then verify they can all be read
            for (int i = 0; i < 4; i++) {
                var buffer = Enumerable.Repeat ((byte) (i + 1), Piece.BlockSize).ToArray ();
                await level1.WriteAsync (file, Piece.BlockSize * i, buffer, 0, buffer.Length);
            }

            Assert.AreEqual (Piece.BlockSize * 3, level1.CacheUsed, "#1");
            Assert.AreEqual (Piece.BlockSize, level2.CacheUsed, "#2");

            // Read them all back out and verify them
            for (int i = 0; i < 4; i++) {
                var buffer = new byte[Piece.BlockSize];
                await level1.ReadAsync (file, Piece.BlockSize * i, buffer, 0, Piece.BlockSize);
                Assert.That (buffer, Is.All.EqualTo ((byte) (i + 1)));
            }
            Assert.AreEqual (Piece.BlockSize * 3, level1.CacheHits, "#3");
            Assert.AreEqual (Piece.BlockSize, level1.CacheMisses, "#4");
            Assert.AreEqual (Piece.BlockSize, level2.CacheHits, "#5");
        }

        [Test]
        public async Task FlushMultipleBlocks()
        {
            byte[] buffer;
            var blocking = new BlockingWriter ();
            var memory = new MemoryWriter (blocking, Piece.BlockSize * 3);

            // Write 3 blocks
            for (int i = 0; i < 3; i++) {
                buffer = Enumerable.Repeat ((byte) (i + 1), Piece.BlockSize).ToArray ();
                await memory.WriteAsync (file, Piece.BlockSize * i, buffer, 0, buffer.Length).WithTimeout ();
            }

            // Flush them all
            var flushTask = memory.FlushAsync (file);

            // Process the first flush
            blocking.Writes.TakeWithTimeout ().tcs.SetResult (null);

            // write a new block
            buffer = Enumerable.Repeat ((byte) 1, Piece.BlockSize).ToArray ();
            await memory.WriteAsync (file, Piece.BlockSize, buffer, 0, buffer.Length).WithTimeout ();

            // Process the remaining two flushes
            blocking.Writes.TakeWithTimeout ().tcs.SetResult (null);
            blocking.Writes.TakeWithTimeout ().tcs.SetResult (null);

            await flushTask.WithTimeout ();
        }

        [Test]
        public async Task WriteBlockWhileFlushing ()
        {
            var blocking = new BlockingWriter ();
            var memory = new MemoryWriter (blocking, Piece.BlockSize * 3);

            await memory.WriteAsync (file, Piece.BlockSize, new byte[Piece.BlockSize], 0, Piece.BlockSize).WithTimeout ();

            // Begin flushing the piece, but write another block to the cache while the flush is in-progress
            var flushTask = memory.FlushAsync (file);
            await memory.WriteAsync (file, Piece.BlockSize, new byte[Piece.BlockSize], 0, Piece.BlockSize).WithTimeout ();
            blocking.Writes.TakeWithTimeout ().tcs.SetResult (null);
            await flushTask.WithTimeout ();

            // At the end we should have one block still in the cache.
            Assert.AreEqual (Piece.BlockSize, memory.CacheUsed);
        }

        [Test]
        public async Task ReadWriteBlock ()
        {
            var buffer = Enumerable.Repeat ((byte) 55, Piece.BlockSize).ToArray ();
            await level1.WriteAsync (file, 0, buffer, 0, buffer.Length);

            buffer = new byte[Piece.BlockSize];
            await level1.ReadAsync (file, 0, buffer, 0, buffer.Length);
            Assert.That (buffer, Is.All.EqualTo (55));
        }

        [Test]
        public async Task ReadWriteBlockChangeOriginal ()
        {
            var buffer = Enumerable.Repeat ((byte) 5, Piece.BlockSize).ToArray ();
            await level1.WriteAsync (file, 0, buffer, 0, buffer.Length);

            buffer = new byte[Piece.BlockSize];
            await level1.ReadAsync (file, 0, buffer, 0, buffer.Length);
            Assert.That (buffer, Is.All.EqualTo ((byte) 5), "#2");
        }

        [Test]
        public async Task MemoryWriter_ZeroCapacity_Write()
        {
            var main = new MemoryWriter (new NullWriter (), Piece.BlockSize);
            var empty = new MemoryWriter (main, 0);
            await empty.WriteAsync (file, 0, new byte[] { 7 }, 0, 1);
            Assert.AreEqual (1, main.CacheUsed);

            var data = new byte[1];
            Assert.AreEqual (1, await empty.ReadAsync (file, 0, data, 0, 1));
            Assert.AreEqual (7, data[0]);
        }
    }
}
