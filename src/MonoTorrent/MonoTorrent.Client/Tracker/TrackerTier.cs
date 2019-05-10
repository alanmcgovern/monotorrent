using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public class TrackerTier : IEnumerable<ITracker>
    {
        #region Private Fields

        private bool sendingStartedEvent;
        private bool sentStartedEvent;
        private List<ITracker> trackers;

        #endregion Private Fields


        #region Properties

        internal bool SendingStartedEvent
        {
            get { return this.sendingStartedEvent; }
            set { this.sendingStartedEvent = value; }
        }

        internal bool SentStartedEvent
        {
            get { return this.sentStartedEvent; }
            set { this.sentStartedEvent = value; }
        }

        internal List<ITracker> Trackers
        {
            get { return this.trackers; }
        }

        #endregion Properties


        #region Constructors

        internal TrackerTier(IEnumerable<string> trackerUrls)
        {
            Uri result;
            List<ITracker> trackerList = new List<ITracker>();

            foreach (string trackerUrl in trackerUrls)
            {
                // FIXME: Debug spew?
                if (!Uri.TryCreate(trackerUrl, UriKind.Absolute, out result))
                {
                    Logger.Log(null, "TrackerTier - Invalid tracker Url specified: {0}", trackerUrl);
                    continue;
                }

                ITracker tracker = TrackerFactory.Create(result);
                if (tracker != null)
                {
                    trackerList.Add(tracker);
                }
                else
                {
                    Console.Error.WriteLine("Unsupported protocol {0}", result);                // FIXME: Debug spew?
                }
            }

            this.trackers = trackerList;
        }

        #endregion Constructors


        #region Methods

        internal int IndexOf(ITracker tracker)
        {
            return trackers.IndexOf(tracker);
        }

        public IEnumerator<ITracker> GetEnumerator()
        {
            return trackers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public List<ITracker> GetTrackers()
        {
            return new List<ITracker>(trackers);
        }

        #endregion Methods
    }
}
