using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.Trackers;
using MonoTorrent.TrackerServer;

namespace TrackerApp
{
    public class StressTest
    {
        readonly List<string> hashes = new List<string> ();
        readonly Random random = new Random (1);
        readonly SpeedMonitor requests = new SpeedMonitor ();
        readonly Thread[] threads;
        private readonly int threadSleepTime;

        readonly HttpTrackerListener trackerListener;
        readonly string trackerAddress;
        readonly TrackerServer trackerServer;


        public int RequestRate {
            get { return (int) requests.Rate; }
        }

        public long TotalTrackerRequests {
            get { return requests.Total; }
        }

        public StressTest (int torrents, int peers, int requests, string trackerAddress)
        {
            this.trackerAddress = trackerAddress;

            for (int i = 0; i < torrents; i++) {
                byte[] infoHash = new byte[20];
                random.NextBytes (infoHash);
                hashes.Add (new InfoHash (infoHash).UrlEncode ());
            }

            threadSleepTime = Math.Max ((int) (20000.0 / requests + 0.5), 1);
            threads = new Thread[20];

            trackerListener = new HttpTrackerListener (trackerAddress);
            trackerListener.Start ();

            trackerServer = new TrackerServer {
                AllowUnregisteredTorrents = true,
                AllowScrape = true
            };
            trackerServer.RegisterListener (trackerListener);
        }

        public void Start ()
        {
            var client = new HttpClient ();
            for (int i = 0; i < threads.Length; i++) {
                threads[i] = new Thread ((ThreadStart) async delegate {
                    StringBuilder sb = new StringBuilder ();
                    int torrent = 0;
                    while (true) {
                        sb.Remove (0, sb.Length);

                        sb.Append (trackerAddress);
                        sb.Append ("?info_hash=");
                        sb.Append (hashes[(torrent++) % hashes.Count]);
                        sb.Append ("&peer_id=");
                        sb.Append ("12345123451234512345");
                        sb.Append ("&port=");
                        sb.Append ("5000");
                        sb.Append ("&uploaded=");
                        sb.Append ("5000");
                        sb.Append ("&downloaded=");
                        sb.Append ("5000");
                        sb.Append ("&left=");
                        sb.Append ("5000");
                        sb.Append ("&compact=");
                        sb.Append ("1");

                        await client.GetByteArrayAsync (sb.ToString ());
                        requests.AddDelta (1);
                        requests.Tick ();
                        await Task.Delay (threadSleepTime);
                    }
                });
                threads[i].Start ();
            }
        }
    }
}
