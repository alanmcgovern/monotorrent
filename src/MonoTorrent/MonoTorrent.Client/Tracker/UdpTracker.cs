using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.Client.Messages.UdpTracker;

namespace MonoTorrent.Client.Tracker
{
    public class UdpTracker : Tracker
    {
        Task<long> ConnectionIdTask { get; set; }
        Stopwatch LastConnected { get; }  = new Stopwatch ();
        int MaxRetries { get; } = 3;
        public TimeSpan RetryDelay { get; internal set; } = TimeSpan.FromSeconds (15);

        public UdpTracker (Uri announceUrl)
            : base (announceUrl)
        {
            CanScrape = true;
            CanAnnounce = true;
        }

        public override async Task AnnounceAsync (AnnounceParameters parameters, object state)
        {
            try {
                if (ConnectionIdTask == null || LastConnected.Elapsed > TimeSpan.FromMinutes (1))
                    ConnectionIdTask = ConnectAsync ();
                await ConnectionIdTask;

                var message = new AnnounceMessage (DateTime.Now.GetHashCode (), ConnectionIdTask.Result, parameters);
                var announce = (AnnounceResponseMessage) await SendAndReceiveAsync (message);
                MinUpdateInterval = announce.Interval;
                RaiseAnnounceComplete (new AnnounceResponseEventArgs (this, state, true, announce.Peers));
            } catch (Exception e) {
                ConnectionIdTask = null;
                RaiseAnnounceComplete (new AnnounceResponseEventArgs (this, state, false));
                throw new TrackerException ("Announce could not be completed", e);
            }
        }

        public override async Task ScrapeAsync (ScrapeParameters parameters, object state)
        {
            try {
                if (ConnectionIdTask == null || LastConnected.Elapsed > TimeSpan.FromMinutes (1))
                    ConnectionIdTask = ConnectAsync ();
                await ConnectionIdTask;

                var infohashes = new List<byte[]> { parameters.InfoHash.Hash };
                var message = new ScrapeMessage (DateTime.Now.GetHashCode (), ConnectionIdTask.Result, infohashes);
                var response = (ScrapeResponseMessage) await SendAndReceiveAsync (message);

                if (response.Scrapes.Count == 1) {
                    Complete = response.Scrapes[0].Seeds;
                    Downloaded = response.Scrapes[0].Complete;
                    Incomplete = response.Scrapes[0].Leeches;
                }
                RaiseScrapeComplete (new ScrapeResponseEventArgs (this, state, true));
            } catch (Exception e) {
                ConnectionIdTask = null;
                RaiseScrapeComplete (new ScrapeResponseEventArgs (this, state, false));
                throw new TrackerException ("Scrape could not be completed", e);
            }
        }

        async Task<long> ConnectAsync ()
        {
            var message = new ConnectMessage ();
            // Reset the timer so we don't do two concurrent connect requests. It's just an optimisation
            // as concurrent requests are fine!
            LastConnected.Restart ();

            // Send our request, which could take a few retries.
            var response = (ConnectResponseMessage) await SendAndReceiveAsync (message);

            // Reset the timer after we receive the response so we get maximum benefit from our
            // 2 minute allowance to use the connection id. 
            LastConnected.Restart ();
            return response.ConnectionId;
        }

        async Task<UdpTrackerMessage> SendAndReceiveAsync (UdpTrackerMessage msg)
        {
            var cts = new CancellationTokenSource (TimeSpan.FromSeconds (RetryDelay.TotalSeconds * MaxRetries));

            using (var udpClient = new UdpClient (Uri.Host, Uri.Port))
            using (cts.Token.Register (() => udpClient.Dispose ())) {
                SendAsync (udpClient, msg, cts.Token);
                return await ReceiveAsync (udpClient, msg.TransactionId, cts.Token);
            }
        }

        async Task<UdpTrackerMessage> ReceiveAsync (UdpClient client, int transactionId, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                var received = await client.ReceiveAsync ();
                UdpTrackerMessage rsp = UdpTrackerMessage.DecodeMessage (received.Buffer, 0, received.Buffer.Length, MessageType.Response);
                
                if (transactionId == rsp.TransactionId) {
                    if (rsp is ErrorMessage error) {
                        FailureMessage = error.Error;
                        throw new Exception ("The tracker returned an error.");
                    } else {
                        return rsp;
                    }
                }
            }
            throw new Exception ("The tracker did not respond.");
        }

        void SendAsync (UdpClient client, UdpTrackerMessage msg, CancellationToken token)
        {
            var buffer = msg.Encode ();

            client.Send (buffer, buffer.Length);
            ClientEngine.MainLoop.QueueTimeout (RetryDelay, () => {
                try {
                    if (!token.IsCancellationRequested)
                        client.Send (buffer, buffer.Length);
                    return !token.IsCancellationRequested;
                } catch {
                    return false;
                }
            });
        }

        public override string ToString ()
        {
            return Uri.ToString ();
        }
    }
}
