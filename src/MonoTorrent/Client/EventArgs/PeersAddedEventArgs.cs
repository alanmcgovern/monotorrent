namespace MonoTorrent.Client
{
    /// <summary>
    ///     Provides the data needed to handle a PeersAdded event
    /// </summary>
    public abstract class PeersAddedEventArgs : TorrentEventArgs
    {
        private readonly int total;

        #region Constructors

        /// <summary>
        ///     Creates a new PeersAddedEventArgs
        /// </summary>
        /// <param name="peersAdded">The number of peers just added</param>
        protected PeersAddedEventArgs(TorrentManager manager, int peersAdded, int total)
            : base(manager)
        {
            NewPeers = peersAdded;
            this.total = total;
        }

        #endregion

        #region Member Variables

        public int ExistingPeers
        {
            get { return total - NewPeers; }
        }

        public int NewPeers { get; }

        #endregion
    }
}