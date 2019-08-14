//
// DiskWriterTests.cs
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
using System.Threading.Tasks;

using MonoTorrent.Client.PieceWriters;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    public class ExceptionWriter : PieceWriter
    {
        public bool exist, close, flush, move, read, write;

        public override bool Exists(TorrentFile file)
        {
            if (exist)
                throw new Exception("exists");
            return true;
        }

        public override void Close(TorrentFile file)
        {
            if (close)
                throw new Exception("close");
        }

        public override void Flush(TorrentFile file)
        {
            if (flush)
                throw new Exception("flush");
        }

        public override void Move(TorrentFile file, string newPath, bool overwrite)
        {
            if (move)
                throw new Exception("move");
        }

        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (read)
                throw new Exception("read");
            return count;
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (write)
                throw new Exception("write");
        }
    }

    [TestFixture]
    public class DiskWriterTests
    {
        byte [] data = new byte [Piece.BlockSize];
        DiskManager diskManager;
        TestRig rig;
        ExceptionWriter writer;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile();
            diskManager = rig.Engine.DiskManager;
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public async Task Setup()
        {
            writer = new ExceptionWriter();
            diskManager.Writer = writer;
            await rig.Manager.StopAsync();
        }

        [Test]
        public async Task CloseFail()
        {
            writer.close = true;
            await diskManager.CloseFilesAsync (rig.Manager);
            Assert.AreEqual (TorrentState.Error, rig.Manager.State);
        }

        [Test]
        public async Task FlushFail()
        {
            writer.flush = true;
            await diskManager.FlushAsync(rig.Manager, 0);
            Assert.AreEqual (TorrentState.Error, rig.Manager.State);
        }

        [Test]
        public async Task MoveFail()
        {
            writer.move = true;
            await diskManager.MoveFilesAsync(rig.Manager, "root", true);
            Assert.AreEqual (TorrentState.Error, rig.Manager.State);
        }

        [Test]
        public void ReadFail()
        {
            writer.read = true;
            diskManager.ReadAsync (rig.Manager, 0, data, data.Length).Wait ();
            Assert.AreEqual (TorrentState.Error, rig.Manager.State);
        }

        [Test]
        public async Task WriteFail()
        {
            writer.write = true;
            await diskManager.WriteAsync(rig.Manager, 0, data, data.Length);
            Assert.AreEqual (TorrentState.Error, rig.Manager.State);
        }
    }
}
