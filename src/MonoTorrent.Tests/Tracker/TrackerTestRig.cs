using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Tracker;
using MonoTorrent.Tracker.Listeners;
using System.Collections.Specialized;
using System.Net;
using System.Web;
using MonoTorrent.BEncoding;

namespace MonoTorrent.TrackerTests
{
    public class CustomComparer : MonoTorrent.Tracker.IPeerComparer
    {
        public bool Equals(object left, object right)
        {
            return left.Equals(right);
        }
        public object GetKey(AnnounceParameters parameters)
        {
            return parameters.Uploaded;
        }

        public int GetHashCode(object obj)
        {
            return obj.GetHashCode();
        }
    }

    public class CustomListener : ListenerBase
    {
        public BEncodedValue Handle(PeerDetails d, MonoTorrent.Common.TorrentEvent e, ITrackable trackable)
        {
            NameValueCollection c = new NameValueCollection();
            c.Add("info_hash", HttpUtility.UrlEncode(trackable.InfoHash));
            c.Add("peer_id", d.peerId);
            c.Add("port", d.Port.ToString());
            c.Add("uploaded", d.Uploaded.ToString());
            c.Add("downloaded", d.Downloaded.ToString());
            c.Add("left", d.Remaining.ToString());
            c.Add("compact", "0");

            return base.Handle(c, d.ClientAddress, false);
        }

        public override bool Running
        {
            get { return true; }
        }

        public override void Start()
        {
            
        }

        public override void Stop()
        {
            
        }
    }
    public class Trackable : ITrackable
    {
        private byte[] infoHash;
        private string name;


        public Trackable(byte[] infoHash, string name)
        {
            this.infoHash = infoHash;
            this.name = name;
        }

        public byte[] InfoHash
        {
            get { return infoHash; }
        }

        public string Name
        {
            get { return name; }
        }
    }

    public class PeerDetails
    {
        public int Port;
        public IPAddress ClientAddress;
        public long Downloaded;
        public long Uploaded;
        public long Remaining;
        public string peerId;
        public ITrackable trackable;
    }

    public class TrackerTestRig
    {
        private Random r = new Random(1000);

        public CustomListener Listener;
        public Tracker.Tracker Tracker;

        public List<PeerDetails> Peers;
        public List<Trackable> Trackables;

        public TrackerTestRig()
        {
            Tracker = new MonoTorrent.Tracker.Tracker();
            Listener = new CustomListener();
            Tracker.RegisterListener(Listener);

            GenerateTrackables();
            GeneratePeers();
        }

        private void GenerateTrackables()
        {
            Trackables = new List<Trackable>();
            for (int i = 0; i < 10; i++)
            {
                byte[] infoHash = new byte[20];
                r.NextBytes(infoHash);
                Trackables.Add(new Trackable(infoHash, i.ToString()));
            }
        }

        private void GeneratePeers()
        {
            Peers = new List<PeerDetails>();
            for (int i = 0; i < 100; i++)
            {
                PeerDetails d = new PeerDetails();
                d.ClientAddress = IPAddress.Parse(string.Format("127.0.{0}.2", i));
                d.Downloaded = (int)(10000 * r.NextDouble());
                d.peerId = string.Format("-----------------{0:0.000}", i);
                d.Port = r.Next(65000);
                d.Remaining = r.Next(10000, 100000);
                d.Uploaded = r.Next(10000, 100000);
                Peers.Add(d);
            }
        }
    }
}
