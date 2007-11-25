using System;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    public abstract class TrackerResponseEventArgs : EventArgs
    {
        private string failureMessage;
        private Tracker tracker;
        private string warningMessage;

        public string FailureMessage
        {
            get { return failureMessage; }
            protected internal set { failureMessage = value ?? ""; }
        }
        
        public Tracker Tracker
        {
            get { return tracker; }
            protected set { tracker = value; }
        }

        public string WarningMessage
        {
            get { return warningMessage; }
            protected internal set { warningMessage = value ?? ""; }
        }


        protected TrackerResponseEventArgs(Tracker tracker)
        {
            if (tracker == null)
                throw new ArgumentNullException("tracker");

            this.tracker = tracker;
        }
    }
}
