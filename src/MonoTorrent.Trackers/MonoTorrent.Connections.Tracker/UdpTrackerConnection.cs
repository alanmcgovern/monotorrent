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
using System.Linq;
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

        public AddressFamily AddressFamily { get; }
        public Uri Uri { get; }

        Task<long>? ConnectionIdTask { get; set; }
        ValueStopwatch LastConnected;
        int MaxRetries { get; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds (15);


        public UdpTrackerConnection (Uri announceUri, AddressFamily addressFamily)
        {
            AddressFamily = addressFamily;
            Uri = announceUri;
        }

        public async ReusableTask<AnnounceResponse> AnnounceAsync (AnnounceRequest parameters, CancellationToken token)
        {
            try {
                if (ConnectionIdTask == null || LastConnected.Elapsed > TimeSpan.FromMinutes (1))
                    ConnectionIdTask = ConnectAsync ();
                long connectionId = await ConnectionIdTask;

                // IPV6 overrides are unsupported by the udp tracker protocol.
                //
                // "That means the IP address field in the request remains 32bits wide which makes this
                // field not usable under IPv6 and thus should always be set to 0.
                //

                var port = parameters.GetReportedAddress (AddressFamily == AddressFamily.InterNetwork ? "ipv4" : "ipv6").port;
                AnnounceResponse? announceResponse = null;
                foreach (var infoHash in new[] { parameters.InfoHashes.V1, parameters.InfoHashes.V2 }) {
                    if (infoHash is null)
                        continue;

                    var message = new AnnounceMessage (DateTime.Now.GetHashCode (), connectionId, parameters, infoHash, port);
                    (var response, var errorString) = await SendAndReceiveAsync (message);

                    // Did we receive an 'ErrorMessage' from the tracker? If so, propagate the failure
                    if (errorString != null) {
                        ConnectionIdTask = null;
                        return new AnnounceResponse (TrackerState.InvalidResponse, failureMessage: errorString);
                    } else if (response != null) {
                        var announce = (AnnounceResponseMessage) response;
                        if (announceResponse == null) {
                            var peerDict = new Dictionary<InfoHash, IList<PeerInfo>> { { infoHash, announce.Peers } };
                            announceResponse = new AnnounceResponse (TrackerState.Ok, peerDict, minUpdateInterval: announce.Interval);
                        } else {
                            announceResponse.Peers[infoHash] = announce.Peers; 
                        }
                    } else {
                        throw new NotSupportedException ($"There was no error and no {nameof (AnnounceResponseMessage)} was received");
                    }
                }
                return announceResponse ?? throw new Exception ("There should have been at least one infohash to announce with");
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

                var infohashes = new List<InfoHash> (2);
                if (parameters.InfoHashes.V1 != null)
                    infohashes.Add (parameters.InfoHashes.V1);
                if (parameters.InfoHashes.V2 != null)
                    infohashes.Add (parameters.InfoHashes.V2);

                var message = new ScrapeMessage (DateTime.Now.GetHashCode (), connectionId, infohashes);
                (var rawResponse, var errorString) = await SendAndReceiveAsync (message);

                // Did we receive an 'ErrorMessage' from the tracker? If so, propagate the failure
                if (errorString != null) {
                    ConnectionIdTask = null;
                    return new ScrapeResponse (TrackerState.InvalidResponse, failureMessage: errorString);
                } else if (rawResponse is ScrapeResponseMessage response) {
                    var scrapeInfo = new Dictionary<InfoHash, ScrapeInfo> ();
                    for (int i = 0; i < response.Scrapes.Count; i++) {
                        var info = new ScrapeInfo (
                            complete: response.Scrapes[i].Seeds,
                            downloaded: response.Scrapes[i].Complete,
                            incomplete: response.Scrapes[i].Leeches
                        );
                        scrapeInfo.Add (infohashes[i], info);
                    }
                    return new ScrapeResponse (TrackerState.Ok, scrapeInfo);
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
                using var udpClient = new UdpClient (AddressFamily);
                udpClient.Connect (Uri.Host, Uri.Port);

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
            UdpReceiveResult received = default;
            while (!token.IsCancellationRequested) {
                try {
                    received = await client.ReceiveAsync ();
                } catch (SocketException) {
                    token.ThrowIfCancellationRequested ();
                    // If we never receive a response, assume the tracker is offline and return an 'operationcancelled'
                    throw new OperationCanceledException ();
                }
                var rsp = UdpTrackerMessage.DecodeMessage (received.Buffer.AsSpan (0, received.Buffer.Length), MessageType.Response, received.RemoteEndPoint.AddressFamily);

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
            ReadOnlyMemory<byte> buffer = msg.Encode ();
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
