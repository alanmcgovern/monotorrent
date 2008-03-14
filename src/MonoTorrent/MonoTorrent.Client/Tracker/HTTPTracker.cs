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
using System.Net;
using System.Threading;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Web;

namespace MonoTorrent.Client.Tracker
{
    /// <summary>
    /// Class representing an instance of a Tracker
    /// </summary>
    public class HTTPTracker : Tracker
    {
        private static readonly BEncodedString CustomErrorKey = (BEncodedString)"custom error";

        /// <summary>
        /// The announce URL for this tracker
        /// </summary>
        private Uri announceUrl;

        /// <summary>
        /// The Scrape URL for this tracker
        /// </summary>
        private Uri scrapeUrl;

        public HTTPTracker(Uri announceUrl)
            : base()
        {
            this.announceUrl = announceUrl;
            int index = announceUrl.OriginalString.LastIndexOf('/');
            string part = (index + 9 <= announceUrl.OriginalString.Length) ? announceUrl.OriginalString.Substring(index + 1, 8) : "";
            if (part.Equals("announce", StringComparison.OrdinalIgnoreCase))
            {
                CanScrape = true;
                Regex r = new Regex("announce");
                this.scrapeUrl = new Uri(r.Replace(announceUrl.OriginalString, "scrape", 1, index));
            }
        }

        public override WaitHandle Scrape(ScrapeParameters parameters)
        {
            WaitHandle h = null;
            HttpWebRequest request;
            string url = scrapeUrl.OriginalString;

            // If set to false, you could retrieve scrape data for *all* torrents hosted by the tracker. I see no practical use
            // at the moment, so i've removed the ability to set this to false.
            if (true)
            {
                if (url.IndexOf('?') == -1)
                    url += "?info_hash=" + HttpUtility.UrlEncode(parameters.InfoHash);
                else
                    url += "&info_hash=" + HttpUtility.UrlEncode(parameters.InfoHash);
            }
            request = (HttpWebRequest)HttpWebRequest.Create(url);
            parameters.Id.Request = request;
            UpdateState(TrackerState.Scraping);
            try
            {
                h = request.BeginGetResponse(ScrapeReceived, parameters.Id).AsyncWaitHandle;
            }
            catch (Exception ex)
            {
                h = new ManualResetEvent(true);
                Logger.Log(null, "Httptracker - Could not initiate scrape: {0}", ex);
            }
            return h;
        }

        public override WaitHandle Announce(AnnounceParameters parameters)
        {
            WaitHandle h = null;
            string announceString = CreateAnnounceString(parameters);
            LastUpdated = DateTime.Now;
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(announceString);
            request.Proxy = new WebProxy();   // If i don't do this, i can't run the webrequest. It's wierd.
            parameters.Id.Request = request;

            UpdateState(TrackerState.Announcing);
            try
            {
                h = request.BeginGetResponse(AnnounceReceived, parameters.Id).AsyncWaitHandle;
            }
            catch (Exception ex)
            {
                BEncodedDictionary d = new BEncodedDictionary();
                d.Add(CustomErrorKey, (BEncodedString)("Could not initiate announce request: " + ex.Message));
                ProcessResponse(d, parameters.Id);

                h = new ManualResetEvent(true);
            }
            return h;
        }



