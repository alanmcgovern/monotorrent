using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker
{
    /// <summary>
    ///     Provides the data needed to handle a TrackerUpdate event
    /// </summary>
    public class TrackerStateChangedEventArgs : TorrentEventArgs
    {
        #region Constructors

        /// <summary>
        ///     Creates a new TrackerUpdateEventArgs
        /// </summary>
        /// <param name="state">The current state of the update</param>
        /// <param name="response">The response of the tracker (if any)</param>
        public TrackerStateChangedEventArgs(TorrentManager manager, Tracker tracker, TrackerState oldState,
            TrackerState newState)
            : base(manager)
        {
            Tracker = tracker;
            OldState = oldState;
            NewState = newState;
        }

        #endregion

        #region Member Variables

        /// <summary>
        ///     The current status of the tracker update
        /// </summary>
        public Tracker Tracker { get; }


        public TrackerState OldState { get; }


        public TrackerState NewState { get; }

        #endregion
    }
}