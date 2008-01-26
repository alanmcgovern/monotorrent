using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Tracker;
using System.Net;
using System.Collections.Specialized;
using System.Web;
using MonoTorrent.BEncoding;
using System.Threading;

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

        public StressTest()
        {
            averagePeers = 20;
            torrents = 8000;
            hashes = new List<byte[]>(torrents);
            listener = new ManualListener();
            random = new Random(1);
            tracker = new Tracker();

            tracker.RegisterListener(listener);
        }

        internal void Initialise(bool testA)
        {
            if (testA)
                TestA();
            else
                TestB();
        }
        private void TestB()
        {
            long ipAddress;
            byte[] torrent;

            List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>();
            list.Add(new KeyValuePair<int, int>(50, 1000));
            list.Add(new KeyValuePair<int, int>(100, 500));
            list.Add(new KeyValuePair<int, int>(250, 50));
            list.Add(new KeyValuePair<int, int>(200, 60));
            foreach (KeyValuePair<int, int> pair in list)
            {
                for (int i = 0; i < pair.Key; i++)
                {
                    // Create the fake infohash and add the torrent to the tracker.
                    torrent = new byte[20];
                    random.NextBytes(torrent);
                    tracker.Add(new InfoHashTrackable(i.ToString(), torrent));
                    hashes.Add(torrent);

                    ipAddress = 1;
                    for (int j = 0; j < pair.Value; j++)
                        listener.Handle(torrent, new IPAddress(ipAddress++), true);
                }
            }
        }

        private void TestA()
        {
            long ipAddress;
            int peers;
            byte[] torrent;

            for (int i = 0; i < torrents; i++)
            {
                if (i % 20 == 0)
                    Console.WriteLine("Loaded {0}/{1}", i, torrents);

                if (i % 250 == 0)
                    Thread.Sleep(1000);

                // Create the fake infohash and add the torrent to the tracker.
                torrent = new byte[20];
                random.NextBytes(torrent);
                tracker.Add(new InfoHashTrackable(i.ToString(), torrent));
                hashes.Add(torrent);

                ipAddress = 1;
                peers = averagePeers;// random.Next((int)(averagePeers * 0.5), (int)(averagePeers * 1.5));
                for (int j = 0; j < peers; j++)
                    listener.Handle(torrent, new IPAddress(ipAddress++), true);
            }
        }
        public void StartThreadedStress(int count)
        {
            for (int i = 0; i < count; i++)
                new Thread(StartTest).Start();

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
                System.Threading.Thread.Sleep(100);
            }
        }
    }
}