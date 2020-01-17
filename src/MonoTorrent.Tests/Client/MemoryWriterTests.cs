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

using NUnit.Framework;

namespace MonoTorrent.Client.PieceWriters
{
    public class MemoryWriterTests
    {
        MemoryWriter level1;
        MemoryWriter level2;

        TorrentFile file;

        [SetUp]
        public void Setup ()
        {
            file = new TorrentFile ("Relative/Path.txt", Piece.BlockSize * 5);

            level2 = new MemoryWriter (new NullWriter (), Piece.BlockSize * 3);
            level1 = new MemoryWriter (level2, Piece.BlockSize * 3);
        }

        [Test]
        public void FillFirstBuffer ()
        {
            // Write 4 blocks to the stream and then verify they can all be read
            for (int i = 0; i < 4; i++) {
                var buffer = Enumerable.Repeat ((byte) (i + 1), Piece.BlockSize).ToArray ();
                level1.Write (file, Piece.BlockSize * i, buffer, 0, buffer.Length);
            }

            Assert.AreEqual (Piece.BlockSize * 3, level1.CacheUsed, "#1");
            Assert.AreEqual (Piece.BlockSize, level2.CacheUsed, "#2");

            // Read them all back out and verify them
            for (int i = 0; i < 4; i++) {
                var buffer = new byte[Piece.BlockSize];
                level1.Read (file, Piece.BlockSize * i, buffer, 0, Piece.BlockSize);
                Assert.That (buffer, Is.All.EqualTo ((byte) (i + 1)));
            }
            Assert.AreEqual (Piece.BlockSize * 3, level1.CacheHits, "#3");
            Assert.AreEqual (Piece.BlockSize, level1.CacheMisses, "#4");
            Assert.AreEqual (Piece.BlockSize, level2.CacheHits, "#5");
        }

        [Test]
        public void ReadWriteBlock ()
        {
            var buffer = Enumerable.Repeat ((byte) 55, Piece.BlockSize).ToArray ();
            level1.Write (file, 0, buffer, 0, buffer.Length);

            buffer = new byte[Piece.BlockSize];
            level1.Read (file, 0, buffer, 0, buffer.Length);
            Assert.That (buffer, Is.All.EqualTo (55));
        }

        [Test]
        public void ReadWriteBlockChangeOriginal ()
        {
            var buffer = Enumerable.Repeat ((byte) 5, Piece.BlockSize).ToArray ();
            level1.Write (file, 0, buffer, 0, buffer.Length);

            buffer = new byte[Piece.BlockSize];
            level1.Read (file, 0, buffer, 0, buffer.Length);
            Assert.That (buffer, Is.All.EqualTo ((byte) 5), "#2");
        }
    }
}
