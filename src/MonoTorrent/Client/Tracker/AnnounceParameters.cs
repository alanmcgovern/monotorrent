using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public class AnnounceParameters
    {
        public AnnounceParameters()
        {
        }

        public AnnounceParameters(long bytesDownloaded, long bytesUploaded, long bytesLeft,
            TorrentEvent clientEvent, InfoHash infohash, bool requireEncryption,
            string peerId, string ipaddress, int port)
        {
            BytesDownloaded = bytesDownloaded;
            BytesUploaded = bytesUploaded;
            BytesLeft = bytesLeft;
            ClientEvent = clientEvent;
            InfoHash = infohash;
            RequireEncryption = requireEncryption;
            PeerId = peerId;
            Ipaddress = ipaddress;
            Port = port;
        }

        public long BytesDownloaded { get; set; }

        public long BytesLeft { get; set; }

        public long BytesUploaded { get; set; }

        public TorrentEvent ClientEvent { get; set; }

        public InfoHash InfoHash { get; set; }

        public string Ipaddress { get; set; }

        public string PeerId { get; set; }

        public int Port { get; set; }

        public bool RequireEncryption { get; set; }

        public bool SupportsEncryption { get; set; }
    }
}