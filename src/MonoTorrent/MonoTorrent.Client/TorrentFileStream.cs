using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class TorrentFileStream : FileStream
    {
        TorrentFile file;
        private string path;

        public TorrentFile File
        {
            get { return file; }
        }

        public string Path
        {
            get { return path; }
        }


        public TorrentFileStream(string filePath, TorrentFile file, FileMode mode, FileAccess access, FileShare share)
            : base(filePath, mode, access, share)
        {
            this.file = file;
            this.path = filePath;
        }
    }
}
