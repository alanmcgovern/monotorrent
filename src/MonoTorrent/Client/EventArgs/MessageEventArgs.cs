using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    ///     Provides the data needed to handle a PeerMessage event
    /// </summary>
    public class PeerMessageEventArgs : TorrentEventArgs
    {
        #region Constructors

        /// <summary>
        ///     Creates a new PeerMessageEventArgs
        /// </summary>
        /// <param name="message">The peer message involved</param>
        /// <param name="direction">The direction of the message</param>
        internal PeerMessageEventArgs(TorrentManager manager, PeerMessage message, Direction direction, PeerId id)
            : base(manager)
        {
            Direction = direction;
            ID = id;
            Message = message;
        }

        #endregion

        #region Member Variables

        /// <summary>
        ///     The Peer message that was just sent/Received
        /// </summary>
        public PeerMessage Message { get; }

        /// <summary>
        ///     The direction of the message (outgoing/incoming)
        /// </summary>
        public Direction Direction { get; }

        public PeerId ID { get; }

        #endregion
    }
}