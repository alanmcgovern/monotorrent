using System;
using System.Threading;
using MonoTorrent.Client;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Tests.Client
{
    public class DiskWriterTests : IDisposable
    {
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

        private readonly byte[] data = new byte[Piece.BlockSize];
        private readonly DiskManager diskManager;
        private readonly ManualResetEvent handle;
        private readonly TestRig rig;
        private readonly ExceptionWriter writer;

        private void Hookup()
        {
            rig.Manager.TorrentStateChanged += delegate
            {
                if (rig.Manager.State == TorrentState.Error)
                    handle.Set();
            };
        }

        private void CheckFail()
        {
            Assert.True(handle.WaitOne(5000, true), "Failure was not handled");
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
            var called = false;
            writer.read = true;
            Hookup();
            diskManager.QueueRead(rig.Manager, 0, data, data.Length, delegate { called = true; });
            CheckFail();
            Assert.True(called, "#delegate called");
        }

        [Fact]
        public void WriteFail()
        {
            var called = false;
            writer.write = true;
            Hookup();
            diskManager.QueueWrite(rig.Manager, 0, data, data.Length, delegate { called = true; });
            CheckFail();
            Assert.True(called, "#delegate called");
        }
    }
}