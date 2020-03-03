//
// UdpTracker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2007 Eric Butler
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.Messages.UdpTracker;

namespace MonoTorrent.Client.Tracker
{
    [DebuggerDisplay("{" + nameof(Uri)+ "}")]
    class UdpTracker : Tracker
    {
        Task<long> ConnectionIdTask { get; set; }
        ValueStopwatch LastConnected;
        int MaxRetries { get; } = 3;
        internal TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds (15);

        public UdpTracker (Uri announceUrl)
            : base (announceUrl)
        {
            CanScrape = true;
            CanAnnounce = true;
            Status = TrackerState.Unknown;
        }

        protected override async Task<List<Peer>> DoAnnounceAsync (AnnounceParameters parameters)
        {
            try {
                if (ConnectionIdTask == null || LastConnected.Elapsed > TimeSpan.FromMinutes (1))
                    ConnectionIdTask = ConnectAsync ();
                long connectionId = await ConnectionIdTask;

                Status = TrackerState.Connecting;
                var message = new AnnounceMessage (DateTime.Now.GetHashCode (), connectionId, parameters);
                var announce = (AnnounceResponseMessage) await SendAndReceiveAsync (message);
                MinUpdateInterval = announce.Interval;

                Status = TrackerState.Ok;
                return announce.Peers;
            } catch (OperationCanceledException e) {
                Status = TrackerState.Offline;
                ConnectionIdTask = null;
                throw new TrackerException ("Announce could not be completed", e);
            } catch (Exception e) {
                Status = TrackerState.InvalidResponse;
                ConnectionIdTask = null;
                throw new TrackerException ("Announce could not be completed", e);
            }
        }

        protected override async Task DoScrapeAsync (ScrapeParameters parameters)
        {
            try {
                if (ConnectionIdTask == null || LastConnected.Elapsed > TimeSpan.FromMinutes (1))
                    ConnectionIdTask = ConnectAsync ();
                long connectionId = await ConnectionIdTask;

                var infohashes = new List<byte[]> { parameters.InfoHash.Hash };
                var message = new ScrapeMessage (DateTime.Now.GetHashCode (), connectionId, infohashes);
                var response = (ScrapeResponseMessage) await SendAndReceiveAsync (message);

                if (response.Scrapes.Count == 1) {
                    Complete = response.Scrapes[0].Seeds;
                    Downloaded = response.Scrapes[0].Complete;
                    Incomplete = response.Scrapes[0].Leeches;
                }
                Status = TrackerState.Ok;
            } catch (OperationCanceledException e) {
                Status = TrackerState.Offline;
                ConnectionIdTask = null;
                throw new TrackerException ("Scrape could not be completed", e);
            } catch (Exception e) {
                Status = TrackerState.InvalidResponse;
                ConnectionIdTask = null;
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

            try {
                // Calling the UdpClient ctor which takes a hostname, or calling the Connect method,
                // results in a synchronous DNS resolve. Ensure we're on a threadpool thread to avoid
                // blocking.
                await MainLoop.SwitchToThreadpool ();
                using var udpClient = new UdpClient (Uri.Host, Uri.Port);
                using (cts.Token.Register (() => udpClient.Dispose ())) {
                    SendAsync (udpClient, msg, cts.Token);
                    return await ReceiveAsync (udpClient, msg.TransactionId, cts.Token).ConfigureAwait (false);
                }
            } catch {
                cts.Token.ThrowIfCancellationRequested ();
                throw;
            }
        }

        async Task<UdpTrackerMessage> ReceiveAsync (UdpClient client, int transactionId, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                UdpReceiveResult received = await client.ReceiveAsync ();
                var rsp = UdpTrackerMessage.DecodeMessage (received.Buffer, 0, received.Buffer.Length, MessageType.Response);

                if (transactionId == rsp.TransactionId) {
                    if (rsp is ErrorMessage error) {
                        FailureMessage = error.Error;
                        throw new Exception ("The tracker returned an error.");
                    } else {
                        return rsp;
                    }
                }
            }
            // If we get here then the token will have been cancelled. We need the additional
            // 'throw' statement to keep the compiler happy.
            token.ThrowIfCancellationRequested ();
            throw new OperationCanceledException ("The tracker did not respond.");
        }

        void SendAsync (UdpClient client, UdpTrackerMessage msg, CancellationToken token)
        {
            byte[] buffer = msg.Encode ();
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
    }
}
