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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Tracker
{
    class HTTPTracker : Tracker
    {
        static readonly Random random = new Random ();
        static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds (10);

        internal BEncodedString TrackerId { get; set; }

        internal BEncodedString Key { get; set; }

        internal TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;

        internal Uri ScrapeUri { get; }

        public HTTPTracker (Uri announceUrl)
            : base (announceUrl)
        {
            string uri = announceUrl.OriginalString;
            if (uri.EndsWith ("/announce", StringComparison.OrdinalIgnoreCase))
                ScrapeUri = new Uri ($"{uri.Substring (0, uri.Length - "/announce".Length)}/scrape");
            else if (uri.EndsWith ("/announce/", StringComparison.OrdinalIgnoreCase))
                ScrapeUri = new Uri ($"{uri.Substring (0, uri.Length - "/announce/".Length)}/scrape/");

            CanAnnounce = true;
            CanScrape = ScrapeUri != null;
            Status = TrackerState.Unknown;

            // Use a random integer prefixed by our identifier.
            lock (random)
                Key = new BEncodedString ($"{VersionInfo.ClientVersion}-{random.Next (1, int.MaxValue)}");
        }

        protected override async Task<List<Peer>> DoAnnounceAsync (AnnounceParameters parameters)
        {
            // WebRequest.Create can be a comparatively slow operation as reported
            // by profiling. Switch this to the threadpool so the querying of default
            // proxies, and any DNS requests, are definitely not run on the main thread.
            await MainLoop.SwitchToThreadpool ();

            // Clear out previous failure state
            FailureMessage = "";
            WarningMessage = "";
            var peers = new List<Peer> ();

            Uri announceString = CreateAnnounceString (parameters);
            using var client = new HttpClient ();
            HttpResponseMessage response;
            using var cts = new CancellationTokenSource (RequestTimeout);
            try {
                Status = TrackerState.Connecting;
                response = await client.GetAsync (announceString, HttpCompletionOption.ResponseHeadersRead,  cts.Token);
            } catch (Exception ex) {
                Status = TrackerState.Offline;
                FailureMessage = "The tracker could not be contacted";
                throw new TrackerException (FailureMessage, ex);
            }

            try {
                using var responseRegistration = cts.Token.Register (() => response.Dispose ());
                using (response) {
                    peers = await AnnounceReceivedAsync (response).ConfigureAwait (false);
                    Status = TrackerState.Ok;
                    return peers;
                }
            } catch (Exception ex) {
                Status = TrackerState.InvalidResponse;
                FailureMessage = "The tracker returned an invalid or incomplete response";
                throw new TrackerException (FailureMessage, ex);
            }
        }

        protected override async Task DoScrapeAsync (ScrapeParameters parameters)
        {
            // WebRequest.Create can be a comparatively slow operation as reported
            // by profiling. Switch this to the threadpool so the querying of default
            // proxies, and any DNS requests, are definitely not run on the main thread.
            await MainLoop.SwitchToThreadpool ();

            string url = ScrapeUri.OriginalString;
            // If you want to scrape the tracker for *all* torrents, don't append the info_hash.
            if (url.IndexOf ('?') == -1)
                url += $"?info_hash={parameters.InfoHash.UrlEncode ()}";
            else
                url += $"&info_hash={parameters.InfoHash.UrlEncode ()}";

            using var client = new HttpClient ();

            HttpResponseMessage response;
            using var cts = new CancellationTokenSource (RequestTimeout);
            try {
                response = await client.GetAsync (url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            } catch (Exception ex) {
                Status = TrackerState.Offline;
                FailureMessage = "The tracker could not be contacted";
                throw new TrackerException (FailureMessage, ex);
            }

            try {
                using var responseRegistration = cts.Token.Register (() => response.Dispose ());
                using (response)
                    await ScrapeReceivedAsync (parameters.InfoHash, response).ConfigureAwait (false);
                Status = TrackerState.Ok;
            } catch (Exception ex) {
                Status = TrackerState.InvalidResponse;
                FailureMessage = "The tracker returned an invalid or incomplete response";
                throw new TrackerException (FailureMessage, ex);
            }
        }

        Uri CreateAnnounceString (AnnounceParameters parameters)
        {
            var b = new UriQueryBuilder (Uri);
            b.Add ("info_hash", parameters.InfoHash.UrlEncode ())
             .Add ("peer_id", parameters.PeerId.UrlEncode ())
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
            if (!b.Contains ("key") && Key != null)
                b.Add ("key", Key.UrlEncode ());
            if (!string.IsNullOrEmpty (parameters.IPAddress))
                b.Add ("ip", parameters.IPAddress);

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
                b.Add ("trackerid", TrackerId.UrlEncode ());

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

        public override bool Equals (object obj)
        {
            if (!(obj is HTTPTracker tracker))
                return false;

            // If the announce URL matches, then CanScrape and the scrape URL must match too
            return (Uri.Equals (tracker.Uri));
        }

        public override int GetHashCode ()
        {
            return Uri.GetHashCode ();
        }

        async Task<List<Peer>> AnnounceReceivedAsync (HttpResponseMessage response)
        {
            await MainLoop.SwitchToThreadpool ();

            BEncodedDictionary dict = await DecodeResponseAsync (response).ConfigureAwait (false);
            var peers = new List<Peer> ();
            HandleAnnounce (dict, peers);
            Status = TrackerState.Ok;
            return peers;
        }

        void HandleAnnounce (BEncodedDictionary dict, List<Peer> peers)
        {
            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dict) {
                switch (keypair.Key.Text) {
                    case ("complete"):
                        Complete = Convert.ToInt32 (keypair.Value.ToString ());
                        break;

                    case ("incomplete"):
                        Incomplete = Convert.ToInt32 (keypair.Value.ToString ());
                        break;

                    case ("downloaded"):
                        Downloaded = Convert.ToInt32 (keypair.Value.ToString ());
                        break;

                    case ("tracker id"):
                        TrackerId = (BEncodedString) keypair.Value;
                        break;

                    case ("min interval"):
                        MinUpdateInterval = TimeSpan.FromSeconds (int.Parse (keypair.Value.ToString ()));
                        break;

                    case ("interval"):
                        UpdateInterval = TimeSpan.FromSeconds (int.Parse (keypair.Value.ToString ()));
                        break;

                    case ("peers"):
                        if (keypair.Value is BEncodedList)          // Non-compact response
                            peers.AddRange (Peer.Decode ((BEncodedList) keypair.Value));
                        else if (keypair.Value is BEncodedString)   // Compact response
                            peers.AddRange (Peer.Decode ((BEncodedString) keypair.Value));
                        break;

                    case ("failure reason"):
                        FailureMessage = keypair.Value.ToString ();
                        break;

                    case ("warning message"):
                        WarningMessage = keypair.Value.ToString ();
                        break;

                    default:
                        Logger.Log (null, "HttpTracker - Unknown announce tag received: Key {0}  Value: {1}", keypair.Key.ToString (), keypair.Value.ToString ());
                        break;
                }
            }
        }

        async Task ScrapeReceivedAsync (InfoHash infoHash, HttpResponseMessage response)
        {
            await MainLoop.SwitchToThreadpool ();

            BEncodedDictionary dict = await DecodeResponseAsync (response).ConfigureAwait (false);

            // FIXME: Log the failure?
            if (!dict.ContainsKey ("files")) {
                return;
            }
            var files = (BEncodedDictionary) dict["files"];
            if (files.Count != 1)
                throw new TrackerException ("The scrape response contained unexpected data");

            var d = (BEncodedDictionary) files[new BEncodedString (infoHash.Hash)];
            foreach (KeyValuePair<BEncodedString, BEncodedValue> kp in d) {
                switch (kp.Key.ToString ()) {
                    case ("complete"):
                        Complete = (int) ((BEncodedNumber) kp.Value).Number;
                        break;

                    case ("downloaded"):
                        Downloaded = (int) ((BEncodedNumber) kp.Value).Number;
                        break;

                    case ("incomplete"):
                        Incomplete = (int) ((BEncodedNumber) kp.Value).Number;
                        break;

                    default:
                        Logger.Log (null, "HttpTracker - Unknown scrape tag received: Key {0}  Value {1}", kp.Key.ToString (), kp.Value.ToString ());
                        break;
                }
            }
        }

        public override string ToString ()
        {
            return Uri.ToString ();
        }
    }
}
