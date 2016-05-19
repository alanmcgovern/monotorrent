using System.IO;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class TorrentFileStream : FileStream
    {
        public TorrentFileStream(TorrentFile file, FileMode mode, FileAccess access, FileShare share)
            : base(file.FullPath, mode, access, share, 1)
        {
            File = file;
        }

        public TorrentFile File { get; }

        public string Path
        {
            get { return File.FullPath; }
        }
    }
}