using MonoTorrent.BEncoding;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer
{
    public interface IPeerConnectionGate
    {
        ReusableTask<bool> TryAcceptHandshakeAsync (BEncodedString localPeerId, PeerInfo remotePeer,
            IPeerConnection connection, InfoHash infoHash);
    }
}
