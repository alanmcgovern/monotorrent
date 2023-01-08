//
// HTTPTracker.cs
//
// Authors:
//   Eric Butler eric@extremeboredom.net
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Logging;
using MonoTorrent.Trackers;

using ReusableTasks;

namespace MonoTorrent.Connections.Tracker
{
    public class HttpTrackerConnection : ITrackerConnection
    {
        static readonly BEncodedString FilesKey = new BEncodedString ("files");

        static readonly Logger logger = Logger.Create (nameof (HttpTrackerConnection));

        public AddressFamily AddressFamily { get; }

        public bool CanScrape { get; }

        public Uri? ScrapeUri { get; }

        public Uri Uri { get; }

        // FIXME: Make private?
        public BEncodedString? TrackerId { get; set; }

        HttpClient Client { get; }

        public HttpTrackerConnection (Uri announceUri, HttpClient client)
            : this(announceUri, client, AddressFamily.InterNetwork)
        {

        }

        public HttpTrackerConnection (Uri announceUri, HttpClient client, AddressFamily addressFamily)
        {
            AddressFamily = addressFamily;
            Uri = announceUri;
            Client = client;

            string uri = announceUri.OriginalString;
            if (uri.EndsWith ("/announce", StringComparison.OrdinalIgnoreCase))
                ScrapeUri = new Uri ($"{uri.Substring (0, uri.Length - "/announce".Length)}/scrape");
            else if (uri.EndsWith ("/announce/", StringComparison.OrdinalIgnoreCase))
                ScrapeUri = new Uri ($"{uri.Substring (0, uri.Length - "/announce/".Length)}/scrape/");

            CanScrape = ScrapeUri != null;
        }

        public async ReusableTask<AnnounceResponse> AnnounceAsync (AnnounceRequest parameters, CancellationToken token)
        {
            // WebRequest.Create can be a comparatively slow operation as reported
            // by profiling. Switch this to the threadpool so the querying of default
            // proxies, and any DNS requests, are definitely not run on the main thread.
            await new ThreadSwitcher ();

            AnnounceResponse? announceResponse = null;
            foreach (var infoHash in new[] { parameters.InfoHashes.V1!, parameters.InfoHashes.V2! }.Where (t => t != null)) {
                Uri announceString = CreateAnnounceString (parameters, infoHash);
                HttpResponseMessage response;

                try {
                    response = await Client.GetAsync (announceString, HttpCompletionOption.ResponseHeadersRead, token);
                } catch {
                    return new AnnounceResponse (
                        state: TrackerState.Offline,
                        failureMessage: "The tracker could not be contacted"
                    );
                }

                try {
                    using var responseRegistration = token.Register (() => response.Dispose ());
                    using (response) {
                        var resp = await AnnounceReceivedAsync (infoHash, response).ConfigureAwait (false);
                        if (announceResponse == null) {
                            announceResponse = resp;
                        } else {
                            announceResponse.Peers[infoHash] = resp.Peers[infoHash];
                        }
                        logger.InfoFormatted ("Tracker {0} sent {1} peers for InfoHash {2}", Uri, resp.Peers[infoHash].Count, infoHash);
                    }
                } catch {
                    return new AnnounceResponse (
                        state: TrackerState.InvalidResponse,
                        failureMessage: "The tracker returned an invalid or incomplete response"
                    );
                }
            }
            return announceResponse!;
        }

        public async ReusableTask<ScrapeResponse> ScrapeAsync (ScrapeRequest parameters, CancellationToken token)
        {
            // WebRequest.Create can be a comparatively slow operation as reported
            // by profiling. Switch this to the threadpool so the querying of default
            // proxies, and any DNS requests, are definitely not run on the main thread.
            await new ThreadSwitcher ();

            string url = ScrapeUri!.OriginalString;
            foreach (InfoHash infohash in new[] { parameters.InfoHashes.V1!, parameters.InfoHashes.V2! }.Where (t => t != null)) {
                // If you want to scrape the tracker for *all* torrents, don't append the info_hash.
                if (url.IndexOf ('?') == -1)
                    url += $"?info_hash={infohash.Truncate ().UrlEncode ()}";
                else
                    url += $"&info_hash={infohash.Truncate ().UrlEncode ()}";
            }

            HttpResponseMessage response;

            try {
                response = await Client.GetAsync (url, HttpCompletionOption.ResponseHeadersRead, token);
            } catch {
                return new ScrapeResponse (
                    state: TrackerState.Offline,
                    failureMessage: "The tracker could not be contacted"
                );
            }

            try {
                using var responseRegistration = token.Register (() => response.Dispose ());
                using (response)
                    return await ScrapeReceivedAsync (parameters.InfoHashes, response).ConfigureAwait (false);
            } catch {
                return new ScrapeResponse (
                    state: TrackerState.InvalidResponse,
                    failureMessage: "The tracker returned an invalid or incomplete response"
                );
            }
        }

