using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class TrackerTier
    {
        #region Private Fields

        private bool sendingStartedEvent;
        private bool sentStartedEvent;
        private Tracker[] trackers;

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

        public Tracker[] Trackers
        {
            get { return this.trackers; }
        }

        #endregion Properties


        #region Constructors

        internal TrackerTier(stringCollection trackerUrls, AsyncCallback announceCallback,
                        AsyncCallback scrapeCallback)
        {
            Uri result;
            List<Tracker> trackerList = new List<Tracker>(trackerUrls.Count);

            for (int i = 0; i < trackerUrls.Count; i++)
                if (Uri.TryCreate(trackerUrls[i], UriKind.Absolute, out result) && result.Scheme != "udp")
                    trackerList.Add(new Tracker(trackerUrls[i], announceCallback, scrapeCallback));

            this.trackers = trackerList.ToArray();
        }

        #endregion Constructors


        #region Methods

        internal int IndexOf(Tracker tracker)
        {
            for (int i = 0; i < this.trackers.Length; i++)
                if (this.trackers[i].Equals(tracker))
                    return i;

            return -1;
        }

        #endregion Methods
    }
}
