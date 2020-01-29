using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

using MonoTorrent;

namespace TrackerApp
{
    public class StressTest
    {
        readonly List<string> hashes = new List<string> ();
        readonly Random random = new Random (1);
        readonly SpeedMonitor requests = new SpeedMonitor ();
        readonly Thread[] threads;
        private readonly int threadSleepTime;

        public int RequestRate {
            get { return (int) requests.Rate; }
        }

        public long TotalTrackerRequests {
            get { return requests.Total; }
        }

        public StressTest (int torrents, int peers, int requests)
        {
            for (int i = 0; i < torrents; i++) {
                byte[] infoHash = new byte[20];
                random.NextBytes (infoHash);
                hashes.Add (new InfoHash (infoHash).UrlEncode ());
            }

            threadSleepTime = Math.Max ((int) (20000.0 / requests + 0.5), 1);
            threads = new Thread[20];
        }

        public void Start (string trackerAddress)
        {
            for (int i = 0; i < threads.Length; i++) {
                threads[i] = new Thread ((ThreadStart) delegate {
                    StringBuilder sb = new StringBuilder ();
                    int torrent = 0;
                    while (true) {
                        sb.Remove (0, sb.Length);

                        int ipaddress = random.Next (0, hashes.Count);

                        sb.Append (trackerAddress);
                        sb.Append ("?info_hash=");
                        sb.Append (hashes[torrent++]);
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

                        WebRequest req = WebRequest.Create (sb.ToString ());
                        req.BeginGetResponse (delegate (IAsyncResult r) {
                            try {
                                req.EndGetResponse (r).Close ();
                                requests.AddDelta (1);
                            } catch {
                            } finally {
                                requests.Tick ();
                            }
                        }, null);

                        Thread.Sleep (threadSleepTime);
                    }
                });
                threads[i].Start ();
            }
        }
    }
}
