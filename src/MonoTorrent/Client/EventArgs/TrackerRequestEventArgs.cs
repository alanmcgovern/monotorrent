using System;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Tracker
{
    public abstract class TrackerResponseEventArgs : EventArgs
    {
        private bool successful;
        TrackerConnectionID id;
        private Tracker tracker;

        internal TrackerConnectionID Id
        {
            get { return id; }
        }

        public object State
        {
            get { return id; }
        }

        /// <summary>
        /// True if the request completed successfully
        /// </summary>
        public bool Successful
        {
            get { return successful; }
            set { successful = value; }
        }

        /// <summary>
        /// The tracker which the request was sent to
        /// </summary>
        public Tracker Tracker
        {
            get { return tracker; }
            protected set { tracker = value; }
        }

        protected TrackerResponseEventArgs(Tracker tracker, object state, bool successful)
        {
            if (tracker == null)
                throw new ArgumentNullException("tracker");
            if (!(state is TrackerConnectionID))
                throw new ArgumentException("The state object must be the same object as in the call to Announce", "state");
            this.id = (TrackerConnectionID)state;
            this.successful = successful;
            this.tracker = tracker;
        }
    }
}
