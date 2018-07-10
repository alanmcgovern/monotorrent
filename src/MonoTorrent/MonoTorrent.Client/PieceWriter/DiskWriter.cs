using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MonoTorrent.Common;
using System.IO;
using System.Threading;

namespace MonoTorrent.Client.PieceWriters
{
    public class DiskWriter : PieceWriter
    {
        private Stopwatch _stopwatch = new Stopwatch();

        private FileStreamBuffer streamsBuffer;

        public int OpenFiles
        {
            get { return streamsBuffer.Count; }
        }

        public DiskWriter()
            : this(10)
        {

        }

        public DiskWriter(int maxOpenFiles)
        {
            this.streamsBuffer = new FileStreamBuffer(maxOpenFiles);
        }

        public override void Close(TorrentFile file)
        {
            streamsBuffer.CloseStream(file.FullPath);
        }

        public override void Dispose()
        {
            streamsBuffer.Dispose();
            base.Dispose();
        }

        internal TorrentFileStream GetStream(TorrentFile file, FileAccess access)
        {
            return streamsBuffer.GetStream(file, access);
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
            streamsBuffer.CloseStream(oldPath);
            if (ignoreExisting)
                File.Delete(newPath);
            File.Move(oldPath, newPath);
        }

        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File(file);
            Check.Buffer(buffer);

            if (offset < 0 || offset + count > file.Length)
                throw new ArgumentOutOfRangeException("offset");

            Stream s = GetStream(file, FileAccess.Read);
            if (s.Length < offset + count)
                return 0;
            s.Seek(offset, SeekOrigin.Begin);
            return s.Read(buffer, bufferOffset, count);
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File(file);
            Check.Buffer(buffer);

            if (offset < 0 || offset + count > file.Length)
                throw new ArgumentOutOfRangeException("offset");

            TorrentFileStream stream = GetStream(file, FileAccess.ReadWrite);
            
            _stopwatch.Reset();
            _stopwatch.Start();

            stream.Seek(offset, SeekOrigin.Begin);
            stream.Write(buffer, bufferOffset, count);

            if (_stopwatch.ElapsedMilliseconds > 1000)
                Logger.Log(null, "Slow write time: {0}ms pos: {1} len: {2}", _stopwatch.ElapsedMilliseconds, offset, count);
            
        }

        public override bool Exists(TorrentFile file)
        {
            return File.Exists(file.FullPath);
        }

        public override void Flush(TorrentFile file)
        {
            Stream s = streamsBuffer.FindStream(file.FullPath);
            if (s != null)
                s.Flush();
        }
    }
}
