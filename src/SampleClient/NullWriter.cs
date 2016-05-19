using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Common;

namespace SampleClient
{
    public class NullWriter : PieceWriter
    {
        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
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
            return false;
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
        }
    }
}