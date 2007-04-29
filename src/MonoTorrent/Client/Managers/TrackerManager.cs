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
        /// Returns the tracker that is current in use by the engine
        /// </summary>
        public Tracker CurrentTracker
        {
            get { return this.trackerTiers[0].Trackers[0]; }
        }


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
        public TrackerTier[] TrackerTiers
        {
            get { return this.trackerTiers; }
        }
        private TrackerTier[] trackerTiers;
        private int currentTrackerTierIndex;

        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        /// <param name="manager">The TorrentManager to create the tracker connection for</param>
        public TrackerManager(TorrentManager manager, EngineSettings engineSettings)
        {
            this.manager = manager;
            this.currentTrackerTierIndex = 0;
            this.infoHash = HttpUtility.UrlEncode(manager.Torrent.InfoHash);

            this.announceReceived = new AsyncCallback(this.AnnounceReceived);
            this.scrapeReceived = new AsyncCallback(this.ScrapeReceived);

            // Check if this tracker supports scraping
            this.trackerTiers = new TrackerTier[manager.Torrent.AnnounceUrls.Count];
            for (int i = 0; i < manager.Torrent.AnnounceUrls.Count; i++)
                this.trackerTiers[i] = new TrackerTier(manager.Torrent.AnnounceUrls[i], this.announceReceived, this.scrapeReceived, engineSettings);
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
        internal WaitHandle Announce(TorrentEvent clientEvent)
        {
            return Announce(this.trackerTiers[0], this.trackerTiers[0].Trackers[0], clientEvent, true);
        }

#warning Should i always send TorrentEvent.None?
        public WaitHandle Announce(TrackerTier tier, Tracker tracker)
        {
            return Announce(tier, tracker, TorrentEvent.None, false);
        }

        private WaitHandle Announce(TrackerTier tier, Tracker tracker, TorrentEvent clientEvent, bool trySubsequent)
        {
            TrackerConnectionID id = new TrackerConnectionID(tier, tracker, trySubsequent, clientEvent, null);
            this.updateSucceeded = true;
            this.lastUpdated = DateTime.Now;
            UpdateState(tracker, TrackerState.Announcing);
            WaitHandle handle = tracker.Announce(this.manager.Monitor.DataBytesDownloaded,
                                                this.manager.Monitor.DataBytesUploaded,
                                                (long)((1 - this.manager.Bitfield.PercentComplete / 100.0) * this.manager.Torrent.Size),
                                                clientEvent, this.infoHash, id);
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
                id.TrackerTier.SendingStartedEvent = false;
                this.updateSucceeded = false;
                id.Tracker.UpdateSucceeded = false;
                id.Tracker.FailureMessage = dict["custom error"].ToString();
                UpdateState(id.Tracker, TrackerState.AnnouncingFailed);

                if (!id.TrySubsequent)
                    return;

                GetNextTracker(id.Tracker, out id.TrackerTier, out id.Tracker);
                if (id.TrackerTier == null || id.Tracker == null)
                    return;
                
                Announce(id.TrackerTier, id.Tracker, id.TorrentEvent, true);
            }
            else
            {
                ToolBox.Switch<Tracker>(id.TrackerTier.Trackers, 0, id.TrackerTier.IndexOf(id.Tracker));
                if (id.TrackerTier.SendingStartedEvent)
                {
                    id.TrackerTier.SendingStartedEvent = false;
                    id.TrackerTier.SentStartedEvent = true;
                }

                HandleAnnounce(id, dict);
                UpdateState(id.Tracker, TrackerState.AnnounceSuccessful);
            }
        }


        private void GetNextTracker(Tracker tracker, out TrackerTier trackerTier, out Tracker trackerReturn)
        {
            for (int i = 0; i < this.trackerTiers.Length; i++)
            {
                for (int j = 0; j < this.trackerTiers[i].Trackers.Length; j++)
                {
                    if (this.trackerTiers[i].Trackers[j] != tracker)
                        continue;

                    // If we are on the last tracker of this tier, check to see if there are more tiers
                    if (j == (this.trackerTiers[i].Trackers.Length - 1))
                    {
                        if (i == (this.trackerTiers.Length - 1))
                        {
                            trackerTier = null;
                            trackerReturn = null;
                            return;
                        }

                        trackerTier = this.trackerTiers[i + 1];
                        trackerReturn = trackerTier.Trackers[0];
                        return;
                    }

                    trackerTier = this.trackerTiers[i];
                    trackerReturn = trackerTier.Trackers[j + 1];
                    return;
                }
            }

            trackerTier= null;
            trackerReturn = null;
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
                        Logger.Log("Key: " + keypair.Key.ToString() + " Value: " + keypair.Value.ToString());
                        break;
                }
            }
        }


        /// <summary>
        /// Scrapes the first tracker for peer information.
        /// </summary>
        /// <param name="requestSingle">True if you want scrape information for just the torrent in the TorrentManager. False if you want everything on the tracker</param>
        /// <returns></returns>
        public WaitHandle Scrape()
        {
            return Scrape(this.trackerTiers[0], this.trackerTiers[0].Trackers[0], true);
        }


        /// <summary>
        /// Scrapes the specified tracker for peer information.
        /// </summary>
        /// <param name="requestSingle">True if you want scrape information for just the torrent in the TorrentManager. False if you want everything on the tracker</param>
        /// <returns></returns>
        public WaitHandle Scrape(TrackerTier tier, Tracker tracker)
        {
            return Scrape(tier, tracker, false);
        }

        private WaitHandle Scrape(TrackerTier tier, Tracker tracker, bool trySubsequent)
        {
            TrackerConnectionID id = new TrackerConnectionID(tier, tracker, trySubsequent, TorrentEvent.None, null);
            WaitHandle handle = tracker.Scrape(this.infoHash, id);
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
            {
                id.Tracker.FailureMessage = dict["custom error"].ToString();
                UpdateState(id.Tracker, TrackerState.ScrapingFailed);

                if (!id.TrySubsequent)
                    return;

                do
                {
                    GetNextTracker(id.Tracker, out id.TrackerTier, out id.Tracker);
                } while (id.Tracker != null && id.TrackerTier != null && !id.Tracker.CanScrape);

                if (id.TrackerTier == null || id.Tracker == null)
                    return;

                Scrape(id.TrackerTier, id.Tracker, true);
            }
            if (!dict.ContainsKey("files"))
                return;

            BEncodedDictionary files = (BEncodedDictionary)dict["files"];
            foreach (KeyValuePair<BEncodedString, IBEncodedValue> keypair in files)
            {
                d = (BEncodedDictionary)keypair.Value;
                foreach (KeyValuePair<BEncodedString, IBEncodedValue> kp in d)
                {
                    switch (kp.Key.ToString())
                    {
                        case ("complete"):
                            id.Tracker.Complete = Convert.ToInt32(kp.Value.ToString());
                            break;

                        case ("downloaded"):
                            id.Tracker.Downloaded = Convert.ToInt32(kp.Value.ToString());
                            break;

                        case ("incomplete"):
                            id.Tracker.Incomplete = Convert.ToInt32(kp.Value.ToString());
                            break;

                        default:
                            Logger.Log("Key: " + kp.Key.ToString() + " Value: " + kp.Value.ToString());
                            break;
                    }
                }
            }

            UpdateState(id.Tracker, TrackerState.ScrapeSuccessful);
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

            RaiseTrackerStateChange(e);
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


		internal void RaiseTrackerStateChange(TrackerStateChangedEventArgs e)
		{
			if (this.OnTrackerStateChange != null)
				ThreadPool.QueueUserWorkItem(new WaitCallback(AsyncTrackerStateChange), e);
		}

		private void AsyncTrackerStateChange(object args)
		{
			if (this.OnTrackerStateChange != null)
				this.OnTrackerStateChange(this.manager, (TrackerStateChangedEventArgs)args);
		}

        #endregion
    }
}
