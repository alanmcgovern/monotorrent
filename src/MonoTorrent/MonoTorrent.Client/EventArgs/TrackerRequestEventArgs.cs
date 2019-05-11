using System;
using System.Collections.Generic;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Tracker
{
    public abstract class TrackerResponseEventArgs : EventArgs
    {

        /// <summary>
        /// True if the request completed successfully
        /// </summary>
        public bool Successful { get; }

		/// <summary>
		/// The tracker which the request was sent to
		/// </summary>
		public Tracker Tracker { get; }

		protected TrackerResponseEventArgs(Tracker tracker, bool successful)
        {
            Successful = successful;
            Tracker = tracker ?? throw new ArgumentNullException(nameof (tracker));
        }
    }
}
