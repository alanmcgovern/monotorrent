using System;
using System.Collections.Generic;
using System.Net;

namespace MonoTorrent.Tests.Tracker
{
    public class TrackerTestRig : IDisposable
    {
        private readonly Random r = new Random(1000);
        public CustomListener Listener;

        public List<PeerDetails> Peers;
        public List<Trackable> Trackables;
        public MonoTorrent.Tracker.Tracker Tracker;

        public TrackerTestRig()
        {
            Tracker = new MonoTorrent.Tracker.Tracker();
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