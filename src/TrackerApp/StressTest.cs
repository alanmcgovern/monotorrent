using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Tracker;
using System.Net;
using System.Collections.Specialized;
using System.Web;
using MonoTorrent.BEncoding;
using System.Threading;
using MonoTorrent.Tracker.Listeners;

namespace TrackerApp
{
    class ManualListener : ListenerBase
    {
        public ManualListener()
        {
        }

        public override bool Running
        {
            get { return true; }
        }

        public override void Start()
        {
            // This is all manual
        }

        public override void Stop()
        {
            // This is all manual
        }

        internal void Handle(byte[] infoHash, IPAddress address, bool started)
        {
            NameValueCollection collection = new NameValueCollection(8);
            collection.Add("info_hash", HttpUtility.UrlEncode(infoHash));
            collection.Add("peer_id", "fake Peer");
            collection.Add("port", "5000");
            collection.Add("uploaded", "5000");
            collection.Add("downloaded", "5000");
            collection.Add("left", "5000");
            collection.Add("compact", "1");
            if (started)
                collection.Add("event", "started");

            BEncodedValue response = base.Handle(collection, address, false);
            // Just ditch the response, this is only stress testing
        }
    }

    public class StressTest
    {
        public int TotalRequests;
        public int StartTime;

        private List<byte[]> hashes;
        private ManualListener listener;
        private Tracker tracker = new Tracker();
        private Random random = new Random(1);
        private int averagePeers;
        private int torrents;

        private Thread[] threads;
        public StressTest()
        {
            averagePeers = 300;
            torrents = 10000;
            hashes = new List<byte[]>(torrents);
            listener = new ManualListener();
            random = new Random(1);
            tracker = new Tracker();

            tracker.RegisterListener(listener);
        }

        internal void Initialise(int torrents, int peers, int requests)
        {
            long ipAddress;
            byte[] infoHash;

            for (int i = 0; i < torrents; i++)
            {
                if (i % 20 == 0)
                    Console.WriteLine("Loaded {0:0.00}%", (double)i / torrents * 100);

                // Create the fake infohash and add the torrent to the tracker.
                infoHash = new byte[20];
                random.NextBytes(infoHash);
                tracker.Add(new InfoHashTrackable(i.ToString(), infoHash));
                hashes.Add(infoHash);

                ipAddress = 1;
                peers = averagePeers;// random.Next((int)(averagePeers * 0.5), (int)(averagePeers * 1.5));
                for (int j = 0; j < peers; j++)
                    listener.Handle(infoHash, new IPAddress(ipAddress++), true);
            }

            threadSleepTime = (int)(20000.0 / requests + 0.5);
            threads = new Thread[20];
        }
        private int threadSleepTime = 0;

        public void StartThreadedStress()
        {
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(StartTest);
                threads[i].Start();
            }
            StartTime = Environment.TickCount;
        }

        private void StartTest()
        {
            long time = Environment.TickCount;
            while (true)
            {
                int torrent = random.Next(0, hashes.Count - 1);
                int ipaddress = random.Next(0, averagePeers);
                listener.Handle(hashes[torrent], new IPAddress(ipaddress), false);
                TotalRequests++;
                System.Threading.Thread.Sleep(threadSleepTime);
            }
        }
    }
}