        Uri CreateAnnounceString (AnnounceRequest parameters, InfoHash infoHash)
        {
            var b = new UriQueryBuilder (Uri);
            b.Add ("info_hash", infoHash.Truncate ().UrlEncode ())
             .Add ("peer_id", BEncodedString.FromMemory (parameters.PeerId).UrlEncode ())
             .Add ("port", parameters.Port)
             .Add ("uploaded", parameters.BytesUploaded)
             .Add ("downloaded", parameters.BytesDownloaded)
             .Add ("left", parameters.BytesLeft)
             .Add ("compact", 1)
             .Add ("numwant", 100);

            if (parameters.SupportsEncryption)
                b.Add ("supportcrypto", 1);
            if (parameters.RequireEncryption)
                b.Add ("requirecrypto", 1);
            if (!b.Contains ("key"))
                b.Add ("key", parameters.Key);
            if (!string.IsNullOrEmpty (parameters.IPAddress))
                b.Add ("ip", parameters.IPAddress!);

            // If we have not successfully sent the started event to this tier, override the passed in started event
            // Otherwise append the event if it is not "none"
            //if (!parameters.Id.Tracker.Tier.SentStartedEvent)
            //{
            //    sb.Append("&event=started");
            //    parameters.Id.Tracker.Tier.SendingStartedEvent = true;
            //}
            if (parameters.ClientEvent != TorrentEvent.None)
                b.Add ("event", parameters.ClientEvent.ToString ().ToLower ());

            if (!BEncodedString.IsNullOrEmpty (TrackerId))
                b.Add ("trackerid", TrackerId!.UrlEncode ());

            return b.ToUri ();
        }

        static async Task<BEncodedDictionary> DecodeResponseAsync (HttpResponseMessage response)
        {
            int bytesRead = 0;
            int totalRead = 0;
            byte[] buffer = new byte[2048];

            long contentLength = response.Content.Headers.ContentLength ?? -1;
            using var dataStream = new MemoryStream (contentLength > 0 ? (int) contentLength : 256);
            using (Stream reader = await response.Content.ReadAsStreamAsync ()) {
                // If there is a ContentLength, use that to decide how much we read.
                if (contentLength > 0) {
                    while ((bytesRead = await reader.ReadAsync (buffer, 0, (int) Math.Min (contentLength - totalRead, buffer.Length)).ConfigureAwait (false)) > 0) {
                        dataStream.Write (buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        if (totalRead == contentLength)
                            break;
                    }
                } else    // A compact response doesn't always have a content length, so we
                {       // just have to keep reading until we think we have everything.
                    while ((bytesRead = await reader.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false)) > 0)
                        dataStream.Write (buffer, 0, bytesRead);
                }
            }
            dataStream.Seek (0, SeekOrigin.Begin);
            return (BEncodedDictionary) BEncodedValue.Decode (dataStream);
        }

        public override bool Equals (object? obj)
            // If the announce URL matches, then CanScrape and the scrape URL must match too
            => obj is HttpTrackerConnection tracker && (Uri.Equals (tracker.Uri));

        public override int GetHashCode ()
        {
            return Uri.GetHashCode ();
        }

        async Task<AnnounceResponse> AnnounceReceivedAsync (InfoHash infoHash, HttpResponseMessage response)
        {
            await new ThreadSwitcher ();

            BEncodedDictionary dict = await DecodeResponseAsync (response).ConfigureAwait (false);
            return HandleAnnounce (infoHash, dict);
        }

