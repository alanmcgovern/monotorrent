using System;
using System.Collections.Generic;
using System.Text;

using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class PeerConnectionFailedEventArgs : TorrentEventArgs
    {
        private Peer peer;
        private Direction connectionDirection;
        private String message;

        /// <summary>
        /// Peer from which this event happened
        /// </summary>
        public Peer Peer
        {
            get { return this.peer; }
        }

        /// <summary>
        /// Direction of event (if our connection failed to them or their connection failed to us)
        /// </summary>
        public Direction ConnectionDirection
        {
            get { return this.connectionDirection; }
        }

        /// <summary>
        /// Any message that might be associated with this event
        /// </summary>
        public String Message
        {
            get { return message; }
        }


        /// <summary>
        /// Create new instance of PeerConnectionFailedEventArgs for peer from given torrent.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="peer"></param>
        /// <param name="direction">Which direction the connection attempt was</param>
        /// <param name="message">Message associated with the failure</param>
        public PeerConnectionFailedEventArgs(TorrentManager manager, Peer peer, Direction direction, String message)
            : base(manager)
        {
            this.peer = peer;
            this.connectionDirection = direction;
            this.message = message;
        }
    }
}
