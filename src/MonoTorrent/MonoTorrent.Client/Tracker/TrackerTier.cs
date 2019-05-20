using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    public class TrackerTier : IEnumerable<ITracker>
    {
        #region Properties

        List<ITracker> OriginalTrackers { get; }

        internal bool SendingStartedEvent { get; set; }

        internal bool SentStartedEvent { get; set; }

        internal List<ITracker> Trackers { get; }

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

            OriginalTrackers = new List<ITracker>(trackerList);
            Trackers = trackerList;
        }

        #endregion Constructors


        #region Methods

        internal int IndexOf(ITracker tracker)
        {
            return Trackers.IndexOf(tracker);
        }

        public IEnumerator<ITracker> GetEnumerator()
        {
            return Trackers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public List<ITracker> GetTrackers()
        {
            return new List<ITracker>(OriginalTrackers);
        }

        #endregion Methods
    }
}
