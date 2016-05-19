using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;
using MonoTorrent.Tracker.Listeners;

namespace MonoTorrent.Tracker
{
    public class CustomComparer : IPeerComparer
    {
        public object GetKey(AnnounceParameters parameters)
        {
            return parameters.Uploaded;
        }

        public new bool Equals(object left, object right)
        {
            return left.Equals(right);
        }

        public int GetHashCode(object obj)
        {
            return obj.GetHashCode();
        }
    }

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

    public class Trackable : ITrackable
    {
        public Trackable(InfoHash infoHash, string name)
        {
            InfoHash = infoHash;
            Name = name;
        }

        public InfoHash InfoHash { get; }

        public string Name { get; }
    }

    public class PeerDetails
    {
        public IPAddress ClientAddress;
        public long Downloaded;
        public string peerId;
        public int Port;
        public long Remaining;
        public ITrackable trackable;
        public long Uploaded;
    }

    public class TrackerTestRig : IDisposable
    {
        public CustomListener Listener;

        public List<PeerDetails> Peers;
        private readonly Random r = new Random(1000);
        public List<Trackable> Trackables;
        public Tracker Tracker;

        public TrackerTestRig()
        {
            Tracker = new Tracker();
            Listener = new CustomListener();
            Tracker.RegisterListener(Listener);

            GenerateTrackables();
            GeneratePeers();
        }

        public void Dispose()
        {
            Tracker.Dispose();
            Listener.Stop();
        }

        private void GenerateTrackables()
        {
            Trackables = new List<Trackable>();
            for (var i = 0; i < 10; i++)
            {
                var infoHash = new byte[20];
                r.NextBytes(infoHash);
                Trackables.Add(new Trackable(new InfoHash(infoHash), i.ToString()));
            }
        }

        private void GeneratePeers()
        {
            Peers = new List<PeerDetails>();
            for (var i = 0; i < 100; i++)
            {
                var d = new PeerDetails();
                d.ClientAddress = IPAddress.Parse(string.Format("127.0.{0}.2", i));
                d.Downloaded = (int) (10000*r.NextDouble());
                d.peerId = string.Format("-----------------{0:0.000}", i);
                d.Port = r.Next(65000);
                d.Remaining = r.Next(10000, 100000);
                d.Uploaded = r.Next(10000, 100000);
                Peers.Add(d);
            }
        }
    }
}