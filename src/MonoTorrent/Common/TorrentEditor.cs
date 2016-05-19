using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent
{
    public class TorrentEditor : EditableTorrent
    {
        public TorrentEditor(Torrent torrent)
            : base(torrent)
        {
        }

        public TorrentEditor(BEncodedDictionary metadata)
            : base(metadata)
        {
        }

        public new bool CanEditSecureMetadata
        {
            get { return base.CanEditSecureMetadata; }
            set { base.CanEditSecureMetadata = value; }
        }
    }
}