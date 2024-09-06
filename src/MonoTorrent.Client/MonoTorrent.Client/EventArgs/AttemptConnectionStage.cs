using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Peers can be banned at two points, when the connection has been opened, or after the handshakes have been transferred.
    /// </summary>
    public enum AttemptConnectionStage
    {
        /// <summary>
        /// This is invoked before an outgoing connection attempt is made so peers can be discarded at the earliest point in time.
        /// For incoming connections, this is invoked before any data is sent or received through the connection.
        /// At this stage the only information available is the peers <see cref="PeerInfo.ConnectionUri"/>, which contains the
        /// IP address and port of the remote peer.
        /// </summary>
        BeforeConnectionEstablished,

        /// <summary>
        /// At this stage both <see cref="PeerInfo.ConnectionUri"/> and <see cref="PeerInfo.PeerId"/> are known as the BitTorrent
        /// handshakes have been exchanged.
        /// </summary>
        HandshakeComplete,
    }
}
