using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    ///     Provides the data needed to handle a PeerConnection event
    /// </summary>
    public class PeerConnectionEventArgs : TorrentEventArgs
    {
        #region Member Variables

        public PeerId PeerID { get; }


        /// <summary>
        ///     The peer event that just happened
        /// </summary>
        public Direction ConnectionDirection { get; }

        /// <summary>
        ///     Any message that might be associated with this event
        /// </summary>
        public string Message { get; }

        #endregion

        #region Constructors

        internal PeerConnectionEventArgs(TorrentManager manager, PeerId id, Direction direction)
            : this(manager, id, direction, "")
        {
        }


        internal PeerConnectionEventArgs(TorrentManager manager, PeerId id, Direction direction, string message)
            : base(manager)
        {
            PeerID = id;
            ConnectionDirection = direction;
            Message = message;
        }

        #endregion
    }
}