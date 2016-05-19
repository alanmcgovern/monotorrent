using System.Collections.Specialized;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;
using MonoTorrent.Tracker;
using MonoTorrent.Tracker.Listeners;

namespace MonoTorrent.Tests.Tracker
{
    public class CustomListener : ListenerBase
    {
        public override bool Running
        {
            get { return true; }
        }

        public BEncodedValue Handle(PeerDetails d, TorrentEvent e, ITrackable trackable)
        {
            var c = new NameValueCollection();
            c.Add("info_hash", trackable.InfoHash.UrlEncode());
            c.Add("peer_id", d.peerId);
            c.Add("port", d.Port.ToString());
            c.Add("uploaded", d.Uploaded.ToString());
            c.Add("downloaded", d.Downloaded.ToString());
            c.Add("left", d.Remaining.ToString());
            c.Add("compact", "0");

            return base.Handle(c, d.ClientAddress, false);
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
        }
    }
}