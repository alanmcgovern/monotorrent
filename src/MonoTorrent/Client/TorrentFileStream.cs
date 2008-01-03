using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class TorrentFileStream : FileStream
    {
        private TorrentFile file;
        private string path;

        public TorrentFile File
        {
            get { return file; }
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
        public string Path
        {
            get { return path; }
        }

        public TorrentFileStream(TorrentFile file, string filePath, FileMode mode, FileAccess access, FileShare share)
            : base(filePath, mode, access, share)
        {
            this.file = file;
            this.path = filePath;
        }
    }
}
