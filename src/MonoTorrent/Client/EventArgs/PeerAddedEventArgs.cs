namespace MonoTorrent.Client
{
    /// <summary>
    ///     Provides the data needed to handle a PeersAdded event
    /// </summary>
    public class PeerAddedEventArgs : TorrentEventArgs
    {
        #region Constructors

        /// <summary>
        ///     Creates a new PeersAddedEventArgs
        /// </summary>
        /// <param name="peersAdded">The number of peers just added</param>
        public PeerAddedEventArgs(TorrentManager manager, Peer peerAdded)
            : base(manager)
        {
            Peer = peerAdded;
        }

        #endregion

        #region Member Variables

        /// <summary>
        ///     The number of peers that were added in the last update
        /// </summary>
        public Peer Peer { get; }

        #endregion
    }
}