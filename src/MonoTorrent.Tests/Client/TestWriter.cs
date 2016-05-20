using System.Collections.Generic;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Common;

namespace MonoTorrent.Tests.Client
{
    public class TestWriter : PieceWriter
    {
        public List<TorrentFile> DoNotReadFrom = new List<TorrentFile>();
        public bool DontWrite;
        public List<TorrentFile> FilesThatExist = new List<TorrentFile>();
        public List<string> Paths = new List<string>();

        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (DoNotReadFrom.Contains(file))
                return 0;

            if (!Paths.Contains(file.FullPath))
                Paths.Add(file.FullPath);

            if (!DontWrite)
                for (var i = 0; i < count; i++)
                    buffer[bufferOffset + i] = (byte) (bufferOffset + i);
            return count;
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
        }

        public override void Close(TorrentFile file)
        {
        }

        public override void Flush(TorrentFile file)
        {
        }

        public override bool Exists(TorrentFile file)
        {
            return FilesThatExist.Contains(file);
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
        }
    }
}