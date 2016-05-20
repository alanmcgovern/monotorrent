using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using MonoTorrent;
using MonoTorrent.Common;

namespace TrackerApp
{
    public class StressTest
    {
        private readonly List<string> hashes = new List<string>();
        private readonly Random random = new Random(1);
        private readonly SpeedMonitor requests = new SpeedMonitor();
        private readonly Thread[] threads;
        private readonly int threadSleepTime;

        public StressTest(int torrents, int peers, int requests)
        {
            for (var i = 0; i < torrents; i++)
            {
                var infoHash = new byte[20];
                random.NextBytes(infoHash);
                hashes.Add(new InfoHash(infoHash).UrlEncode());
            }

            threadSleepTime = Math.Max((int) (20000.0/requests + 0.5), 1);
            threads = new Thread[20];
        }

        public int RequestRate
        {
            get { return requests.Rate; }
        }

        public long TotalTrackerRequests
        {
            get { return requests.Total; }
        }

        public void Start(string trackerAddress)
        {
            for (var i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread((ThreadStart) delegate
                {
                    var sb = new StringBuilder();
                    var torrent = 0;
                    while (true)
                    {
                        sb.Remove(0, sb.Length);

                        var ipaddress = random.Next(0, hashes.Count);

                        sb.Append(trackerAddress);
                        sb.Append("?info_hash=");
                        sb.Append(hashes[torrent++]);
                        sb.Append("&peer_id=");
                        sb.Append("12345123451234512345");
                        sb.Append("&port=");
                        sb.Append("5000");
                        sb.Append("&uploaded=");
                        sb.Append("5000");
                        sb.Append("&downloaded=");
                        sb.Append("5000");
                        sb.Append("&left=");
                        sb.Append("5000");
                        sb.Append("&compact=");
                        sb.Append("1");

                        var req = WebRequest.Create(sb.ToString());
                        req.BeginGetResponse(delegate(IAsyncResult r)
                        {
                            try
                            {
                                req.EndGetResponse(r).Close();
                                requests.AddDelta(1);
                            }
                            catch
                            {
                            }
                            finally
                            {
                                requests.Tick();
                            }
                        }, null);

                        Thread.Sleep(threadSleepTime);
                    }
                });
                threads[i].Start();
            }
        }
    }
}