        protected string CreateAnnounceString(AnnounceParameters parameters)
        {
            StringBuilder sb = new StringBuilder(256);

            base.UpdateSucceeded = true;        // If the update ends up failing, reset this to false.
            //base.LastUpdated = DateTime.Now;
            // FIXME: This method should be tidied up. I don't like the way it current works
            sb.Append(this.announceUrl);
            sb.Append((this.announceUrl.OriginalString.IndexOf('?') == -1) ? '?' : '&');
            sb.Append("info_hash=");
            sb.Append(HttpUtility.UrlEncode(parameters.Infohash));
            sb.Append("&peer_id=");
            sb.Append(parameters.PeerId);
            sb.Append("&port=");
            sb.Append(parameters.Port);
            if (ClientEngine.SupportsEncryption)
                sb.Append("&supportcrypto=1");
            if (parameters.RequireEncryption && ClientEngine.SupportsEncryption)
                sb.Append("&requirecrypto=1");
            sb.Append("&uploaded=");
            sb.Append(parameters.BytesUploaded);
            sb.Append("&downloaded=");
            sb.Append(parameters.BytesDownloaded);
            sb.Append("&left=");
            sb.Append(parameters.BytesLeft);
            sb.Append("&compact=1");    // Always use compact response
            sb.Append("&numwant=");
            sb.Append(100);
            sb.Append("&key=");  // The 'key' protocol, used as a kind of 'password'. Must be the same between announces
            sb.Append(Key);
            if (parameters.Ipaddress != null)
            {
                sb.Append("&ip=");
                sb.Append(parameters.Ipaddress);
            }

            // If we have not successfully sent the started event to this tier, override the passed in started event
            // Otherwise append the event if it is not "none"
            if (!parameters.Id.Tracker.Tier.SentStartedEvent)
            {
                sb.Append("&event=started");
                parameters.Id.Tracker.Tier.SendingStartedEvent = true;
            }
            else if (parameters.ClientEvent != TorrentEvent.None)
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



        /// <summary>
        /// Decodes the response from a HTTPWebRequest
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private BEncodedDictionary DecodeResponse(IAsyncResult result)
        {
            int bytesRead = 0;
            int totalRead = 0;
            byte[] buffer = new byte[2048];
            HttpWebResponse response;
            TrackerConnectionID id = (TrackerConnectionID)result.AsyncState;

            try
            {
                response = (HttpWebResponse)((HttpWebRequest)id.Request).EndGetResponse(result);

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
            catch (WebException)
            {
                BEncodedDictionary dict = new BEncodedDictionary();
                dict.Add("custom error", (BEncodedString)"The tracker could not be contacted");
                return dict;
            }
            catch (BEncodingException)
            {
                BEncodedDictionary dict = new BEncodedDictionary();
                dict.Add("custom error", (BEncodedString)"The tracker returned an invalid or incomplete response");
                return dict;
            }
        }


        /// <summary>
        /// Called as part of the Async SendUpdate reponse
        /// </summary>
        /// <param name="result"></param>
        private void AnnounceReceived(IAsyncResult result)
        {
            TrackerConnectionID id = (TrackerConnectionID)result.AsyncState;
            BEncodedDictionary dict = DecodeResponse(result);
            ProcessResponse(dict, id);
        }

        private void ProcessResponse(BEncodedDictionary dict, TrackerConnectionID id)
        {
            AnnounceResponseEventArgs args = new AnnounceResponseEventArgs(id);

            UpdateSucceeded = !dict.ContainsKey(CustomErrorKey);
            if (!UpdateSucceeded)
            {
                FailureMessage = dict[CustomErrorKey].ToString();
                UpdateState(TrackerState.AnnouncingFailed);
            }
            else
            {
                if (id.Tracker.Tier.SendingStartedEvent)
                    id.Tracker.Tier.SentStartedEvent = true;
                
                HandleAnnounce(dict, args);
                UpdateState(TrackerState.AnnounceSuccessful);
            }

            id.Tracker.Tier.SendingStartedEvent = false;
            args.Successful = UpdateSucceeded;
            RaiseAnnounceComplete(args);
        }

        /// <summary>
        /// Handles the parsing of the dictionary when an announce result has been received
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dict"></param>
        private void HandleAnnounce(BEncodedDictionary dict, AnnounceResponseEventArgs args)
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
                        MinUpdateInterval = int.Parse(keypair.Value.ToString());
                        break;

                    case ("interval"):
                        UpdateInterval = int.Parse(keypair.Value.ToString());
                        break;

                    case ("peers"):
                        if (keypair.Value is BEncodedList)          // Non-compact response
                            args.Peers.AddRange(Peer.Decode((BEncodedList)keypair.Value));
                        else if (keypair.Value is BEncodedString)   // Compact response
                            args.Peers.AddRange(Peer.Decode((BEncodedString)keypair.Value));
                        break;

                    case ("failure reason"):
                        FailureMessage = keypair.Value.ToString();
                        args.Successful = false;
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void ScrapeReceived(IAsyncResult result)
        {
            BEncodedDictionary d;
            TrackerConnectionID id = (TrackerConnectionID)result.AsyncState;
            BEncodedDictionary dict = DecodeResponse(result);

            bool successful = !dict.ContainsKey("custom error");
            ScrapeResponseEventArgs args = new ScrapeResponseEventArgs(this, successful);

            if (!successful)
            {
                FailureMessage = dict["custom error"].ToString();
                UpdateState(TrackerState.ScrapingFailed);
            }

            else if (!dict.ContainsKey("files"))
            {
                args.Successful = false;
                UpdateState(TrackerState.ScrapingFailed);
            }

            else
            {
                BEncodedDictionary files = (BEncodedDictionary)dict["files"];
                foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in files)
                {
                    d = (BEncodedDictionary)keypair.Value;
                    foreach (KeyValuePair<BEncodedString, BEncodedValue> kp in d)
                    {
                        switch (kp.Key.ToString())
                        {
                            case ("complete"):
                                Complete = Convert.ToInt32(kp.Value.ToString());
                                break;

                            case ("downloaded"):
                                Downloaded = Convert.ToInt32(kp.Value.ToString());
                                break;

                            case ("incomplete"):
                                Incomplete = Convert.ToInt32(kp.Value.ToString());
                                break;

                            default:
                                Logger.Log(null, "HttpTracker - Unknown scrape tag received: Key {0}  Value {1}", kp.Key.ToString(), kp.Value.ToString());
                                break;
                        }
                    }
                }

                UpdateState(TrackerState.ScrapeSuccessful);
            }

            RaiseScrapeComplete(args);
        }


        public override bool Equals(object obj)
        {
            HTTPTracker tracker = obj as HTTPTracker;
            if (tracker == null)
                return false;

            // If the announce URL matches, then CanScrape and the scrape URL must match too
            return (this.announceUrl == tracker.announceUrl);
        }


        public override int GetHashCode()
        {
            return this.announceUrl.GetHashCode();
        }


        public override string ToString()
        {
            return this.announceUrl.OriginalString;
        }
    }
}
