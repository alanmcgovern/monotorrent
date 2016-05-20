using System;

namespace MonoTorrent.Client.Tracker
{
    public abstract class TrackerResponseEventArgs : EventArgs
    {
        protected TrackerResponseEventArgs(Tracker tracker, object state, bool successful)
        {
            if (tracker == null)
                throw new ArgumentNullException("tracker");
            if (!(state is TrackerConnectionID))
                throw new ArgumentException("The state object must be the same object as in the call to Announce",
                    "state");
            Id = (TrackerConnectionID) state;
            Successful = successful;
            Tracker = tracker;
        }

        internal TrackerConnectionID Id { get; }

        public object State
        {
            get { return Id; }
        }

        /// <summary>
        ///     True if the request completed successfully
        /// </summary>
        public bool Successful { get; set; }

        /// <summary>
        ///     The tracker which the request was sent to
        /// </summary>
        public Tracker Tracker { get; protected set; }
    }
}