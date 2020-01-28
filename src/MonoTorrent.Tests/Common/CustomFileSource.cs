using System.Collections.Generic;

namespace MonoTorrent.Common
{
    public class CustomFileSource : ITorrentFileSource
    {
        public IEnumerable<FileMapping> Files {
            get; private set;
        }

        public bool IgnoreHidden => false;

        public string TorrentName => "Name";

        public CustomFileSource (List<FileMapping> files)
        {
            Files = files;
        }
    }
}
