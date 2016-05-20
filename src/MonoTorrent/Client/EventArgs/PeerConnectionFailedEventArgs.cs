using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class PeerConnectionFailedEventArgs : TorrentEventArgs
    {
        /// <summary>
        ///     Create new instance of PeerConnectionFailedEventArgs for peer from given torrent.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="peer"></param>
        /// <param name="direction">Which direction the connection attempt was</param>
        /// <param name="message">Message associated with the failure</param>
        public PeerConnectionFailedEventArgs(TorrentManager manager, Peer peer, Direction direction, string message)
            : base(manager)
        {
            Peer = peer;
            ConnectionDirection = direction;
            Message = message;
        }

        /// <summary>
        ///     Peer from which this event happened
        /// </summary>
        public Peer Peer { get; }

        /// <summary>
        ///     Direction of event (if our connection failed to them or their connection failed to us)
        /// </summary>
        public Direction ConnectionDirection { get; }

        /// <summary>
        ///     Any message that might be associated with this event
        /// </summary>
        public string Message { get; }
    }
}