namespace MonoTorrent.Client
{
    /// <summary>
    ///     Provides the data needed to handle a PieceHashed event
    /// </summary>
    public class PieceHashedEventArgs : TorrentEventArgs
    {
        #region Constructors

        /// <summary>
        ///     Creates a new PieceHashedEventArgs
        /// </summary>
        /// <param name="pieceIndex">The index of the piece that was hashed</param>
        /// <param name="hashPassed">True if the piece passed the hashcheck, false otherwise</param>
        public PieceHashedEventArgs(TorrentManager manager, int pieceIndex, bool hashPassed)
            : base(manager)
        {
            PieceIndex = pieceIndex;
            HashPassed = hashPassed;
        }

        #endregion

        #region Member Variables

        /// <summary>
        ///     The index of the piece that was just hashed
        /// </summary>
        public int PieceIndex { get; }


        /// <summary>
        ///     The value of whether the piece passed or failed the hash check
        /// </summary>
        public bool HashPassed { get; }

        #endregion
    }
}