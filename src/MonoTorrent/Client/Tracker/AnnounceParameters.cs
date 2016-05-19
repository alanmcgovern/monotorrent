using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public class AnnounceParameters
    {
        private long bytesDownloaded;
        private long bytesLeft;
        private long bytesUploaded;
        private TorrentEvent clientEvent;
        private InfoHash infohash;
        private string ipaddress;
        private string peerId;
        private int port;
        private bool requireEncryption;
        private bool supportsEncryption;

        public long BytesDownloaded
        {
            get { return bytesDownloaded; }
            set { bytesDownloaded = value; }
        }

        public long BytesLeft
        {
            get { return bytesLeft; }
            set { bytesLeft = value; }
        }

        public long BytesUploaded
        {
            get { return bytesUploaded; }
            set { bytesUploaded = value; }
        }

        public TorrentEvent ClientEvent
        {
            get { return clientEvent; }
            set { clientEvent = value; }
        }

        public InfoHash InfoHash
        {
            get { return infohash; }
            set { infohash = value; }
        }

        public string Ipaddress
        {
            get { return ipaddress; }
            set { ipaddress = value; }
        }

        public string PeerId
        {
            get { return peerId; }
            set { peerId = value; }
        }

        public int Port
        {
            get { return port; }
            set { port = value; }
        }

        public bool RequireEncryption
        {
            get { return requireEncryption; }
            set { requireEncryption = value; }
        }

        public bool SupportsEncryption
        {
            get { return supportsEncryption; }
            set { supportsEncryption = value; }
        }


        public AnnounceParameters()
        {

        }

        public AnnounceParameters(long bytesDownloaded, long bytesUploaded, long bytesLeft,
                                  TorrentEvent clientEvent, InfoHash infohash, bool requireEncryption,
                                  string peerId, string ipaddress, int port)
        {
            this.bytesDownloaded = bytesDownloaded;
            this.bytesUploaded = bytesUploaded;
            this.bytesLeft = bytesLeft;
            this.clientEvent = clientEvent;
            this.infohash = infohash;
            this.requireEncryption = requireEncryption;
            this.peerId = peerId;
            this.ipaddress = ipaddress;
            this.port = port;
        }
    }
}