using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace MonoTorrent.Client
{
    public class TrackerTier : IEnumerable<Tracker>
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

        internal TrackerTier(MonoTorrentCollection<string> trackerUrls)
        {
            Uri result;
            List<Tracker> trackerList = new List<Tracker>(trackerUrls.Count);

            for (int i = 0; i < trackerUrls.Count; i++)
            {
                if (Uri.TryCreate(trackerUrls[i], UriKind.Absolute, out result))
                {
                    Tracker tracker = TrackerFactory.CreateForProtocol(result.Scheme, trackerUrls[i]);
                    if (tracker != null)
                    {
                        trackerList.Add(tracker);
                    }
                    else
                    {
                        Console.Error.WriteLine("Unsupported protocol {0}", result);
                    }
                }
                else
                {
                    Console.Error.WriteLine("Ignoring bad uri: {0}", trackerUrls[i]);
                }
            }

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


        public IEnumerator<Tracker> GetEnumerator()
        {
            for (int i = 0; i < trackers.Length; i++)
                yield return trackers[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion Methods
    }
}
