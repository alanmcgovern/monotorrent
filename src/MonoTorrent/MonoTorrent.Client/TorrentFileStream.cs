using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class TorrentFileStream : FileStream
    {
        private string path;

        public string Path
        {
            get { return path; }
        }

        public TorrentFileStream(string filePath, FileMode mode, FileAccess access, FileShare share)
            : base(filePath, mode, access, share)
        {
            this.path = filePath;
        }
    }
}
