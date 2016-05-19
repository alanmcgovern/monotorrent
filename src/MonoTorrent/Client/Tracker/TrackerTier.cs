using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public class TrackerTier : IEnumerable<Tracker>
    {
        #region Private Fields

        private bool sendingStartedEvent;
        private bool sentStartedEvent;
        private List<Tracker> trackers;

        #endregion Private Fields

        #region Properties

        internal bool SendingStartedEvent
        {
            get { return sendingStartedEvent; }
            set { sendingStartedEvent = value; }
        }

        internal bool SentStartedEvent
        {
            get { return sentStartedEvent; }
            set { sentStartedEvent = value; }
        }

        internal List<Tracker> Trackers
        {
            get { return trackers; }
        }

        #endregion Properties

        #region Constructors

        internal TrackerTier(IEnumerable<string> trackerUrls)
        {
            Uri result;
            var trackerList = new List<Tracker>();

            foreach (var trackerUrl in trackerUrls)
            {
                // FIXME: Debug spew?
                if (!Uri.TryCreate(trackerUrl, UriKind.Absolute, out result))
                {
                    Logger.Log(null, "TrackerTier - Invalid tracker Url specified: {0}", trackerUrl);
                    continue;
                }

                var tracker = TrackerFactory.Create(result);
                if (tracker != null)
                {
                    trackerList.Add(tracker);
                }
                else
                {
                    Console.Error.WriteLine("Unsupported protocol {0}", result); // FIXME: Debug spew?
                }
            }

            trackers = trackerList;
        }

        #endregion Constructors

        #region Methods

        internal int IndexOf(Tracker tracker)
        {
            return trackers.IndexOf(tracker);
        }

        public IEnumerator<Tracker> GetEnumerator()
        {
            return trackers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public List<Tracker> GetTrackers()
        {
            return new List<Tracker>(trackers);
        }

        #endregion Methods
    }
}