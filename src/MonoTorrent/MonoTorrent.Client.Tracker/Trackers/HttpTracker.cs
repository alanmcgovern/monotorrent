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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Tracker
{
    class HttpTracker : TrackerBase
    {
        static readonly Random random = new Random ();
        static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds (10);

        internal BEncodedString TrackerId { get; set; }

        internal BEncodedString Key { get; set; }

        internal TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;

        internal Uri ScrapeUri { get; }

        static string TryRemoveSuffix (string suffix, string s) =>
            s.EndsWith (suffix, StringComparison.Ordinal) ? s.Substring (0, s.Length - suffix.Length) : null;

        public HttpTracker (Uri announceUrl)
            : base (announceUrl)
        {
            var uriStr = announceUrl.OriginalString;
            var uriStrWithoutEndSlash = TryRemoveSuffix ("/", uriStr);
            var shouldAddEndSlash = uriStrWithoutEndSlash != null;
            var announceUriBaseStr = TryRemoveSuffix ("/announce", uriStrWithoutEndSlash ?? uriStr);
            ScrapeUri = announceUriBaseStr == null
                ? null
                : new Uri (announceUriBaseStr + "/scrape" + (shouldAddEndSlash ? "/" : ""));

            CanAnnounce = true;
            CanScrape = ScrapeUri != null;

            // Use a random integer prefixed by our identifier.
            lock (random)
                Key = new BEncodedString ($"{VersionInfo.ClientVersion}-{random.Next (1, int.MaxValue)}");
        }

        protected override async Task<List<Peer>> DoAnnounceAsync (AnnounceParameters parameters)
        {
            // Clear out previous failure state
            FailureMessage = "";
            WarningMessage = "";

            var announceString = CreateAnnounceString (parameters);
            var request = (HttpWebRequest) WebRequest.Create (announceString);
            request.UserAgent = VersionInfo.ClientVersion;
            request.Proxy = new WebProxy ();   // If i don't do this, i can't run the webrequest. It's wierd.

            WebResponse response;
            using var cts = new CancellationTokenSource (RequestTimeout);
            using var registration = cts.Token.Register (() => request.Abort ());

            try {
                response = await request.GetResponseAsync ().ConfigureAwait (false);
            } catch (Exception ex) {
                Status = TrackerState.Offline;
                FailureMessage = "The tracker could not be contacted";
                throw new TrackerException (FailureMessage, ex);
            }

            try {
                using var responseRegistration = cts.Token.Register (() => response.Close ());
                using (response) {
                    var peers = await AnnounceReceivedAsync (response).ConfigureAwait (false);
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
            string url = ScrapeUri.OriginalString;
            // If you want to scrape the tracker for *all* torrents, don't append the info_hash.
            if (url.IndexOf ('?') == -1)
                url += "?info_hash=" + parameters.InfoHash.UrlEncode ();
            else
                url += "&info_hash=" + parameters.InfoHash.UrlEncode ();

            var request = (HttpWebRequest) WebRequest.Create (url);
            request.UserAgent = VersionInfo.ClientVersion;
            request.Proxy = new WebProxy ();

            using var cts = new CancellationTokenSource (RequestTimeout);
            using var registration = cts.Token.Register (() => request.Abort ());

            WebResponse response;
            try {
                response = await request.GetResponseAsync ().ConfigureAwait (false);
            } catch (Exception ex) {
                Status = TrackerState.Offline;
                FailureMessage = "The tracker could not be contacted";
                throw new TrackerException (FailureMessage, ex);
            }

            try {
                using var responseRegistration = cts.Token.Register (() => response.Close ());
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
            UriQueryBuilder b = new UriQueryBuilder (Uri);
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

        static async Task<BEncodedDictionary> DecodeResponseAsync (WebResponse response)
        {
            var ms = new MemoryStream ();
            using (var reader = response.GetResponseStream ()) {
                await reader.CopyToAsync (ms);
            }

            ms.Seek (0, SeekOrigin.Begin);
            return (BEncodedDictionary) BEncodedValue.Decode (ms);
        }

        // If the announce URL matches, then CanScrape and the scrape URL must match too
        public override bool Equals (object obj) =>
            obj is HttpTracker trackerBase && Uri.Equals (trackerBase.Uri);

        public override int GetHashCode () => Uri.GetHashCode ();

        async Task<List<Peer>> AnnounceReceivedAsync (WebResponse response)
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
                        if (keypair.Value is BEncodedList list)          // Non-compact response
                            peers.AddRange (Peer.Decode (list));
                        else if (keypair.Value is BEncodedString @string)   // Compact response
                            peers.AddRange (Peer.Decode (@string));
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

        async Task ScrapeReceivedAsync (InfoHash infoHash, WebResponse response)
        {
            await MainLoop.SwitchToThreadpool ();

            BEncodedDictionary dict = await DecodeResponseAsync (response).ConfigureAwait (false);

            // FIXME: Log the failure?
            if (!dict.ContainsKey ("files")) {
                return;
            }
            BEncodedDictionary files = (BEncodedDictionary) dict["files"];
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

        public override string ToString () => Uri.ToString ();
    }
}
