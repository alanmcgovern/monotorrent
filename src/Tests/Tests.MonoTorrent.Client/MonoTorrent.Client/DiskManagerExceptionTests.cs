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

using MonoTorrent.PieceWriter;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class DiskManagerExceptionTests
    {
        public class ExceptionWriter : IPieceWriter
        {
            public bool exist, close, flush, move, read, write;

            public List<ITorrentManagerFile> FlushedFiles = new List<ITorrentManagerFile> ();

            public int OpenFiles => 0;
            public int MaximumOpenFiles { get; }

            public ReusableTask CloseAsync (ITorrentManagerFile file)
            {
                if (close)
                    throw new Exception ("close");
                return ReusableTask.CompletedTask;
            }

            public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
            {
                if (exist)
                    throw new Exception ("exists");
                return ReusableTask.FromResult (true);
            }

            public void Dispose ()
            {

            }

            public ReusableTask FlushAsync (ITorrentManagerFile file)
            {
                if (flush)
                    throw new Exception ("flush");
                FlushedFiles.Add (file);
                return ReusableTask.CompletedTask;
            }

            public ReusableTask MoveAsync (ITorrentManagerFile file, string newPath, bool overwrite)
            {
                if (move)
                    throw new Exception ("move");
                return ReusableTask.CompletedTask;
            }

            public ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
            {
                if (read)
                    throw new Exception ("read");
                return ReusableTask.FromResult (buffer.Length);
            }

            public ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
            {
                if (write)
                    throw new Exception ("write");
                return ReusableTask.CompletedTask;
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

        byte[] buffer;
        ITorrentManagerInfo data;
        DiskManager diskManager;
        ExceptionWriter writer;

        [SetUp]
        public void Setup ()
        {
            var pieceLength = Constants.BlockSize * 2;
            var files = TorrentFileInfo.Create (pieceLength,
                Constants.BlockSize / 2,
                Constants.BlockSize,
                Constants.BlockSize + Constants.BlockSize / 2,
                Constants.BlockSize * 2 + Constants.BlockSize / 2
            );

            buffer = new byte[Constants.BlockSize];
            data = TestTorrentManagerInfo.Create (
                files: files,
                size: files.Sum (f => f.Length),
                pieceLength: pieceLength
            );

            writer = new ExceptionWriter ();
            diskManager = new DiskManager (new EngineSettings (), Factories.Default, writer);
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
            Assert.ThrowsAsync<Exception> (() => diskManager.MoveFileAsync ((TorrentFileInfo) data.Files[0], ("root", "bar", "baz")));
        }

        [Test]
        public void MoveFilesFail ()
        {
            writer.move = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.MoveFilesAsync (data.Files, "root", true));
        }

        [Test]
        public void ReadFail ()
        {
            writer.read = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.ReadAsync (data, new BlockInfo (0, 0, Constants.BlockSize), buffer).AsTask ());
        }

        [Test]
        public void WriteFail ()
        {
            writer.write = true;
            Assert.ThrowsAsync<Exception> (() => diskManager.WriteAsync (data, new BlockInfo (0, 0, Constants.BlockSize), buffer).AsTask ());
        }
    }
}
