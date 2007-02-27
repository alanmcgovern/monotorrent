//
// TrackerManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Text;
using System.Net;
using System.IO;
using MonoTorrent.Common;
using System.Collections.ObjectModel;
using System.Threading;
using System.Web;
using System.Diagnostics;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the connection to a tracker that an TorrentManager has
    /// </summary>
    public class TrackerManager
    {
        #region Events
        /// <summary>
        /// Event that's fired every time the state changes during a TrackerUpdate
        /// </summary>
        public event EventHandler<TrackerStateChangedEventArgs> OnTrackerStateChange;
        #endregion


        #region Member Variables
        private AsyncCallback announceReceived;
        private AsyncCallback scrapeReceived;
        private TorrentManager manager;


        /// <summary>
        /// The infohash for the torrent
        /// </summary>
        private string infoHash;


        /// <summary>
        /// True if the last update succeeded
        /// </summary>
        public bool UpdateSucceeded
        {
            get { return this.updateSucceeded; }
        }
        private bool updateSucceeded;


        /// <summary>
        /// The time the last tracker update was sent to any tracker
        /// </summary>
        public DateTime LastUpdated
        {
            get { return this.lastUpdated; }
        }
        private DateTime lastUpdated;


        /// <summary>
        /// The trackers available
        /// </summary>
        public Tracker[] Trackers
        {
            get { return this.trackers; }
        }
        private Tracker[] trackers;
        private int currentTrackerIndex;


        /// <summary>
        /// Returns the tracker that is currently being actively used by the engine.
        /// </summary>
        public Tracker CurrentTracker
        {
            get { return this.trackers[this.currentTrackerIndex]; } 
        }
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="manager">The TorrentManager to create the tracker connection for</param>
        public TrackerManager(TorrentManager manager)
        {
            this.manager = manager;
            this.currentTrackerIndex = 0;
            this.infoHash = HttpUtility.UrlEncode(manager.Torrent.InfoHash);

            this.announceReceived = new AsyncCallback(this.AnnounceReceived);
            this.scrapeReceived = new AsyncCallback(this.ScrapeReceived);

            // Check if this tracker supports scraping
            this.trackers = new Tracker[manager.Torrent.AnnounceUrls.Length];
            for (int i = 0; i < manager.Torrent.AnnounceUrls.Length; i++)
                this.trackers[i] = new Tracker(manager.Torrent.AnnounceUrls[i], this.announceReceived, this.scrapeReceived);
        }
        #endregion


        #region Methods
        /// <summary>
        /// Sends a status update to the tracker
        /// </summary>
        /// <param name="bytesDownloaded">The number of bytes downloaded since the last update</param>
        /// <param name="bytesUploaded">The number of bytes uploaded since the last update</param>
        /// <param name="bytesLeft">The number of bytes left to download</param>
        /// <param name="clientEvent">The Event (if any) that represents this update</param>
        public WaitHandle Announce(long bytesDownloaded, long bytesUploaded, long bytesLeft, TorrentEvent clientEvent)
        {
            this.updateSucceeded = true;
            this.lastUpdated = DateTime.Now;
            Tracker tracker = this.ChooseTracker();
            
            UpdateState(tracker, TrackerState.Announcing);
            WaitHandle handle = tracker.Announce(bytesDownloaded, bytesUploaded, bytesLeft, clientEvent, this.infoHash);
            return handle;
        }


        /// <summary>
        /// Called as part of the Async SendUpdate reponse
        /// </summary>
        /// <param name="result"></param>
        private void AnnounceReceived(IAsyncResult result)
        {
            BEncodedDictionary dict = DecodeResponse(result);
            TrackerConnectionID id = (TrackerConnectionID)result.AsyncState;

            if (dict.ContainsKey("custom error"))
            {
                id.Tracker.SendingStatedEvent = false;
                this.updateSucceeded = false;
                id.Tracker.UpdateSucceeded = false;
                id.Tracker.FailureMessage = dict["custom error"].ToString();
                UpdateState(id.Tracker, TrackerState.AnnouncingFailed);
            }
            else
            {
                if (id.Tracker.SendingStatedEvent)
                {
                    id.Tracker.SendingStatedEvent = false;
                    id.Tracker.StartedEventSentSuccessfully = true;
                }
                UpdateState(id.Tracker, TrackerState.AnnounceSuccessful);
                HandleAnnounce(id, dict);
            }
        }


        /// <summary>
        /// Handles the parsing of the dictionary when an announce result has been received
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dict"></param>
        private void HandleAnnounce(TrackerConnectionID id, BEncodedDictionary dict)
        {
            int peersAdded = 0;
            foreach (KeyValuePair<BEncodedString, IBEncodedValue> keypair in dict)
            {
                switch (keypair.Key.Text)
                {

                    case ("complete"):
                        id.Tracker.Complete = Convert.ToInt32(keypair.Value.ToString());
                        break;

                    case ("incomplete"):
                        id.Tracker.Incomplete = Convert.ToInt32(keypair.Value.ToString());
                        break;

                    case ("downloaded"):
                        id.Tracker.Downloaded = Convert.ToInt32(keypair.Value.ToString());
                        break;

                    case ("tracker id"):
                        id.Tracker.TrackerId = keypair.Value.ToString();
                        break;

                    case ("min interval"):
                        id.Tracker.MinUpdateInterval = int.Parse(keypair.Value.ToString());
                        break;

                    case ("interval"):
                        id.Tracker.UpdateInterval = int.Parse(keypair.Value.ToString());
                        break;

                    case ("peers"):
                        if (keypair.Value is BEncodedList)          // Non-compact response
                            peersAdded = this.manager.AddPeers(((BEncodedList)keypair.Value));
                        else if (keypair.Value is BEncodedString)   // Compact response
                            peersAdded = this.manager.AddPeers(((BEncodedString)keypair.Value).TextBytes);
                        break;

                    case ("failure reason"):
                        id.Tracker.FailureMessage = keypair.Value.ToString();
                        break;

                    case ("warning message"):
                        id.Tracker.WarningMessage = keypair.Value.ToString();
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine("Key: " + keypair.Key.ToString() + " Value: " + keypair.Value.ToString());
                        break;
                }
            }
        }



        /// <summary>
        /// Scrapes the tracker for peer information.
        /// </summary>
        /// <param name="requestSingle">True if you want scrape information for just the torrent in the TorrentManager. False if you want everything on the tracker</param>
        /// <returns></returns>
        public WaitHandle Scrape(bool requestSingle)
        {
            Tracker tracker = this.ChooseTracker();
            WaitHandle handle = tracker.Scrape(requestSingle, this.infoHash);

            UpdateState(tracker, TrackerState.Scraping);
            return handle;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void ScrapeReceived(IAsyncResult result)
        {
            BEncodedDictionary d;
            BEncodedDictionary dict = DecodeResponse(result);
            TrackerConnectionID id = (TrackerConnectionID)result.AsyncState;

            if (dict.ContainsKey("custom error"))
                return;

            foreach (KeyValuePair<BEncodedString, IBEncodedValue> keypair in dict)
            {
                d = (BEncodedDictionary)keypair.Value;
                foreach (KeyValuePair<BEncodedString, IBEncodedValue> kp in d)
                {
                    switch (kp.Key.ToString())
                    {
                        case ("complete"):
                            id.Tracker.Complete = Convert.ToInt32(keypair.Value.ToString());
                            break;

                        case ("downloaded"):
                            id.Tracker.Downloaded = Convert.ToInt32(keypair.Value.ToString());
                            break;

                        case ("incomplete"):
                            id.Tracker.Incomplete = Convert.ToInt32(keypair.Value.ToString());
                            break;

                        default:
                            System.Diagnostics.Trace.WriteLine("Key: " + keypair.Key.ToString() + " Value: " + keypair.Value.ToString());
                            break;
                    }
                }
            }
        }


        /// <summary>
        /// Wrapper method to call the OnStateChanged event correctly
        /// </summary>
        /// <param name="tracker"></param>
        /// <param name="newState"></param>
        internal void UpdateState(Tracker tracker, TrackerState newState)
        {
            if (tracker.State == newState)
                return;

            TrackerStateChangedEventArgs e = new TrackerStateChangedEventArgs(tracker, tracker.State, newState);
            tracker.State = newState;

            if (this.OnTrackerStateChange != null)
                this.OnTrackerStateChange(this.manager, e);
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
                response = (HttpWebResponse)id.Request.EndGetResponse(result);

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
                    return (BEncodedDictionary)BEncode.Decode(dataStream);
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
        /// If a tracker is unreachable, the next tracker is chosen from the list
        /// </summary>
        /// <returns></returns>
        private Tracker ChooseTracker()
        {
            if (!this.updateSucceeded)
            {
                this.currentTrackerIndex++;
                if (this.currentTrackerIndex == this.trackers.Length)
                    this.currentTrackerIndex = 0;
            }

            return this.trackers[this.currentTrackerIndex];
        }
        #endregion
    }
}
