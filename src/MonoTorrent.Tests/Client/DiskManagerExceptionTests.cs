//
// DiskManagerExceptionTests.cs
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

using MonoTorrent.Client.PieceWriters;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class DiskManagerExceptionTests
    {
        public class ExceptionWriter : IPieceWriter
        {
            public bool exist, close, flush, move, read, write;

            public List<TorrentFile> FlushedFiles = new List<TorrentFile> ();

            public void Close (TorrentFile file)
            {
                if (close)
                    throw new Exception ("close");
            }

            public bool Exists (TorrentFile file)
            {
                if (exist)
                    throw new Exception ("exists");
                return true;
            }

            public void Dispose ()
            {

            }

            public void Flush (TorrentFile file)
            {
                if (flush)
                    throw new Exception ("flush");
                FlushedFiles.Add (file);
            }

            public void Move (TorrentFile file, string newPath, bool overwrite)
            {
                if (move)
                    throw new Exception ("move");
            }

            public int Read (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
            {
                if (read)
                    throw new Exception ("read");
                return count;
            }

            public void Write (TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
            {
                if (write)
                    throw new Exception ("write");
            }
        }

        class TestTorrentData : ITorrentData
        {
            public TorrentFile[] Files { get; set; }
            public int PieceLength { get; set; }
            public long Size { get; set; }
        }

        byte[] buffer;
        TestTorrentData data;
        DiskManager diskManager;
        ExceptionWriter writer;

        [SetUp]
        public void Setup ()
        {
            var files = new[] {
                new TorrentFile ("First",  Piece.BlockSize / 2),
                new TorrentFile ("Second", Piece.BlockSize),
                new TorrentFile ("Third",  Piece.BlockSize + Piece.BlockSize / 2),
                new TorrentFile ("Fourth", Piece.BlockSize * 2 + Piece.BlockSize / 2),
            };

            buffer = new byte[Piece.BlockSize];
            data = new TestTorrentData {
                Files = files,
                Size = files.Sum (f => f.Length),
                PieceLength = Piece.BlockSize * 2
            };

            writer = new ExceptionWriter ();
            diskManager = new DiskManager (new EngineSettings (), writer);
        }

        [Test]
        public void CheckAnyFilesExistsFail ()
        {
            writer.exist = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.CheckAnyFilesExistAsync (data));
        }

        [Test]
        public void CheckFileExistsFail ()
        {
            writer.exist = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.CheckFileExistsAsync (data.Files[0]));
        }

        [Test]
        public void CloseFail ()
        {
            writer.close = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.CloseFilesAsync (data));
        }

        [Test]
        public void MoveFileFail ()
        {
            writer.move = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.MoveFileAsync (data.Files[0], "root"));
        }

        [Test]
        public void MoveFilesFail ()
        {
            writer.move = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.MoveFilesAsync (data, "root", true));
        }

        [Test]
        public void ReadFail ()
        {
            writer.read = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.ReadAsync (data, 0, buffer, buffer.Length).AsTask ());
        }

        [Test]
        public void WriteFail ()
        {
            writer.write = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.WriteAsync (data, 0, buffer, buffer.Length).AsTask ());
        }
    }
}
