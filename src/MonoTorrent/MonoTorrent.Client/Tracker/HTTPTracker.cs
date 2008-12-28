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
using System.Text;
using MonoTorrent.BEncoding;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;
using MonoTorrent.Common;
using System.IO;

namespace MonoTorrent.Client.Tracker
{
    public class HTTPTracker : Tracker
    {
        static Random random = new Random();
        static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        Uri scrapeUrl;
        string key;
        string TrackerId;

        public string Key
        {
            get { return key; }
            private set { key = value; }
        }

        public Uri ScrapeUri
        {
            get { return scrapeUrl; }
        }

        public HTTPTracker(Uri announceUrl)
            : base(announceUrl)
        {
            CanAnnounce = true;
            int index = announceUrl.OriginalString.LastIndexOf('/');
            string part = (index + 9 <= announceUrl.OriginalString.Length) ? announceUrl.OriginalString.Substring(index + 1, 8) : "";
            if (part.Equals("announce", StringComparison.OrdinalIgnoreCase))
            {
                CanScrape = true;
                Regex r = new Regex("announce");
                this.scrapeUrl = new Uri(r.Replace(announceUrl.OriginalString, "scrape", 1, index));
            }

            byte[] passwordKey = new byte[8];
            lock (random)
                random.NextBytes(passwordKey);
            Key = HttpUtility.UrlEncode(passwordKey);
        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
            try
            {
                string announceString = CreateAnnounceString(parameters);
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(announceString);
                request.Proxy = new WebProxy();   // If i don't do this, i can't run the webrequest. It's wierd.
                RaiseBeforeAnnounce();
                BeginRequest(request, AnnounceReceived, new object[] { request, state });
            }
            catch (Exception ex)
            {
                Status = TrackerState.Offline;
                FailureMessage = ("Could not initiate announce request: " + ex.Message);
                RaiseAnnounceComplete(new AnnounceResponseEventArgs(this, state, false));
            }
        }

        void BeginRequest(WebRequest request, AsyncCallback callback, object state)
        {
            IAsyncResult result = request.BeginGetResponse(callback, state);
            ClientEngine.MainLoop.QueueTimeout(RequestTimeout, delegate
            {
                if (!result.IsCompleted)
                    request.Abort();
                return false;
            });
        }

        void AnnounceReceived(IAsyncResult result)
        {
            FailureMessage = "";
            WarningMessage = "";
            object[] stateOb = (object[])result.AsyncState;
            WebRequest request = (WebRequest)stateOb[0];
            object state = stateOb[1];
            List<Peer> peers = new List<Peer>();
            try
            {
                BEncodedDictionary dict = DecodeResponse(request, result);
                HandleAnnounce(dict, peers);
                Status = TrackerState.Ok;
            }
            catch (WebException)
            {
                Status = TrackerState.Offline;
                FailureMessage = "The tracker could not be contacted";
            }
            catch (BEncodingException)
            {
                Status = TrackerState.InvalidResponse;
                FailureMessage = "The tracker returned an invalid or incomplete response";
            }
            finally
            {
                RaiseAnnounceComplete(new AnnounceResponseEventArgs(this, state, string.IsNullOrEmpty(FailureMessage), peers));
            }
        }

        string CreateAnnounceString(AnnounceParameters parameters)
        {
            StringBuilder sb = new StringBuilder(256);

            //base.LastUpdated = DateTime.Now;
            // FIXME: This method should be tidied up. I don't like the way it current works
            sb.Append(Uri);
            sb.Append(Uri.OriginalString.Contains("?") ? '&' : '?');
            sb.Append("info_hash=");
            sb.Append(HttpUtility.UrlEncode(parameters.Infohash));
            sb.Append("&peer_id=");
            sb.Append(parameters.PeerId);
            sb.Append("&port=");
            sb.Append(parameters.Port);
            if (parameters.SupportsEncryption)
                sb.Append("&supportcrypto=1");
            if (parameters.RequireEncryption)
                sb.Append("&requirecrypto=1");
            sb.Append("&uploaded=");
            sb.Append(parameters.BytesUploaded);
            sb.Append("&downloaded=");
            sb.Append(parameters.BytesDownloaded);
            sb.Append("&left=");
            sb.Append(parameters.BytesLeft);
            sb.Append("&compact=1");    // Always use compact response
            sb.Append("&numwant=");
            sb.Append(parameters.BytesLeft == 0 ? 0 : 100);
            if (!Uri.Query.Contains("key="))
            {
                sb.Append("&key=");  // The 'key' protocol, used as a kind of 'password'. Must be the same between announces
                sb.Append(Key);
            }
            if (parameters.Ipaddress != null)
            {
                sb.Append("&ip=");
                sb.Append(parameters.Ipaddress);
            }

            // If we have not successfully sent the started event to this tier, override the passed in started event
            // Otherwise append the event if it is not "none"
            //if (!parameters.Id.Tracker.Tier.SentStartedEvent)
            //{
            //    sb.Append("&event=started");
            //    parameters.Id.Tracker.Tier.SendingStartedEvent = true;
            //}
            if (parameters.ClientEvent != TorrentEvent.None)
            {
                sb.Append("&event=");
                sb.Append(parameters.ClientEvent.ToString().ToLower());
            }

            if (!string.IsNullOrEmpty(TrackerId))
            {
                sb.Append("&trackerid=");
                sb.Append(TrackerId);
            }

            return sb.ToString();
        }

