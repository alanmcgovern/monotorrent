using System.Collections.Generic;
using MonoTorrent.Common;

namespace MonoTorrent.Tests.Common
{
    public class CustomFileSource : ITorrentFileSource
    {
        public CustomFileSource(List<FileMapping> files)
        {
            Files = files;
        }

        public bool IgnoreHidden
        {
            get { return false; }
        }

        public IEnumerable<FileMapping> Files { get; }

        public string TorrentName
        {
            get { return "Name"; }
        }
    }
}