        AnnounceResponse HandleAnnounce (InfoHash infoHash, BEncodedDictionary dict)
        {
            int complete = 0, incomplete = 0, downloaded = 0;
            TimeSpan? minUpdateInterval = null, updateInterval = null;
            string failureMessage = "", warningMessage = "";
            var peers = new List<PeerInfo> ();
            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dict) {
                switch (keypair.Key.Text) {
                    case ("complete"):
                        complete = Convert.ToInt32 (keypair.Value.ToString ());
                        break;

                    case ("incomplete"):
                        incomplete = Convert.ToInt32 (keypair.Value.ToString ());
                        break;

                    case ("downloaded"):
                        downloaded = Convert.ToInt32 (keypair.Value.ToString ());
                        break;

                    case ("tracker id"):
                        TrackerId = (BEncodedString) keypair.Value;
                        break;

                    case ("min interval"):
                        minUpdateInterval = TimeSpan.FromSeconds (int.Parse (keypair.Value.ToString ()!));
                        break;

                    case ("interval"):
                        updateInterval = TimeSpan.FromSeconds (int.Parse (keypair.Value.ToString ()!));
                        break;

                    case ("peers"):
                        if (keypair.Value is BEncodedList bencodedList)          // Non-compact response
                            peers.AddRange (PeerDecoder.Decode (bencodedList, AddressFamily));
                        else if (keypair.Value is BEncodedString bencodedStr)   // Compact response
                            peers.AddRange (PeerInfo.FromCompact (bencodedStr.Span, AddressFamily));
                        break;

                    case ("failure reason"):
                        failureMessage = keypair.Value.ToString ()!;
                        break;

                    case ("warning message"):
                        warningMessage = keypair.Value.ToString ()!;
                        break;

                    default:
                        logger.InfoFormatted ("Unknown announce tag received: Key {0}  Value: {1}", keypair.Key, keypair.Value);
                        break;
                }
            }

            return new AnnounceResponse (
                state: TrackerState.Ok,
                peers: new Dictionary<InfoHash, IList<PeerInfo>> { { infoHash, peers } },
                minUpdateInterval: minUpdateInterval,
                updateInterval: updateInterval,
                scrapeInfo: new Dictionary<InfoHash, ScrapeInfo> { { infoHash, new ScrapeInfo (complete, downloaded, incomplete) } },
                warningMessage: warningMessage,
                failureMessage: failureMessage
            );
        }

        async ReusableTask<ScrapeResponse> ScrapeReceivedAsync (InfoHashes infoHashes, HttpResponseMessage response)
        {
            await new ThreadSwitcher ();

            BEncodedDictionary dict = await DecodeResponseAsync (response).ConfigureAwait (false);

            // FIXME: Log the failure?
            if (!dict.ContainsKey (FilesKey)) {
                return new ScrapeResponse (TrackerState.Ok, warningMessage: "Tracker did not have data for this torrent");
            }

            var files = (BEncodedDictionary) dict[FilesKey];
            if (files.Count != 1)
                throw new TrackerException ("The scrape response contained unexpected data");

            var results = new Dictionary<InfoHash, ScrapeInfo> ();
            foreach (var infoHash in new[] { infoHashes.V1!, infoHashes.V2! }.Where (t => t != null)) {
                var d = (BEncodedDictionary) files[BEncodedString.FromMemory (infoHash.Truncate ()!.AsMemory ())];
                int complete = 0, downloaded = 0, incomplete = 0;
                foreach (KeyValuePair<BEncodedString, BEncodedValue> kp in d) {
                    switch (kp.Key.ToString ()) {
                        case "complete":
                            complete = (int) ((BEncodedNumber) kp.Value).Number;
                            break;

                        case "downloaded":
                            downloaded = (int) ((BEncodedNumber) kp.Value).Number;
                            break;

                        case "incomplete":
                            incomplete = (int) ((BEncodedNumber) kp.Value).Number;
                            break;

                        default:
                            logger.InfoFormatted ("Unknown scrape tag received: Key {0}  Value {1}", kp.Key, kp.Value);
                            break;
                    }
                }
                results[infoHash] = new ScrapeInfo (complete: complete, downloaded: downloaded, incomplete: incomplete);
            }

            return new ScrapeResponse (TrackerState.Ok, results);
        }

        public override string ToString ()
        {
            return Uri.ToString ();
        }
    }
}
