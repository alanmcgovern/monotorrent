using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Tracker
{
    public abstract class ListenerBase
    {
        public event EventHandler<ScrapeParameters> ScrapeReceived;
        public event EventHandler<AnnounceParameters> AnnounceReceived;

        /// <summary>
        /// True if the listener is actively listening for incoming connections
        /// </summary>
        public abstract bool Running { get; }

        /// <summary>
        /// Starts listening for incoming connections
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Stops listening for incoming connections
        /// </summary>
        public abstract void Stop();

        protected virtual void RaiseAnnounceReceived(AnnounceParameters e)
        {
            EventHandler<AnnounceParameters> h = AnnounceReceived;
            if (h != null)
                h(this, e);
        }

        protected virtual void RaiseScrapeReceived(ScrapeParameters e)
        {
            EventHandler<ScrapeParameters> h = ScrapeReceived;
            if (h != null)
                h(this, e);
        }
    }
}