        BEncodedDictionary DecodeResponse(WebRequest request, IAsyncResult result)
        {
            int bytesRead = 0;
            int totalRead = 0;
            byte[] buffer = new byte[2048];

            WebResponse response = request.EndGetResponse(result);
            using (MemoryStream dataStream = new MemoryStream(response.ContentLength > 0 ? (int)response.ContentLength : 256))
            {

                using (BinaryReader reader = new BinaryReader(response.GetResponseStream()))
                {
                    // If there is a ContentLength, use that to decide how much we read.
                    if (response.ContentLength > 0)
                    {
                        while (totalRead < response.ContentLength)
                        {
                            bytesRead = reader.Read(buffer, 0, buffer.Length);
                            dataStream.Write(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                        }
                    }



                    else    // A compact response doesn't always have a content length, so we
                    {       // just have to keep reading until we think we have everything.
                        while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                            dataStream.Write(buffer, 0, bytesRead);
                    }
                }
                response.Close();
                dataStream.Seek(0, SeekOrigin.Begin);
                return (BEncodedDictionary)BEncodedValue.Decode(dataStream);
            }
        }

        public override bool Equals(object obj)
        {
            HTTPTracker tracker = obj as HTTPTracker;
            if (tracker == null)
                return false;

            // If the announce URL matches, then CanScrape and the scrape URL must match too
            return (Uri.Equals(tracker.Uri));
        }

        public override int GetHashCode()
        {
            return Uri.GetHashCode();
        }

        void HandleAnnounce(BEncodedDictionary dict, List<Peer> peers)
        {
            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dict)
            {
                switch (keypair.Key.Text)
                {
                    case ("complete"):
                        Complete = Convert.ToInt32(keypair.Value.ToString());
                        break;

                    case ("incomplete"):
                        Incomplete = Convert.ToInt32(keypair.Value.ToString());
                        break;

                    case ("downloaded"):
                        Downloaded = Convert.ToInt32(keypair.Value.ToString());
                        break;

                    case ("tracker id"):
                        TrackerId = keypair.Value.ToString();
                        break;

                    case ("min interval"):
                        MinUpdateInterval = TimeSpan.FromSeconds(int.Parse(keypair.Value.ToString()));
                        break;

                    case ("interval"):
                        UpdateInterval = TimeSpan.FromSeconds(int.Parse(keypair.Value.ToString()));
                        break;

                    case ("peers"):
                        if (keypair.Value is BEncodedList)          // Non-compact response
                            peers.AddRange(Peer.Decode((BEncodedList)keypair.Value));
                        else if (keypair.Value is BEncodedString)   // Compact response
                            peers.AddRange(Peer.Decode((BEncodedString)keypair.Value));
                        break;

                    case ("failure reason"):
                        FailureMessage = keypair.Value.ToString();
                        break;

                    case ("warning message"):
                        WarningMessage = keypair.Value.ToString();
                        break;

                    default:
                        Logger.Log(null, "HttpTracker - Unknown announce tag received: Key {0}  Value: {1}", keypair.Key.ToString(), keypair.Value.ToString());
                        break;
                }
            }
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
            try
            {
                string url = scrapeUrl.OriginalString;

                // If you want to scrape the tracker for *all* torrents, don't append the info_hash.
                if (url.IndexOf('?') == -1)
                    url += "?info_hash=" + HttpUtility.UrlEncode(parameters.InfoHash);
                else
                    url += "&info_hash=" + HttpUtility.UrlEncode(parameters.InfoHash);

                WebRequest request = WebRequest.Create(url);
                BeginRequest(request, ScrapeReceived, new object[] { request, state });
            }
            catch
            {
                RaiseScrapeComplete(new ScrapeResponseEventArgs(this, state, false));
            }
        }

        void ScrapeReceived(IAsyncResult result)
        {
            string message = "";
            object[] stateOb = (object[])result.AsyncState;
            WebRequest request = (WebRequest)stateOb[0];
            object state = stateOb[1];

            try
            {
                BEncodedDictionary d;
                BEncodedDictionary dict = DecodeResponse(request, result);

                // FIXME: Log the failure?
                if (!dict.ContainsKey("files"))
                {
                    message = "Response contained no data";
                    return;
                }
                BEncodedDictionary files = (BEncodedDictionary)dict["files"];
                foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in files)
                {
                    d = (BEncodedDictionary)keypair.Value;
                    foreach (KeyValuePair<BEncodedString, BEncodedValue> kp in d)
                    {
                        switch (kp.Key.ToString())
                        {
                            case ("complete"):
                                Complete = (int)((BEncodedNumber)kp.Value).Number;
                                break;

                            case ("downloaded"):
                                Downloaded = (int)((BEncodedNumber)kp.Value).Number;
                                break;

                            case ("incomplete"):
                                Incomplete = (int)((BEncodedNumber)kp.Value).Number;
                                break;

                            default:
                                Logger.Log(null, "HttpTracker - Unknown scrape tag received: Key {0}  Value {1}", kp.Key.ToString(), kp.Value.ToString());
                                break;
                        }
                    }
                }
            }
            catch (WebException)
            {
                message = "The tracker could not be contacted";
            }
            catch (BEncodingException)
            {
                message = "The tracker returned an invalid or incomplete response";
            }
            finally
            {
                RaiseScrapeComplete(new ScrapeResponseEventArgs(this, state, string.IsNullOrEmpty(message)));
            }
        }

        public override string ToString()
        {
            return Uri.ToString();
        }
    }
}
