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
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Common;
using NUnit.Framework;
using System.IO;
using System.Threading;

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

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
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
        TestRig rig;
        ExceptionWriter writer;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            writer = new ExceptionWriter();
            rig = TestRig.CreateMultiFile();
            rig.Engine.DiskManager.Writer = writer;
        }

        [SetUp]
        public void Setup()
        {
            rig.Manager.Stop();
        }

        [TestFixtureTearDown]
        public void Teardown()
        {
            rig.Dispose();
        }

        [Test]
        public void CloseFail()
        {
            ManualResetEvent handle = new ManualResetEvent(false);
            rig.Manager.TorrentStateChanged += delegate
            {
                if (rig.Manager.State == TorrentState.Error)
                    handle.Set();
            };
            writer.close = true;

            rig.Manager.HashCheck (true);
            Assert.IsTrue(handle.WaitOne(50000, true), "Failure was not handled");
        }

        [Test]
        public void ExistFail()
        {
            ManualResetEvent handle = new ManualResetEvent(false);
            rig.Manager.TorrentStateChanged += delegate {
                if (rig.Manager.State == TorrentState.Error)
                    handle.Set();
            };
            writer.exist = true;
            
            rig.Manager.HashCheck(false);
            Assert.IsTrue(handle.WaitOne(50000, true), "Failure was not handled");
        }

        [Test]
        public void ReadFail()
        {
            ManualResetEvent handle = new ManualResetEvent(false);
            rig.Manager.TorrentStateChanged += delegate {
                if (rig.Manager.State == TorrentState.Error)
                    handle.Set();
            };
            writer.read = true;
            
            rig.Manager.HashCheck(false);
            Assert.IsTrue(handle.WaitOne(50000, true), "Failure was not handled");
        }

        [Test]
        public void WriteFail()
        {
            ManualResetEvent handle = new ManualResetEvent(false);
            rig.Manager.TorrentStateChanged += delegate {
                if (rig.Manager.State == TorrentState.Error)
                    handle.Set();
            };
            writer.write = true;

            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[Piece.BlockSize]);
			BufferedIO io = new BufferedIO();
            io.Initialise (rig.Manager, buffer, 0, Piece.BlockSize, Piece.BlockSize * 4, rig.Torrent.Files);
            io.Id = new PeerId(new Peer("", new Uri("tcp://123.123.123")), rig.Manager);
            rig.Engine.DiskManager.QueueWrite(io, null);

            Assert.IsTrue(handle.WaitOne(5000, true), "Failure was not handled");
        }
    }
}
