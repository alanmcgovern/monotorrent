using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    ///     Provides the data needed to handle a TorrentStateChanged event
    /// </summary>
    public class TorrentStateChangedEventArgs : TorrentEventArgs
    {
        #region Constructors

        /// <summary>
        ///     Creates a new TorrentStateChangedEventArgs
        /// </summary>
        /// <param name="oldState">The old state of the Torrent</param>
        /// <param name="newState">The new state of the Torrent</param>
        public TorrentStateChangedEventArgs(TorrentManager manager, TorrentState oldState, TorrentState newState)
            : base(manager)
        {
            OldState = oldState;
            NewState = newState;
        }

        #endregion

        #region Member Variables

        /// <summary>
        ///     The old state for the torrent
        /// </summary>
        public TorrentState OldState { get; }


        /// <summary>
        ///     The new state for the torrent
        /// </summary>
        public TorrentState NewState { get; }

        #endregion
    }
}