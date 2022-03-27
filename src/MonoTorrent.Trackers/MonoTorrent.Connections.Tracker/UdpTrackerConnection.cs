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

using MonoTorrent.Messages.UdpTracker;
using MonoTorrent.Trackers;

using ReusableTasks;

namespace MonoTorrent.Connections.Tracker
{
    [DebuggerDisplay ("{" + nameof (Uri) + "}")]
    public class UdpTrackerConnection : ITrackerConnection
    {
        public bool CanScrape => true;

        public Uri Uri { get; }

        Task<long>? ConnectionIdTask { get; set; }
        ValueStopwatch LastConnected;
        int MaxRetries { get; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds (15);


        public UdpTrackerConnection (Uri announceUri)
        {
            Uri = announceUri;
        }

        public async ReusableTask<AnnounceResponse> AnnounceAsync (AnnounceRequest parameters, CancellationToken token)
        {
            try {
                if (ConnectionIdTask == null || LastConnected.Elapsed > TimeSpan.FromMinutes (1))
                    ConnectionIdTask = ConnectAsync ();
                long connectionId = await ConnectionIdTask;

                var message = new AnnounceMessage (DateTime.Now.GetHashCode (), connectionId, parameters);
                (var response, var errorString) = await SendAndReceiveAsync (message);

                // Did we receive an 'ErrorMessage' from the tracker? If so, propagate the failure
                if (errorString != null) {
                    ConnectionIdTask = null;
                    return new AnnounceResponse (TrackerState.InvalidResponse, failureMessage: errorString);
                } else if (response != null) {
                    var announce = (AnnounceResponseMessage) response;
                    return new AnnounceResponse (TrackerState.Ok, announce.Peers, minUpdateInterval: announce.Interval);
                } else {
                    throw new NotSupportedException ($"There was no error and no {nameof (AnnounceResponseMessage)} was received");
                }
            } catch (OperationCanceledException) {
                ConnectionIdTask = null;
                return new AnnounceResponse (TrackerState.Offline, failureMessage: "Announce could not be completed");
            } catch {
                ConnectionIdTask = null;
                return new AnnounceResponse (TrackerState.InvalidResponse, failureMessage: "Announce could not be completed");
            }
        }

        public async ReusableTask<ScrapeResponse> ScrapeAsync (ScrapeRequest parameters, CancellationToken token)
        {
            try {
                if (ConnectionIdTask == null || LastConnected.Elapsed > TimeSpan.FromMinutes (1))
                    ConnectionIdTask = ConnectAsync ();
                long connectionId = await ConnectionIdTask;

                var infohashes = new List<InfoHash> { parameters.InfoHash };
                var message = new ScrapeMessage (DateTime.Now.GetHashCode (), connectionId, infohashes);
                (var rawResponse, var errorString) = await SendAndReceiveAsync (message);

                // Did we receive an 'ErrorMessage' from the tracker? If so, propagate the failure
                if (errorString != null) {
                    ConnectionIdTask = null;
                    return new ScrapeResponse (TrackerState.InvalidResponse, failureMessage: errorString);
                } else if (rawResponse is ScrapeResponseMessage response) {
                    int? complete = null, incomplete = null, downloaded = null;
                    if (response.Scrapes.Count == 1) {
                        complete = response.Scrapes[0].Seeds;
                        downloaded = response.Scrapes[0].Complete;
                        incomplete = response.Scrapes[0].Leeches;
                    }
                    return new ScrapeResponse (TrackerState.Ok, complete: complete, downloaded: downloaded, incomplete: incomplete);
                } else {
                    throw new InvalidOperationException ($"There was no error and no {nameof (ScrapeResponseMessage)} was received");
                }
            } catch (OperationCanceledException) {
                ConnectionIdTask = null;
                return new ScrapeResponse (TrackerState.Offline, failureMessage: "Scrape could not be completed");
            } catch (Exception) {
                ConnectionIdTask = null;
                return new ScrapeResponse (TrackerState.InvalidResponse, failureMessage: "Scrape could not be completed");
            }
        }

        async Task<long> ConnectAsync ()
        {
            var message = new ConnectMessage ();
            // Reset the timer so we don't do two concurrent connect requests. It's just an optimisation
            // as concurrent requests are fine!
            LastConnected.Restart ();

            // Send our request, which could take a few retries.
            (var rawResponse, var errorString) = await SendAndReceiveAsync (message);

            // Did we receive an 'ErrorMessage' from the tracker? If so, propagate the failure
            if (errorString != null) {
                ConnectionIdTask = null;
                throw new TrackerException (errorString);
            } else if (rawResponse is ConnectResponseMessage response) {
                // Reset the timer after we receive the response so we get maximum benefit from our
                // 2 minute allowance to use the connection id. 
                LastConnected.Restart ();
                return response.ConnectionId;
            } else {
                throw new InvalidOperationException ($"There was no error and no {nameof (ConnectResponseMessage)} was received");
            }
        }

        async Task<(UdpTrackerMessage?, string?)> SendAndReceiveAsync (UdpTrackerMessage msg)
        {
            var cts = new CancellationTokenSource (TimeSpan.FromSeconds (RetryDelay.TotalSeconds * MaxRetries));

            try {
                // Calling the UdpClient ctor which takes a hostname, or calling the Connect method,
                // results in a synchronous DNS resolve. Ensure we're on a threadpool thread to avoid
                // blocking.
                await new ThreadSwitcher ();
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

        async Task<(UdpTrackerMessage?, string?)> ReceiveAsync (UdpClient client, int transactionId, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                UdpReceiveResult received = await client.ReceiveAsync ();
                var rsp = UdpTrackerMessage.DecodeMessage (received.Buffer.AsSpan (0, received.Buffer.Length), MessageType.Response);

                if (transactionId == rsp.TransactionId) {
                    if (rsp is ErrorMessage error) {
                        return (null, error.Error ?? "The tracker returned an error");
                    } else {
                        return (rsp, null);
                    }
                }
            }
            // If we get here then the token will have been cancelled. We need the additional
            // 'throw' statement to keep the compiler happy.
            token.ThrowIfCancellationRequested ();
            throw new OperationCanceledException ("The tracker did not respond.");
        }

        async void SendAsync (UdpClient client, UdpTrackerMessage msg, CancellationToken token)
        {
            byte[] buffer = msg.Encode ();
            try {
                do {
                    client.Send (buffer, buffer.Length);
                    await Task.Delay (RetryDelay, token);
                }
                while (!token.IsCancellationRequested);
            } catch {
            }
        }
    }
}
