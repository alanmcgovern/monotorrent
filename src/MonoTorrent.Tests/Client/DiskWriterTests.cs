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
using Xunit;
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


    public class DiskWriterTests : IDisposable
    {
        byte[] data = new byte[Piece.BlockSize];
        DiskManager diskManager;
        ManualResetEvent handle;
        TestRig rig;
        ExceptionWriter writer;

        public DiskWriterTests()
        {
            rig = TestRig.CreateMultiFile();
            diskManager = rig.Engine.DiskManager;

            writer = new ExceptionWriter();
            diskManager.Writer = writer;
            handle = new ManualResetEvent(false);
            rig.Manager.Stop();
        }

        public void Dispose()
        {
            handle.Close();
            rig.Dispose();
        }

        [Fact]
        public void CloseFail()
        {
            writer.close = true;
            Hookup();
            diskManager.CloseFileStreams(rig.Manager);
            CheckFail();
        }

        [Fact]
        public void FlushFail()
        {
            writer.flush = true;
            Hookup();
            diskManager.QueueFlush(rig.Manager, 0);
            CheckFail();
        }

        [Fact]
        public void MoveFail()
        {
            writer.move = true;
            Hookup();
            diskManager.MoveFiles(rig.Manager, "root", true);
            CheckFail();
        }

        [Fact]
        public void ReadFail()
        {
            bool called = false;
            writer.read = true;
            Hookup();
            diskManager.QueueRead(rig.Manager, 0, data, data.Length, delegate { called = true; });
            CheckFail();
            Assert.True(called, "#delegate called");
        }

        [Fact]
        public void WriteFail()
        {
            bool called = false;
            writer.write = true;
            Hookup();
            diskManager.QueueWrite(rig.Manager, 0, data, data.Length, delegate { called = true; });
            CheckFail();
            Assert.True(called, "#delegate called");
        }

        void Hookup()
        {
            rig.Manager.TorrentStateChanged += delegate
            {
                if (rig.Manager.State == TorrentState.Error)
                    handle.Set();
            };
        }

        void CheckFail()
        {
            Assert.True(handle.WaitOne(5000, true), "Failure was not handled");
        }
    }
}