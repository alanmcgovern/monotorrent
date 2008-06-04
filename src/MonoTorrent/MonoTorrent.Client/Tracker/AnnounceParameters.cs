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
        private TrackerConnectionID id;
        private byte[] infohash;
        private string ipaddress;
        private string peerId;
        private int port;
        private bool requireEncryption;
        private bool supportsEncryption;

        /// <summary>
        /// The number of bytes downloaded this session
        /// </summary>
        public long BytesDownloaded
        {
            get { return bytesDownloaded; }
            set { bytesDownloaded = value; }
        }

        /// <summary>
        /// The number of bytes left to download
        /// </summary>
        public long BytesLeft
        {
            get { return bytesLeft; }
            set { bytesLeft = value; }
        }

        /// <summary>
        /// The number of bytes uploaded this session
        /// </summary>
        public long BytesUploaded
        {
            get { return bytesUploaded; }
            set { bytesUploaded = value; }
        }

        /// <summary>
        /// The event that caused this announce (if any)
        /// </summary>
        public TorrentEvent ClientEvent
        {
            get { return clientEvent; }
            set { clientEvent = value; }
        }

        /// <summary>
        /// FIXME: Maybe this should be private....
        /// </summary>
        public TrackerConnectionID Id
        {
            get { return id; }
            set { id = value; }
        }

        /// <summary>
        /// The infohash for the torrent which caused this announce
        /// </summary>
        public byte[] Infohash
        {
            get { return infohash; }
            set { infohash = value; }
        }

        /// <summary>
        /// The publicly advertised IP address for this computer
        /// </summary>
        public string Ipaddress
        {
            get { return ipaddress; }
            set { ipaddress = value; }
        }

        /// <summary>
        /// FIXME: Maybe this should be private?...
        /// </summary>
        public string PeerId
        {
            get { return peerId; }
            set { peerId = value; }
        }

        /// <summary>
        /// The public port number that the engine is listening at for connections
        /// </summary>
        public int Port
        {
            get { return port; }
            set { port = value; }
        }

        /// <summary>
        /// True if encrypted connections are required
        /// </summary>
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
                                    TorrentEvent clientEvent, byte[] infohash, TrackerConnectionID id,
                                    bool requireEncryption, string peerId, string ipaddress, int port)
        {
            this.bytesDownloaded = bytesDownloaded;
            this.bytesUploaded = bytesUploaded;
            this.bytesLeft = bytesLeft;
            this.clientEvent = clientEvent;
            this.infohash = infohash;
            this.id = id;
            this.requireEncryption = requireEncryption;
            this.peerId = peerId;
            this.ipaddress = ipaddress;
            this.port = port;
        }
    }
}